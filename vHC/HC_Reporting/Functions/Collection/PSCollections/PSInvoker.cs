// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Reflection;

// using System.Management.Automation;
using System.Runtime.InteropServices;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.Collection.PSCollections
{
    enum PowerShellVersion
    {
        PowerShell5,
        PowerShell7
    }

    class PSInvoker
    {
        private readonly string vb365Script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VB365\Collect-VB365Data.ps1");

        private readonly string vbrConfigScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VBRConfig.ps1");
        private readonly string mfaTestScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Functions\Collection\PSCollections\Scripts\TestMfa.ps1");

        private readonly string nasScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-NasInfo.ps1");

        private readonly string exportLogsScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HotfixDetection\Collect-VBRLogs.ps1");
        private readonly string dumpServers = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HotfixDetection\DumpManagedServerToText.ps1");

        private readonly string vbrConfigModuleDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\vHC-VbrConfig");

        public static readonly string SERVERLISTFILE = "serverlist.txt";

        private readonly CLogger log = CGlobals.Logger;
        private readonly string logStart = "[PsInvoker]\t";
        private PowerShellVersion? preferredVersion = null;
        private string pwshPath = null;
        private string powershellPath = null;

        // Remove duplicate constructor, keep only one with detection logic
        public PSInvoker()
        {
            this.DetectPowerShellVersions();
        }

        private void DetectPowerShellVersions()
        {
            // Try to find pwsh.exe (PowerShell 7)
            this.pwshPath = this.FindExecutableInPath("pwsh.exe");

            // Try to find powershell.exe (PowerShell 5)
            this.powershellPath = this.FindExecutableInPath("powershell.exe");

            if (!string.IsNullOrEmpty(this.pwshPath))
            {
                this.preferredVersion = PowerShellVersion.PowerShell7;
            }
            else if (!string.IsNullOrEmpty(this.powershellPath))
            {
                this.preferredVersion = PowerShellVersion.PowerShell5;
            }
            else
            {
                this.preferredVersion = null;
            }
        }

        private string FindExecutableInPath(string exeName)
        {
            var paths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    var exePath = Path.Combine(path.Trim(), exeName);
                    if (File.Exists(exePath))
                    {

                        return exePath;
                    }
                }
                catch { }
            }

            // Return default path if not found in PATH
            if (exeName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase))
            {

                return @"C:\Program Files\PowerShell\7\pwsh.exe";
            }


            return null;
        }

        private string GetPowerShellExecutable(PowerShellVersion version)
        {
            return version == PowerShellVersion.PowerShell7 ? this.pwshPath : this.powershellPath;
        }

        private ProcessStartInfo CreatePsStartInfo(string arguments, bool useShellExecute, bool createNoWindow, bool redirectStdErr, PowerShellVersion version)
        {
            var exePath = this.GetPowerShellExecutable(version);
            if (string.IsNullOrEmpty(exePath))
            {

                throw new InvalidOperationException($"PowerShell executable for version {version} not found.");
            }


            return new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false, // Must be false to redirect output
                CreateNoWindow = createNoWindow,
                RedirectStandardOutput = true,
                RedirectStandardError = true // Always redirect stderr for logging
            };
        }

        // Refactor script invocation to use failover
        public bool ExecutePsScriptWithFailover(string arguments, bool useShellExecute = false, bool createNoWindow = true, bool redirectStdErr = false)
        {
            if (this.preferredVersion == null)
            {
                this.log.Error("No PowerShell executable found on system.", false);
                return false;
            }

            // Try PowerShell 7 first, then 5
            foreach (var version in new[] { PowerShellVersion.PowerShell7, PowerShellVersion.PowerShell5 })
            {
                var exePath = this.GetPowerShellExecutable(version);
                if (string.IsNullOrEmpty(exePath))
                {
                    // add debug log to say which powershell version was not found
                    this.log.Debug($"[PS] {version} not found on system, skipping...", false);

                    continue;
                }

                try
                {
                    var startInfo = this.CreatePsStartInfo(arguments, useShellExecute, createNoWindow, redirectStdErr, version);

                    // log.Debug($"[PS] StartInfo: {startInfo.FileName} {startInfo.Arguments}");
                    var process = new Process { StartInfo = startInfo };
                    process.Start();
                    this.log.Info($"[PS] Script execution started with {version}. PID: {process.Id}", false);
                    process.WaitForExit();

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = CCollections.StripAnsiCodes(process.StandardError.ReadToEnd());
                    if (!string.IsNullOrWhiteSpace(stdOut))
                    {
                        this.log.Debug($"[PS][STDOUT] {stdOut}", false);
                    }


                    if (!string.IsNullOrWhiteSpace(stdErr))
                    {
                        this.log.Debug($"[PS][STDERR] {stdErr}", false);
                    }


                    this.log.Debug("Exit Code: " + process.ExitCode);

                    // Optionally check exit code or errors here
                    if (process.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        this.log.Warning($"[PS] Script failed with {version}. Exit code: {process.ExitCode}", false);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.log.Error($"[PS] Exception running script with {version}: {ex.Message}", false);
                }

                return false;
            }

            this.log.Error("[PS] Script failed with all available PowerShell versions. Exiting program", false);
            Environment.Exit(1);
            return false;
        }

        public bool Invoke()
        {
            bool res = true;
            this.TryUnblockFiles();

            // RunVbrVhcFunctionSetter();
            res = this.RunVbrConfigCollect();
            if (!res)
            {

                return false;
            }

            return res;
        }

        public void TryUnblockFiles()
        {
            this.UnblockFile(this.vbrConfigScript);
            this.UnblockFile(this.nasScript);
            UnblockFile(mfaTestScript);
            this.UnblockFile(this.exportLogsScript);
            this.UnblockFile(this.dumpServers);
            this.UnblockFile(this.vb365Script);

            // Unblock all PowerShell files in the vHC-VbrConfig module (.ps1, .psm1, .psd1).
            // Zone.Identifier on .psm1/.psd1 causes Unrestricted policy to prompt interactively,
            // which hangs the process when there is no console window.
            if (Directory.Exists(this.vbrConfigModuleDir))
            {
                foreach (var file in Directory.GetFiles(this.vbrConfigModuleDir, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".psm1", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".psd1", StringComparison.OrdinalIgnoreCase))
                    {
                        this.UnblockFile(file);
                    }
                }
            }
        }

        public bool TestMfa()
        {
            var res = new Process();
            if (CGlobals.REMOTEHOST == string.Empty)
            {
                CGlobals.REMOTEHOST = "localhost";
            }

            try
            {
                CredsHandler ch = new();
                var creds = ch.GetCreds();

                // Properly escape the password, username and server for the
                // single-quoted PowerShell argument context (prevents argument injection).
                string escapedPassword = CredentialHelper.EscapeForPowerShellSingleQuotes(creds.Value.Password);
                string escapedUser = CredentialHelper.EscapeForPowerShellSingleQuotes(creds.Value.Username);
                string escapedServer = CredentialHelper.EscapeForPowerShellSingleQuotes(CGlobals.REMOTEHOST ?? "localhost");


                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    // Use single quotes for the password to avoid interpretation of special characters
                    Arguments = $"Import-Module Veeam.Backup.PowerShell; Connect-VBRServer -Server '{escapedServer}' -User '{escapedUser}' -Password '{escapedPassword}'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Log the command with masked password - construct safe log message without ever including sensitive data
                string safeLogArgs = $"Import-Module Veeam.Backup.PowerShell; Connect-VBRServer -Server '{escapedServer}' -User '{escapedUser}' -Password '****'";
                CGlobals.Logger.Info("[TestMfa] Arguments: " + safeLogArgs);

                this.log.Info($"[TestMfa] Creating ProcessStartInfo for MFA test:");
                this.log.Info($"[TestMfa] FileName: {startInfo.FileName}");
                this.log.Info($"[TestMfa] Arguments: {safeLogArgs}");
                this.log.Info($"[TestMfa] RedirectStandardOutput: {startInfo.RedirectStandardOutput}");
                this.log.Info($"[TestMfa] RedirectStandardError: {startInfo.RedirectStandardError}");
                this.log.Info($"[TestMfa] UseShellExecute: {startInfo.UseShellExecute}");
                this.log.Info($"[TestMfa] CreateNoWindow: {startInfo.CreateNoWindow}");
                this.log.Info("[TestMfa] Starting PowerShell process for MFA test...");
                try
                {
                    res.StartInfo = startInfo;
                    res.Start();
                    this.log.Info($"[TestMfa] PowerShell process started. PID: {res.Id}");

                    res.WaitForExit();
                    this.log.Info($"[TestMfa] PowerShell process exited with code: {res.ExitCode}");

                    string stdOut = res.StandardOutput.ReadToEnd();
                    string stdErr = CCollections.StripAnsiCodes(res.StandardError.ReadToEnd());

                    // Note: Not logging full stdout/stderr to avoid potential password leakage in error messages
                    this.log.Debug($"[TestMfa] STDOUT length: {stdOut?.Length ?? 0} chars");
                    this.log.Debug($"[TestMfa] STDERR length: {stdErr?.Length ?? 0} chars");

                    List<string> errorarray = new();

                    bool mfaFound = true;
                    string errString = string.Empty;
                    while ((errString = res.StandardError.ReadLine()) != null)
                    {
                        var errResults = this.ParseErrors(errString);
                        if (!errResults.Success)
                        {
                            this.log.Error(errString, false);
                            this.log.Error(errResults.Message);
                            mfaFound = true;
                            return mfaFound;
                        }

                        errorarray.Add(errString);
                    }

                    this.PushPsErrorsToMainLog(errorarray);

                    return mfaFound;
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error("Error in TestMfa: " + ex.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error("Error in TestMfa: " + ex.Message);
                return false;
            }
        }
        public bool TestMfaVB365()
        {
            if (string.IsNullOrEmpty(CGlobals.REMOTEHOST))
                CGlobals.REMOTEHOST = "localhost";

            bool isRemote = CGlobals.REMOTEEXEC;

            if (isRemote)
            {
                // Remote VB365: use credential-based connection
                CredsHandler ch = new();
                var creds = ch.GetCreds();

                if (creds == null)
                {
                    CGlobals.Logger.Error("Credentials required for remote VB365 execution.");
                    return true; // true = MFA failure, stops collection
                }

                string base64Password = CredentialHelper.EncodePasswordToBase64(creds.Value.Password);
                // Username sits in a single-quoted PSCredential() argument; server is double-quoted.
                string escapedUser = CredentialHelper.EscapeForPowerShellSingleQuotes(creds.Value.Username);
                string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
                string argString = "Import-Module Veeam.Archiver.PowerShell -WarningAction Ignore; " +
                    $"$pw = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{base64Password}')); " +
                    "$secpw = ConvertTo-SecureString $pw -AsPlainText -Force; " +
                    $"$cred = New-Object System.Management.Automation.PSCredential('{escapedUser}', $secpw); " +
                    $"Connect-VBOServer -Server \"{escapedServer}\" -Credential $cred";

                CGlobals.Logger.Info("[VB365 MFA Check] Testing remote connection...", false);
                return this.ExecutePsScriptWithFailover(argString, useShellExecute: false,
                    createNoWindow: true, redirectStdErr: true);
            }
            else
            {
                // Local VB365: use existing Windows auth
                string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
                string argString = $"Connect-VBOServer -Server \"{escapedServer}\"";
                CGlobals.Logger.Info("[VB365 MFA Check] Testing local connection...", false);
                return this.ExecutePsScriptWithFailover(argString, useShellExecute: false,
                    createNoWindow: false, redirectStdErr: true);
            }
        }

        public bool RunVbrConfigCollect()
        {
            bool success = true;
            success = this.ExecutePsScript(this.VbrConfigStartInfo(), tolerateExitCodeIfComplete: true);


            // Skip NAS script during remote execution as it reads from local log files
            // that don't exist on the management machine
            if (success && !CGlobals.REMOTEEXEC)
            {
                success = this.ExecutePsScript(this.VbrNasStartInfo());
            }
            else if (CGlobals.REMOTEEXEC)
            {
                this.log.Info("[PS] Skipping NAS info collection - not supported for remote execution", false);
            }

            return success;
        }

        // Collection is "complete" if the manifest file (written as the final collection
        // step, immediately before "Collection complete" is logged) exists on disk; the
        // stdout marker is a fallback for callers that don't have a manifest path yet.
        // A real on-disk artifact is the signal here, not a fragile exit-code assumption -
        // this is what lets us tell "collection finished, teardown hiccuped" apart from
        // "collection actually failed".
        internal static bool VbrCollectionCompleted(string stdOut, string manifestPath)
        {
            if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
            {
                return true;
            }

            return !string.IsNullOrEmpty(stdOut) && stdOut.Contains("[Get-VBRConfig] Collection complete");
        }

        public bool ExecutePsScript(ProcessStartInfo startInfo, bool tolerateExitCodeIfComplete = false)
        {
            var res1 = new Process();
            res1.StartInfo = startInfo;
            res1.Start();

            this.log.Info("[PS] Script execution started. PID: " + res1.Id.ToString(), false);

            // Read both streams concurrently to avoid deadlock:
            // Sequential ReadToEnd() calls block if the process fills the stderr pipe buffer
            // while C# is still waiting on stdout (and vice versa).
            var stdOutTask = System.Threading.Tasks.Task.Run(() => res1.StandardOutput.ReadToEnd());
            var stdErrTask = System.Threading.Tasks.Task.Run(() => res1.StandardError.ReadToEnd());

            // Wait for process to complete (7 day timeout for large environments)
            bool exited = res1.WaitForExit(604800000);
            string stdOut = stdOutTask.GetAwaiter().GetResult();
            string stdErr = stdErrTask.GetAwaiter().GetResult();
            if (!exited)
            {
                this.log.Error("[PS] Script execution timeout after 7 days", false);
                try { res1.Kill(); } catch { }
                return false;
            }

            // Log stdout if present
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                this.log.Debug($"[PS][STDOUT] {stdOut}", false);
            }

            // Process stderr
            List<string> errorarray = new();
            bool failed = false;

            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                string[] errLines = stdErr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string errString in errLines)
                {
                    var errResults = this.ParseErrors(errString);
                    if (!errResults.Success)
                    {
                        this.log.Error(errString, false);
                        this.log.Error(errResults.Message);
                        failed = true;
                    }
                    errorarray.Add(errString);
                }
            }

            if (errorarray.Count > 0)
            {
                this.PushPsErrorsToMainLog(errorarray);
            }

            // Check exit code
            if (res1.ExitCode != 0)
            {
                string manifestPath = Path.Combine(CVariables.vbrDir, $"{CGlobals.REMOTEHOST}_CollectionManifest.csv");
                if (tolerateExitCodeIfComplete && VbrCollectionCompleted(stdOut, manifestPath))
                {
                    this.log.Info($"[PS] Script exited with code {res1.ExitCode} but collection completed " +
                        "(manifest present / 'Collection complete' logged). Proceeding to report generation.", false);
                }
                else
                {
                    this.log.Error($"[PS] Script failed with exit code: {res1.ExitCode}", false);
                    failed = true;
                }
            }

            this.log.Info(CMessages.PsVbrConfigProcIdDone, false);
            return !failed;
        }

        private void PushPsErrorsToMainLog(List<string> errors)
        {
            if (errors.Count > 0)
            {
                this.log.Error("PowerShell Errors: ");
                foreach (var e in errors)
                {
                    this.log.Error("\t" + e);
                }
            }
        }

        private PsErrorTypes ParseErrors(string errorLine)
        {
            if (errorLine.Contains("Unable to connect to the server with MFA-enabled user account"))
            {
                var message = "Unable to connect to VBR because the current account is MFA-enabled. Please run Veeam Health Check from Command Prompt or PowerShell using a non-MFA-enabled account, or provide alternate credentials in the app.";
                VeeamHealthCheck.Shared.CGlobals.UserFacingError = message;
                return new PsErrorTypes
                {
                    Success = false,
                    Message = message
                };
            }

            else return new PsErrorTypes { Success = true, Message = "Success" };
        }

        private ProcessStartInfo VbrConfigStartInfo()
        {
            this.log.Info(CMessages.PsVbrConfigStart, false);

            if (CGlobals.REMOTEHOST == string.Empty)
            {
                CGlobals.REMOTEHOST = "localhost";
            }

            bool needsCredentials = CGlobals.REMOTEEXEC;

            // Build argument string with BOTH VBRVersion and ReportInterval
            // Use Bypass (not Unrestricted) so PS never prompts for unsigned/internet-sourced
            // scripts - Unrestricted still prompts interactively which hangs a windowless process.
            string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
            string argString = $"-NoProfile -ExecutionPolicy Bypass -file \"{this.vbrConfigScript}\" " +
                               $"-VBRServer \"{escapedServer}\" " +
                               $"-VBRVersion \"{CGlobals.VBRMAJORVERSION}\" " +
                               $"-ReportInterval {CGlobals.ReportDays} ";

            if (CGlobals.RescanHosts)
            {
                argString += "-RescanHosts ";
            }

            if (CGlobals.REMOTEEXEC)
            {
                argString += "-RemoteExecution ";
            }

            // Add ReportPath parameter
            if (!string.IsNullOrEmpty(CVariables.vbrDir))
            {
                argString += $"-ReportPath \"{CVariables.vbrDir}\" ";
            }

            // Add LogPath parameter so collector logs follow the configured output root
            argString += $"-LogPath \"{Path.Combine(CVariables.unsafeDir, "Log")}\" ";
            // Add credentials if needed for remote execution
            string safeArgString = argString; // For logging without sensitive data
            if (needsCredentials)
            {
                CredsHandler ch = new();
                var creds = ch.GetCreds();
                if (creds != null)
                {
                    byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(creds.Value.Password);
                    string passwordBase64 = Convert.ToBase64String(passwordBytes);
                    string escapedUser = CredentialHelper.EscapeForPowerShellDoubleQuotes(creds.Value.Username);
                    argString += $"-User \"{escapedUser}\" -PasswordBase64 \"{passwordBase64}\" ";
                    safeArgString += $"-User \"{escapedUser}\" -PasswordBase64 \"****\" ";
                }
            }

            this.log.Debug(this.logStart + "PS ArgString = " + safeArgString, false);

            // Use same PowerShell version logic as other methods
            string exePath = null;
            if (!string.IsNullOrEmpty(this.pwshPath) && !(CGlobals.VBRMAJORVERSION < 13))
            {
                exePath = this.pwshPath;
            }
            else if (!string.IsNullOrEmpty(this.powershellPath))
            {
                exePath = this.powershellPath;
            }
            else
            {
                throw new InvalidOperationException("No PowerShell executable found on system.");
            }

            this.log.Debug(this.logStart + $"Using PowerShell executable: {exePath}", false);

            return new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = argString,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        private ProcessStartInfo VbrNasStartInfo()
        {
            this.log.Info(string.Empty);
            // Pass the VBR directory path which now includes server name and timestamp
            return this.ConfigStartInfo(this.nasScript, 0, CVariables.vbrDir);
        }

        private ProcessStartInfo ExportLogsStartInfo(string path, string server)
        {
            this.log.Info(CMessages.PsVbrConfigStart, false);
            return this.LogCollectionInfo(this.exportLogsScript, path, server);
        }

        private ProcessStartInfo DumpServersStartInfo()
        {
            this.log.Info("Starting dump servers script", false);
            return this.ServerDumpInfo(this.dumpServers);
        }

        public void RunServerDump()
        {
            ProcessStartInfo p = this.DumpServersStartInfo();
            var result = Process.Start(p);
            this.log.Info("Starting PowerShell Server Dump. Process ID: " + result.Id.ToString(), false);
            result.WaitForExit();
            this.log.Info("Powershell server dump complete.", false);
        }

        public void RunVbrLogCollect(string path, string server)
        {
            ProcessStartInfo p = this.ExportLogsStartInfo(path, server);

            // log.Debug(p., false);
            var res1 = Process.Start(p);
            this.log.Info(CMessages.PsVbrConfigProcId + res1.Id.ToString(), false);
            this.log.Info("\tPS Window is minimized by default. Progress indicators can be found there.", false);

            res1.WaitForExit();

            this.log.Info(CMessages.PsVbrConfigProcIdDone, false);
        }

        private ProcessStartInfo LogCollectionInfo(string scriptLocation, string path, string server)
        {
            // Quote and escape every value: scriptLocation/path may contain spaces, and
            // server (REMOTEHOST) is operator-controlled — unquoted it allows PowerShell
            // argument injection (aggravated by UseShellExecute=true). Code-review cd-13/cs-01.
            string escapedScript = CredentialHelper.EscapeForPowerShellDoubleQuotes(scriptLocation);
            string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(server);
            string escapedPath = CredentialHelper.EscapeForPowerShellDoubleQuotes(path);
            string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{escapedScript}\" -Server \"{escapedServer}\" -ReportPath \"{escapedPath}\"";

            if (CGlobals.DEBUG)
            {
                this.log.Debug(this.logStart + "PS ArgString = " + argString, false);
            }


            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = argString,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Minimized
            };
        }

        private ProcessStartInfo ServerDumpInfo(string scriptLocation)
        {
            string argString;
            string server = "localhost";
            if (CGlobals.REMOTEHOST == string.Empty)
                server = "localhost";
            else
            {
                server = CGlobals.REMOTEHOST;
            }

            // Escape both values; server (REMOTEHOST) is operator-controlled and was
            // previously unquoted/unescaped — PowerShell argument injection. Code-review cd-13/cs-01.
            string escapedScript = CredentialHelper.EscapeForPowerShellDoubleQuotes(scriptLocation);
            string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(server);
            argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{escapedScript}\" -Server \"{escapedServer}\"";

            if (CGlobals.DEBUG)
            {
                this.log.Debug(this.logStart + "PS ArgString = " + argString, false);
            }


            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = argString,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Minimized,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
        }

        private ProcessStartInfo ConfigStartInfo(string scriptLocation, int days, string path)
        {
            if (CGlobals.REMOTEHOST == string.Empty)
            {
                CGlobals.REMOTEHOST = "localhost";
            }

            // Determine if credentials are needed:
            // - Only needed for remote execution (REMOTEEXEC flag is set)
            // - Not needed for local VBR (IsVbr is true and REMOTEEXEC is false)
            bool needsCredentials = CGlobals.REMOTEEXEC;

            string argString;
            string safeArgString; // For logging without sensitive data
            string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
            if (days != 0)
            {
                argString =
                    $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer \"{escapedServer}\" -ReportInterval {CGlobals.ReportDays} ";
                safeArgString = argString;
                // Add ReportPath parameter if provided
                if (!string.IsNullOrEmpty(path))
                {
                    argString += $"-ReportPath \"{path}\" ";
                    safeArgString += $"-ReportPath \"{path}\" ";
                }
                if (needsCredentials)
                {
                    CredsHandler ch = new();
                    var creds = ch.GetCreds();
                    if (creds != null)
                    {
                        // Encode password in Base64 for secure transmission
                        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(creds.Value.Password);
                        string passwordBase64 = Convert.ToBase64String(passwordBytes);
                        string escapedUser = CredentialHelper.EscapeForPowerShellDoubleQuotes(creds.Value.Username);
                        argString += $"-User \"{escapedUser}\" -PasswordBase64 \"{passwordBase64}\" ";
                        safeArgString += $"-User \"{escapedUser}\" -PasswordBase64 \"****\" ";
                    }
                }
            }
            else
            {
                argString =
                    $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer \"{escapedServer}\" -VBRVersion \"{CGlobals.VBRMAJORVERSION}\" ";
                safeArgString = argString;
                // Add ReportPath parameter if provided
                if (!string.IsNullOrEmpty(path))
                {
                    argString += $"-ReportPath \"{path}\" ";
                    safeArgString += $"-ReportPath \"{path}\" ";
                }
                if (needsCredentials)
                {
                    CredsHandler ch = new();
                    var creds = ch.GetCreds();
                    if (creds != null)
                    {
                        // Encode password in Base64 for secure transmission
                        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(creds.Value.Password);
                        string passwordBase64 = Convert.ToBase64String(passwordBytes);
                        string escapedUser = CredentialHelper.EscapeForPowerShellDoubleQuotes(creds.Value.Username);
                        argString += $"-User \"{escapedUser}\" -PasswordBase64 \"{passwordBase64}\" ";
                        safeArgString += $"-User \"{escapedUser}\" -PasswordBase64 \"****\" ";
                    }
                }
            }

            this.log.Debug(this.logStart + "PS ArgString = " + safeArgString, false);

            // Use the same PowerShell version failover logic as ExecutePsScriptWithFailover
            // Prefer PowerShell 7, then 5, else throw
            string exePath = null;

            // if vbr version is v13 and pwsh exists, use pwsh, else use powershell
            if (!string.IsNullOrEmpty(this.pwshPath) && !(CGlobals.VBRMAJORVERSION < 13))
            {
                exePath = this.pwshPath;
            }

            else if (!string.IsNullOrEmpty(this.powershellPath))
            {
                exePath = this.powershellPath;
            }
            else
            {

                throw new InvalidOperationException("No PowerShell executable found on system.");
            }

            // log which powershell we're using as debug logging

            this.log.Debug(this.logStart + $"Using PowerShell executable: {exePath}", false);
            return new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = argString,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        public void InvokeVb365CollectEmbedded()
        {
            this.log.Info("[PS] Enter VB365 collection invoker...", false);

            string scriptContent = this.GetEmbeddedScript("VeeamHealthCheck.Functions.Collection.PSCollections.Scripts.Collect-VB365Data.ps1");

            if (string.IsNullOrEmpty(scriptContent))
            {
                this.log.Error("[PS] Failed to load embedded script.", false);
                return;
            }

            this.ExecuteEmbeddedScript(scriptContent);
        }

        private void ExecuteEmbeddedScript(string scriptContent)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(scriptContent)
                    .AddParameter("ReportingIntervalDays", CGlobals.ReportDays);

                this.log.Info("[PS] Starting VB365 Collection PowerShell process", false);
                try
                {
                    var results = ps.Invoke();
                }
                catch (Exception ex)
                {
                    this.log.Error("[PS] VB365 collection failed.", false);
                    this.log.Error(ex.Message, false);
                }

                if (ps.HadErrors)
                {
                    foreach (var error in ps.Streams.Error)
                    {
                        this.log.Error($"[PS] {error}", false);
                    }
                }
                else
                {
                    this.log.Info("[PS] VB365 collection complete!", false);
                }
            }
        }

        private string GetEmbeddedScript(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    this.log.Error($"[PS] Resource '{resourceName}' not found.", false);
                    return null;
                }

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public bool InvokeVb365Collect()
        {
            this.log.Info("[PS] Enter VB365 collection invoker...", false);
            var scriptFile = this.vb365Script;
            this.UnblockFile(scriptFile);

            // Build the argument string (with credentials, when needed) and a
            // separate masked variant for logging so the password never lands
            // in any log file. Mirrors the VBR pattern at VbrConfigStartInfo.
            string args = this.BuildVb365Arguments(out string safeArgs);

            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            this.log.Info("[PS] Starting VB365 Collection Powershell process", false);
            this.log.Info("[PS] [ARGS]: " + safeArgs, false);
            var result = Process.Start(startInfo);
            this.log.Info("[PS] Process started with ID: " + result.Id.ToString(), false);

            // Capture output for diagnostics: the collector's own INFO/WARNING/ERROR
            // logging goes to CollectorMain.log on disk, not stdout (Write-LogFile only
            // echoes to the console-only Information/Warning/Error streams, and only
            // when the script's own DebugInConsole setting is on) - so unlike the VBR
            // config collector, there is no in-process "collection complete" marker we
            // can observe from here. The real process exit code is the only reliable
            // signal available; capturing stdout/stderr at least surfaces PowerShell-
            // level failures (e.g. module load errors) that were previously silent.
            var stdOutTask = System.Threading.Tasks.Task.Run(() => result.StandardOutput.ReadToEnd());
            var stdErrTask = System.Threading.Tasks.Task.Run(() => result.StandardError.ReadToEnd());
            result.WaitForExit();
            string stdOut = stdOutTask.GetAwaiter().GetResult();
            string stdErr = stdErrTask.GetAwaiter().GetResult();

            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                this.log.Debug($"[PS][VB365][STDOUT] {stdOut}", false);
            }
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                this.log.Error($"[PS][VB365][STDERR] {stdErr}", false);
            }

            if (result.ExitCode != 0)
            {
                this.log.Error($"[PS] VB365 script failed with exit code: {result.ExitCode}", false);
                return false;
            }

            this.log.Info("[PS] VB365 collection complete!", false);
            return true;
        }

        /// <summary>
        /// Assembles the PowerShell argument string for the VB365 collection
        /// script. When VB365 collection is remote (REMOTEEXEC), the call
        /// fetches credentials via <see cref="CredsHandler"/> and appends
        /// <c>-Username "..." -PasswordBase64 "..."</c> matching the
        /// VBR pattern. The <paramref name="safeArgs"/> output replaces the
        /// password with <c>****</c> for safe logging.
        /// </summary>
        internal string BuildVb365Arguments(out string safeArgs)
        {
            string scriptFile = this.vb365Script;
            string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST);
            string serverArg = CGlobals.REMOTEEXEC ? $" -VBOServerFqdnOrIp \"{escapedServer}\"" : string.Empty;

            string baseArgs =
                $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptFile}\" " +
                $"-ReportingIntervalDays \"{CGlobals.ReportDays}\"{serverArg}";

            string args = baseArgs;
            safeArgs = baseArgs;

            // Credentials are only needed for remote VB365 collection; local
            // VB365 uses the current Windows session.
            bool needsCredentials = CGlobals.REMOTEEXEC;
            if (needsCredentials)
            {
                CredsHandler ch = new();
                var creds = ch.GetCreds();
                if (creds != null)
                {
                    string passwordBase64 = CredentialHelper.EncodePasswordToBase64(creds.Value.Password);
                    string escapedUser = CredentialHelper.EscapeForPowerShellDoubleQuotes(creds.Value.Username);
                    args += $" -Username \"{escapedUser}\" -PasswordBase64 \"{passwordBase64}\"";
                    safeArgs += $" -Username \"{escapedUser}\" -PasswordBase64 \"****\"";
                }
            }

            return args;
        }

        private void UnblockFile(string file)
        {
            try
            {
                FileUnblocker fu = new();
                fu.Unblock(file);
            }
            catch (Exception ex)
            {
                this.log.Warning("Script unblock failed. Manual unblocking of files may be required.\n\t");
                this.log.Warning(ex.Message);
            }
        }

        public class FileUnblocker
        {
            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool DeleteFile(string name);

            public bool Unblock(string fileName)
            {
                return DeleteFile(fileName + ":Zone.Identifier");
            }
        }
    }
}
