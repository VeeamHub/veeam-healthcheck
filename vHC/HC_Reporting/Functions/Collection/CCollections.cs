// <copyright file="CCollections.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Management.Infrastructure;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection
{
    internal class CCollections
    {
        private static readonly Regex AnsiPattern = new Regex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

        public bool SCRIPTSUCCESS;
        private readonly CLogger log = CGlobals.Logger;

        internal static string StripAnsiCodes(string s) =>
            s is null ? string.Empty : AnsiPattern.Replace(s, string.Empty);

        // Hard ceiling for the VBR MFA pre-check PowerShell process. On VBR v13 an
        // untrusted server certificate (or any other interactive prompt) can block
        // Connect-VBRServer forever; this timeout guarantees collection always proceeds
        // or fails cleanly instead of hanging indefinitely (GitHub issue #149).
        //
        // Set generously (90s): the ceiling is a safety net for a genuine hang, not a
        // performance budget — with -ForceAcceptTlsCertificate present the check normally
        // returns in a second or two. A cold `Import-Module Veeam.Backup.PowerShell` +
        // Connect-VBRServer handshake on a large/loaded v13 server can legitimately take
        // tens of seconds, and a too-tight ceiling has false-failed big environments before.
        internal const int MfaCheckTimeoutSeconds = 90;

        /// <summary>
        /// Builds the PowerShell script for the local (Windows-auth) VBR MFA pre-check.
        /// <c>-ForceAcceptTlsCertificate</c> is added ONLY for VBR v13+ (the parameter does not
        /// exist on v12): on v13 a self-signed / untrusted server certificate makes Connect-VBRServer
        /// raise an interactive trust prompt that can never be answered when the process runs headless
        /// (CreateNoWindow, no stdin), hanging the collection forever (issue #149). <c>-ErrorAction
        /// Stop</c> makes a failed connect surface as a non-zero exit code rather than a silent success.
        /// </summary>
        internal static string BuildLocalMfaConnectScript(int vbrMajorVersion)
        {
            // -ForceAcceptTlsCertificate ONLY exists on VBR v13+. Passing it to v12's
            // Connect-VBRServer throws "A parameter cannot be found that matches parameter
            // name 'ForceAcceptTlsCertificate'" and breaks collection, so include it only for
            // v13+ — which is also the only version where the untrusted-cert prompt hangs (#149).
            string certFlag = vbrMajorVersion >= 13 ? " -ForceAcceptTlsCertificate" : string.Empty;
            return "Import-Module Veeam.Backup.PowerShell -WarningAction Ignore; " +
                   $"Connect-VBRServer -Server localhost{certFlag} -ErrorAction Stop";
        }

        /// <summary>
        /// Starts a PowerShell process from <paramref name="startInfo"/> and runs it under a hard
        /// timeout with non-blocking (async) reads of BOTH output streams. Prevents the historical
        /// hang where an interactive prompt (e.g. an unaccepted VBR server certificate on v13) or a
        /// full redirected-pipe buffer blocked forever on synchronous ReadToEnd()/WaitForExit()
        /// (issue #149). On timeout the process and its child tree are killed and
        /// <paramref name="timedOut"/> is set true. Emits ample debug logging throughout.
        /// </summary>
        /// <returns>true if the process started and exited on its own within the timeout;
        /// false if it failed to start or was killed after timing out.</returns>
        internal static bool RunBoundedPowerShell(
            ProcessStartInfo startInfo,
            int timeoutSeconds,
            string logTag,
            out string stdOut,
            out string stdErr,
            out int exitCode,
            out bool timedOut)
        {
            stdOut = string.Empty;
            stdErr = string.Empty;
            exitCode = -1;
            timedOut = false;

            CGlobals.Logger.Debug($"{logTag} Launching '{startInfo.FileName}' with a {timeoutSeconds}s timeout (async stream reads to avoid pipe deadlock).");

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                CGlobals.Logger.Error($"{logTag} Failed to start PowerShell process (Process.Start returned null).", false);
                return false;
            }

            CGlobals.Logger.Debug($"{logTag} PowerShell process started. PID={process.Id}. Waiting up to {timeoutSeconds}s for exit...");

            // Begin reading both streams asynchronously BEFORE waiting, so a full pipe buffer on
            // one stream can never deadlock the other (the classic ReadToEnd()-ordering hang).
            Task<string> outTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errTask = process.StandardError.ReadToEndAsync();

            bool exited = process.WaitForExit(timeoutSeconds * 1000);

            if (!exited)
            {
                timedOut = true;
                CGlobals.Logger.Warning(
                    $"{logTag} PowerShell process TIMED OUT after {timeoutSeconds}s — killing PID {process.Id}. " +
                    "Most likely an unaccepted VBR server certificate or another interactive prompt is blocking Connect-VBRServer.", false);
                try
                {
                    process.Kill(entireProcessTree: true);
                    CGlobals.Logger.Debug($"{logTag} Kill signal sent to PID {process.Id} and its child process tree.");
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Debug($"{logTag} Failed to kill timed-out process PID {process.Id}: {ex.Message}");
                }

                // Best-effort capture of whatever was produced before the kill (never block).
                try { if (outTask.Wait(2000)) { stdOut = outTask.Result ?? string.Empty; } } catch { /* ignore */ }
                try { if (errTask.Wait(2000)) { stdErr = StripAnsiCodes(errTask.Result); } } catch { /* ignore */ }
                CGlobals.Logger.Debug($"{logTag} Post-timeout capture — STDOUT: {stdOut.Length} chars, STDERR: {stdErr.Length} chars.");
                return false;
            }

            try { stdOut = outTask.GetAwaiter().GetResult() ?? string.Empty; }
            catch (Exception ex) { CGlobals.Logger.Debug($"{logTag} Error reading STDOUT: {ex.Message}"); }

            try { stdErr = StripAnsiCodes(errTask.GetAwaiter().GetResult()); }
            catch (Exception ex) { CGlobals.Logger.Debug($"{logTag} Error reading STDERR: {ex.Message}"); }

            exitCode = process.ExitCode;
            CGlobals.Logger.Debug($"{logTag} PowerShell process exited on its own. ExitCode={exitCode}, STDOUT={stdOut.Length} chars, STDERR={stdErr.Length} chars.");
            return true;
        }

        public CCollections() { }

        /* All collection utilities should run through here:
         * - powershell
         * - SQL
         * - Logs
         * - Other?
         * 
         */
        public void Run()
        {
            if (CGlobals.RunSecReport)
            {
                this.ExecSecurityCollection();
            }

            // main powershell execution point
            this.ExecPSScripts();

            // run diagnostic of CSV output and sizes, dump to logs:
            if (CGlobals.EffectiveIsVbr)
            {
                this.GetCsvFileSizesToLog();
            }

            // GetCsvFileSizesToLog();

            this.log.Info("[Collections] Phase: Recon check...", false);
            CheckRecon();
            this.log.Info("[Collections] Phase: Recon check...done!", false);

            if (!CGlobals.RunSecReport && CGlobals.EffectiveIsVbr)
            {
                this.log.Info("[Collections] Phase: Log wait analysis...", false);
                this.PopulateWaits();
                this.log.Info("[Collections] Phase: Log wait analysis...done!", false);
            }

            if (CGlobals.IsVbr && !CGlobals.REMOTEEXEC)
            {
                this.log.Info("[Collections] Phase: VMC reader...", false);
                this.ExecVmcReader();
                this.log.Info("[Collections] Phase: VMC reader...done!", false);

                this.log.Info("[Collections] Phase: Registry DB info...", false);
                this.GetRegistryDbInfo();
                this.log.Info("[Collections] Phase: Registry DB info...done!", false);

                if (CGlobals.DBTYPE != CGlobals.PgTypeName)
                {
                    this.log.Info(string.Format("[Collections] Phase: SQL queries (DBTYPE={0})...", CGlobals.DBTYPE), false);
                    this.ExecSqlQueries();
                    this.log.Info("[Collections] Phase: SQL queries...done!", false);
                }
                else
                {
                    this.log.Info(string.Format("[Collections] Phase: SQL queries skipped (PostgreSQL backend, DBTYPE={0})", CGlobals.DBTYPE), false);
                }
            }

            this.log.Info("[Collections] Run() complete.", false);
        }

        private static void CheckRecon()
        {
            if (CGlobals.DEBUG)
            {
                CGlobals.Logger.Debug("Checking for Coveware Recon Task");
            }

            CReconChecker rc = new();
            rc.Check();
        }

        private void GetCsvFileSizesToLog()
        {
            if (CGlobals.DEBUG)
            {
                CGlobals.Logger.Debug("Logging CSV File Sizes:");
            }

            // Check if directory exists before attempting to access it
            if (!Directory.Exists(CVariables.vbrDir))
            {
                CGlobals.Logger.Debug($"VBR directory does not exist: {CVariables.vbrDir}");
                return;
            }

            var files = Directory.GetFiles(CVariables.vbrDir, "*.csv", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileSize = fileInfo.Length;
                if (fileSize > 0)
                {
                    CGlobals.Logger.Info($"\tFile: {fileInfo.Name} Size: {fileSize}");
                }
                else
                {
                    CGlobals.Logger.Warning($"\tFile: {fileInfo.Name} Size: {fileSize}");
                }
            }
        }

        private void ExecSecurityCollection()
        {
            CSecurityInit securityInit = new CSecurityInit();
            securityInit.Run();
        }

        private void ExecVmcReader()
        {
            if (CGlobals.IsVbr)
            {
                CLogOptions logOptions = new("vbr");
            }

            if (CGlobals.IsVb365)
            {
                CLogOptions logOptions = new("vb365");
            }
        }

        private void GetRegistryDbInfo()
        {
            try
            {
                CRegReader reg = new CRegReader();

                this.log.Info("[Collections] Registry: reading DB info (GetDbInfo)...", false);
                reg.GetDbInfo();
                this.log.Info("[Collections] Registry: reading DB info (GetDbInfo)...done!", false);

                if (CGlobals.REMOTEEXEC)
                {
                    this.log.Info("[Collections] Registry: reading default VBR keys (remote)...", false);
                    CGlobals.DEFAULTREGISTRYKEYS = reg.DefaultVbrKeysRemote();
                }
                else
                {
                    this.log.Info("[Collections] Registry: reading default VBR keys (local)...", false);
                    CGlobals.DEFAULTREGISTRYKEYS = reg.DefaultVbrKeys();
                }

                this.log.Info("[Collections] Registry: default VBR keys...done!", false);
            }
            catch (Exception e)
            {
                // Don't let a registry-read failure silently abort collection. Log it loudly
                // and continue so report generation can still proceed with whatever was gathered.
                this.log.Error("[Collections] Registry DB info collection failed: " + e.Message, false);
            }
        }

        private void ExecSqlQueries()
        {
            CSqlExecutor sql = new();
            sql.Run();
        }

        private void ExecPSScripts()
        {
            CGlobals.Logger.Info("Starting PS Invoke", false);
            PSInvoker p = new PSInvoker();

            if (!CGlobals.RunSecReport)
            {
                try
                {
                    bool runVbr = CGlobals.EffectiveIsVbr;
                    bool runVb365 = CGlobals.EffectiveIsVb365;

                    // Dynamic fallback when remote + Auto + no local detection
                    if (CGlobals.TargetProductType == TargetProduct.Auto && CGlobals.REMOTEEXEC && !runVbr && !runVb365)
                    {
                        (runVbr, runVb365) = this.DynamicFallback();
                    }

                    if (runVbr)
                    {
                        // Ensure VBR output directory exists (with server name and timestamp)
                        if (!Directory.Exists(CVariables.vbrDir))
                        {
                            Directory.CreateDirectory(CVariables.vbrDir);
                            CGlobals.Logger.Debug($"Created VBR directory: {CVariables.vbrDir}");
                        }

                        CGlobals.Logger.Info("Checking VBR MFA Access...", false);
                        if (this.MfaTestPassed(p))
                        {
                            // add debug logging to help troubleshoot MFA issues
                            CGlobals.Logger.Debug("MFA Not detected, continuing...");

                            this.ExecVbrScripts(p);
                        }
                        else
                        {
                            this.WeighSuccessContinuation();
                        }
                    }

                    if (runVb365)
                    {
                        CGlobals.Logger.Info("Checking VB365 MFA Access...", false);
                        if (!this.TestPsMfaVb365(p))
                        {
                            this.ExecVb365Scripts(p);
                        }
                        else
                        {
                            this.WeighSuccessContinuation();
                        }
                    }
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error(ex.Message);
                }
            }
            else if (CGlobals.RunSecReport)
            {
                this.ExecVbrConfigOnly(p);
            }

            WeighSuccessContinuation();
            CGlobals.Logger.Info("Starting PS Invoke...done!", false);
        }

        private void WeighSuccessContinuation()
        {
            if (this.SCRIPTSUCCESS)
            {
                return;
            }

            string defaultError = $"Script execution has failed. Exiting program. See log for details:\n\t {CGlobals.Logger.logFile}";
            // Prefer a specific user-facing error if previously captured
            string errorToShow = string.IsNullOrWhiteSpace(CGlobals.UserFacingError) ? defaultError : CGlobals.UserFacingError + "\n\nSee log for details:\n\t " + CGlobals.Logger.logFile;

            CGlobals.Logger.Error(errorToShow, false);

            if (CGlobals.GUIEXEC)
            {
                CGlobals.Notifier.ShowError(errorToShow, "Error");
            }

            Environment.Exit(1);
        }

        private bool MfaTestPassed(PSInvoker p)
        {
            // Determine if we need credentials:
            // - If VBR is installed locally (CGlobals.IsVbr) AND we're not doing remote execution, use Windows auth
            // - Otherwise, credentials are required
            bool isLocalVbr = CGlobals.IsVbr && !CGlobals.REMOTEEXEC;

            // For local VBR without remote flag, use Windows authentication (no credentials needed)
            if (isLocalVbr)
            {
                this.log.Info("Local VBR detected, using Windows authentication (no credentials required)...", false);
                return this.RunLocalMfaCheckNoCredentials(p);
            }

            // For remote execution, credentials are required (will prompt if not stored)
            this.log.Info("Remote execution detected, credentials required...", false);

            CredsHandler ch = new();
            var creds = ch.GetCreds();

            // If credentials not provided, cannot continue
            if (creds == null)
            {
                CGlobals.Logger.Error("Credentials required for remote execution but not provided.");
                if (CGlobals.Silent)
                {
                    string host = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;
                    SilentExit.ExitSilent(
                        SilentExit.CredsMissing,
                        $"No credentials for host '{host}'. Seed with /savecreds or supply /credfile=.");
                }
                return false;
            }

            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Functions\Collection\PSCollections\Scripts\TestMfa.ps1");
            bool result = false;
            string error = string.Empty;
            List<string> output = new();

            string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
            if (!File.Exists(pwshPath))
            {
                // we have determined required PS Version in CGLobals earlier. If PowerShell 7 required and not found, log and return failure.
                if (CGlobals.PowerShellVersion == 7)
                {
                    CGlobals.Logger.Debug("PowerShell 7 not found at: " + pwshPath, false);
                    CGlobals.Logger.Error("PowerShell 7 is required but not found. MFA test cannot proceed.");

                    return false;
                }
            }

            try
            {
                if (string.IsNullOrEmpty(CGlobals.REMOTEHOST))
                {
                    CGlobals.REMOTEHOST = "localhost";
                }

                // Base64-encode only to avoid PowerShell arg quoting issues (argument-safety,
                // NOT encryption — Base64 is reversible; the script decodes it to plaintext).
                string base64Password = CredentialHelper.EncodePasswordToBase64(creds.Value.Password);
                // Escape server and username for the double-quoted argument context
                // (prevents PowerShell argument injection via these fields).
                string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
                string escapedUser = CredentialHelper.EscapeForPowerShellDoubleQuotes(creds.Value.Username);

                // Build PowerShell arguments with Base64-encoded password
                // Use double quotes around Base64 string to avoid issues with special characters
                string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Server \"{escapedServer}\" -Username \"{escapedUser}\" -PasswordBase64 \"{base64Password}\" -VBRVersion {CGlobals.VBRMAJORVERSION}";

                var processInfo = new ProcessStartInfo
                {
                    FileName = pwshPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Log processInfo settings - construct safe log message without ever including sensitive data
                string safeArgs = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Server \"{escapedServer}\" -Username \"{escapedUser}\" -PasswordBase64 \"****\" -VBRVersion {CGlobals.VBRMAJORVERSION}";
                CGlobals.Logger.Debug($"ProcessStartInfo Settings:\n  FileName: {processInfo.FileName}\n  Arguments: {safeArgs}\n  RedirectStandardOutput: {processInfo.RedirectStandardOutput}\n  RedirectStandardError: {processInfo.RedirectStandardError}\n  UseShellExecute: {processInfo.UseShellExecute}\n  CreateNoWindow: {processInfo.CreateNoWindow}");
                // Run under a bounded timeout with async reads so an interactive prompt or a full
                // pipe buffer cannot hang the remote MFA check forever (issue #149 defense-in-depth).
                // The cert flag is handled inside TestMfa.ps1, gated on the -VBRVersion we pass above.
                RunBoundedPowerShell(
                    processInfo,
                    MfaCheckTimeoutSeconds,
                    "[Remote MFA Check]",
                    out string stdOut,
                    out string stdErr,
                    out int exitCode,
                    out bool timedOut);

                if (timedOut)
                {
                    string timeoutMsg = $"Remote VBR MFA check to '{CGlobals.REMOTEHOST}' timed out after {MfaCheckTimeoutSeconds}s. " +
                        "The server may be unreachable, or is waiting on an unaccepted certificate / interactive prompt.";
                    CGlobals.Logger.Error(timeoutMsg, false);
                    CGlobals.UserFacingError = timeoutMsg;
                    if (CGlobals.Silent)
                    {
                        string host = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;
                        SilentExit.ExitSilent(
                            SilentExit.HostUnreachable,
                            $"Remote MFA check to host '{host}' timed out after {MfaCheckTimeoutSeconds}s.");
                    }
                    return false;
                }

                error = stdErr;
                if (!string.IsNullOrWhiteSpace(stdOut))
                {
                    output.Add(stdOut);
                }


                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    output.Add(stdErr);
                }


                result = exitCode == 0;

                // Log result summary only - avoid logging full output which could contain sensitive data in error messages
                CGlobals.Logger.Debug($"MFA Test Result: ExitCode={exitCode}, StdOutLength={stdOut?.Length ?? 0}, StdErrLength={stdErr?.Length ?? 0}");

                // Detect specific error conditions and provide user-friendly messages
                if (!result && !string.IsNullOrWhiteSpace(stdErr))
                {
                    // Version mismatch between local console and remote server
                    if (stdErr.Contains("client update is required", StringComparison.OrdinalIgnoreCase) ||
                        stdErr.Contains("linked to a local server installation version", StringComparison.OrdinalIgnoreCase))
                    {
                        string userMsg = $"VBR Console/Client version mismatch detected.\n\n" +
                                       $"The local VBR console version does not match the remote server version.\n" +
                                       $"Remote server: {CGlobals.REMOTEHOST}\n\n" +
                                       $"To fix this:\n" +
                                       $"1. Update the local VBR console to match the remote server version, OR\n" +
                                       $"2. Install a standalone VBR console (not linked to a local server), OR\n" +
                                       $"3. Run VeeamHealthCheck directly on the remote VBR server\n\n" +
                                       $"Original error: {stdErr.Trim()}";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                    }
                    // MFA detected — must be checked BEFORE the generic auth-failed branch
                    // because TestMfa.ps1 emits an MFA-specific signal that we want to map
                    // to exit 4 in silent mode (vs. exit 3 for generic auth failure).
                    else if (stdErr.Contains("MFA-enabled", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("Unable to connect to the server with MFA-enabled user account", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("IsMfaEnabled", StringComparison.OrdinalIgnoreCase))
                    {
                        string userMsg = $"Account is MFA-enabled for remote VBR server: {CGlobals.REMOTEHOST}\n\n" +
                                       $"MFA is not supported for unattended VBR connections.\n" +
                                       $"Use a non-MFA service account.";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                        if (CGlobals.Silent)
                        {
                            SilentExit.ExitSilent(
                                SilentExit.MfaUnsupported,
                                "Account is MFA-enabled. MFA is not supported for unattended VBR connections. Use a service account.");
                        }
                    }
                    // Invalid credentials
                    else if (stdErr.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("authentication failed", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("invalid credentials", StringComparison.OrdinalIgnoreCase))
                    {
                        string userMsg = $"Authentication failed for remote VBR server: {CGlobals.REMOTEHOST}\n\n" +
                                       $"Please verify:\n" +
                                       $"1. Username and password are correct\n" +
                                       $"2. Account has permissions to connect to VBR\n" +
                                       $"3. Account is not MFA-enabled (MFA not supported for remote connections)";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                        if (CGlobals.Silent)
                        {
                            SilentExit.ExitSilent(
                                SilentExit.AuthFailed,
                                $"Authentication failed for host '{CGlobals.REMOTEHOST}'. Verify stored credentials.");
                        }
                    }
                    // Network connectivity issues
                    else if (stdErr.Contains("unable to connect", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("cannot be resolved", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("network path", StringComparison.OrdinalIgnoreCase))
                    {
                        string userMsg = $"Unable to connect to remote VBR server: {CGlobals.REMOTEHOST}\n\n" +
                                       $"Please verify:\n" +
                                       $"1. Server name/IP is correct\n" +
                                       $"2. Server is reachable on the network\n" +
                                       $"3. Firewall allows VBR connections (port 9392)\n" +
                                       $"4. VBR service is running on the remote server";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                        if (CGlobals.Silent)
                        {
                            SilentExit.ExitSilent(
                                SilentExit.HostUnreachable,
                                $"Cannot reach host '{CGlobals.REMOTEHOST}'. Check network/firewall/VBR service.");
                        }
                    }
                    else
                    {
                        // Generic error - log the raw error
                        CGlobals.Logger.Error($"MFA test failed with error: {stdErr.Trim()}", false);
                    }
                }
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error("Error during MFA test:");
                CGlobals.Logger.Error(ex.Message);
                result = false;
            }

            if (!result && CGlobals.PowerShellVersion == 5)
            {

                CGlobals.Logger.Warning("Failing over to PowerShell 5", false);

                return this.RunLocalMfaCheck(p);
            }

            return result;
        }

        /// <summary>
        /// Runs MFA check for local VBR without credentials (Windows authentication).
        /// Uses Connect-VBRServer -Server localhost without -Credential parameter.
        /// </summary>
        private bool RunLocalMfaCheckNoCredentials(PSInvoker p)
        {
            try
            {
                // Validate that PowerShellVersion was actually set during version detection
                if (CGlobals.PowerShellVersion == 0)
                {
                    CGlobals.Logger.Error("PowerShell version not determined. VBR version detection may have failed.", false);
                    CGlobals.Logger.Error("This usually indicates VBR version could not be read from registry or file system.", false);
                    return false;
                }

                string psExe;

                // Use the appropriate PowerShell version based on VBR version
                if (CGlobals.PowerShellVersion == 7)
                {
                    string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
                    if (!File.Exists(pwshPath))
                    {
                        CGlobals.Logger.Debug("PowerShell 7 not found at: " + pwshPath, false);
                        CGlobals.Logger.Error("PowerShell 7 is required but not found. MFA test cannot proceed.");
                        return false;
                    }
                    psExe = pwshPath;
                }
                else
                {
                    // Use PowerShell 5 for VBR version 12 and below
                    psExe = "powershell.exe";
                }

                // Simple Connect-VBRServer without credentials. On VBR v13 the connect string adds
                // -ForceAcceptTlsCertificate (built by BuildLocalMfaConnectScript, gated by version)
                // to avoid the headless cert-prompt hang; on v12 that flag is omitted because the
                // parameter does not exist there and would break the connect (issue #149).
                string script = BuildLocalMfaConnectScript(CGlobals.VBRMAJORVERSION);
                string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"";

                CGlobals.Logger.Debug($"[Local MFA Check] Running local MFA check with Windows auth: {psExe} -Command \"{script}\"");

                var processInfo = new ProcessStartInfo
                {
                    FileName = psExe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                RunBoundedPowerShell(
                    processInfo,
                    MfaCheckTimeoutSeconds,
                    "[Local MFA Check]",
                    out string stdOut,
                    out string stdErr,
                    out int exitCode,
                    out bool timedOut);

                // A timeout is a NON-blocking failure: surface a clear message and fail cleanly
                // instead of hanging the whole collection (issue #149). With the cert flag above
                // this should no longer trigger for the certificate case, but it protects against
                // any other interactive prompt or a wedged PowerShell/module load.
                if (timedOut)
                {
                    string timeoutMsg = $"VBR MFA pre-check timed out after {MfaCheckTimeoutSeconds}s. " +
                        "This usually means the VBR server certificate needs to be accepted. " +
                        "Try launching Veeam Health Check once from an elevated PowerShell or Command Prompt, " +
                        "accept the certificate prompt, then run the report again.";
                    CGlobals.Logger.Error(timeoutMsg, false);
                    CGlobals.UserFacingError = timeoutMsg;
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(stdErr))
                {

                    // Detect specific error conditions and surface clear, user-facing messages
                    if (stdErr.Contains("Unable to connect to the server with MFA-enabled user account", StringComparison.OrdinalIgnoreCase))
                    {
                        string userMsg = "Unable to connect to VBR because the current account is MFA-enabled. Please run Veeam Health Check from Command Prompt or PowerShell using a non-MFA-enabled account, or provide alternate credentials in the app.";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                    }
                    else if (stdErr.Contains("client update is required", StringComparison.OrdinalIgnoreCase) ||
                             stdErr.Contains("linked to a local server installation version", StringComparison.OrdinalIgnoreCase))
                    {
                        string userMsg = "VBR Console/Client version mismatch detected.\n\n" +
                                       "The VBR console version does not match the VBR server version.\n\n" +
                                       "To fix this:\n" +
                                       "1. Update both VBR console and server to the same version, OR\n" +
                                       "2. Install a standalone VBR console (not linked to a local server)";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                    }
                    else
                    {
                        // Log unknown errors for diagnosis
                        CGlobals.Logger.Error($"[Local MFA Check] PowerShell error output:\n{stdErr}");
                        string userMsg = "Failed to connect to VBR server. Check the log file for details.";
                        CGlobals.Logger.Error(userMsg, false);
                        CGlobals.UserFacingError = userMsg;
                    }
                }

                bool result = exitCode == 0;
                CGlobals.Logger.Info($"[Local MFA Check] Result: {(result ? "Success" : "Failed")} (ExitCode={exitCode})", false);

                return result;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error("Error during local MFA check:");
                CGlobals.Logger.Error(ex.Message);
                return false;
            }
        }

        private bool RunLocalMfaCheck(PSInvoker p)
        {
            try
            {
                var result = p.TestMfa();
                CGlobals.Logger.Info("[MFA Test] Result: " + result.ToString(), false);
                return result;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error("Error during MFA test:");
                CGlobals.Logger.Error(ex.Message);
                return false;
            }
        }

        private bool TestPsMfaVb365(PSInvoker p)
        {
            return p.TestMfaVB365();
        }

        private void ExecVbrScripts(PSInvoker p)
        {
            // debug log evaluation of what to run
            CGlobals.Logger.Debug("DEBUG: Evaluating PS Script Execution Conditions:");
            CGlobals.Logger.Debug("IsVbr: " + CGlobals.IsVbr.ToString());
            CGlobals.Logger.Debug("EffectiveIsVbr: " + CGlobals.EffectiveIsVbr.ToString());
            CGlobals.Logger.Debug("REMOTEEXEC: " + CGlobals.REMOTEEXEC.ToString());

            // No EffectiveIsVbr guard here — callers (including DynamicFallback path) already
            // determined runVbr=true; EffectiveIsVbr remains false in the remote+auto case.
            CGlobals.Logger.Info("Entering vbr ps invoker", false);
            this.SCRIPTSUCCESS = p.Invoke();
        }

        private void ExecVbrConfigOnly(PSInvoker p)
        {
            CGlobals.Logger.Info("Entering vbr config collection");
            this.SCRIPTSUCCESS = p.RunVbrConfigCollect();
            // Collector-failure warnings are logged in StartAnalysis after ValidateVbrCsvFiles
            // loads the manifest. Logging here would duplicate those warnings.
        }

        private void ExecVb365Scripts(PSInvoker p)
        {
            if (CGlobals.EffectiveIsVb365)
            {
                CGlobals.Logger.Info("Entering vb365 ps invoker", false);

                // p.InvokeVb365CollectEmbedded();
                this.SCRIPTSUCCESS = p.InvokeVb365Collect();
            }
        }

        private bool TryModuleLoad(string moduleName, string productLabel, int timeoutSeconds = 15)
        {
            try
            {
                CGlobals.Logger.Info($"[Dynamic Fallback] Trying {productLabel} connection to {CGlobals.REMOTEHOST}...", false);

                string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
                if (!File.Exists(pwshPath))
                    pwshPath = "powershell.exe";

                string script = $"Import-Module {moduleName} -WarningAction Ignore -ErrorAction Stop";
                string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = pwshPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                bool exited = process.WaitForExit(timeoutSeconds * 1000);

                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    CGlobals.Logger.Info($"[Dynamic Fallback] {productLabel} connection timed out", false);
                    return false;
                }

                bool result = process.ExitCode == 0;
                CGlobals.Logger.Info($"[Dynamic Fallback] {productLabel} module load: {(result ? "available" : "not available")}", false);
                return result;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Debug($"[Dynamic Fallback] {productLabel} test error: {ex.Message}");
                return false;
            }
        }

        private (bool runVbr, bool runVb365) DynamicFallback()
        {
            CGlobals.Logger.Info("No product type specified for remote server. Attempting auto-detection...", false);

            // Try both — VBR first (more common deployment)
            bool vbrAvailable = this.TryModuleLoad("Veeam.Backup.PowerShell", "VBR", timeoutSeconds: 15);
            bool vb365Available = this.TryModuleLoad("Veeam.Archiver.PowerShell", "VB365", timeoutSeconds: 15);

            if (!vbrAvailable && !vb365Available)
            {
                string msg = $"Unable to connect to {CGlobals.REMOTEHOST} as either VBR or VB365.\n\n" +
                             "Please verify:\n" +
                             "1. The server name/IP is correct\n" +
                             "2. VBR or VB365 is installed and running\n" +
                             "3. Use /vbr or /vb365 flag to specify the product type";
                CGlobals.Logger.Error(msg, false);
                CGlobals.UserFacingError = msg;
                return (false, false);
            }

            CGlobals.Logger.Info($"Auto-detection results: VBR={vbrAvailable}, VB365={vb365Available}", false);
            return (vbrAvailable, vb365Available);
        }

        private void PopulateWaits()
        {
            try
            {
                CLogParser lp = new();
                lp.GetWaitsFromFiles();
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Error checking log files:");
                CGlobals.Logger.Error(e.Message);
            }
        }
    }
}
