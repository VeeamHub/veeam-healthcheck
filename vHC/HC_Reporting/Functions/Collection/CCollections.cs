// <copyright file="CCollections.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
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
        public bool SCRIPTSUCCESS;
        private readonly CLogger log = CGlobals.Logger;

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
            if (CGlobals.IsVbr)
            {
                this.GetCsvFileSizesToLog();
            }

            // GetCsvFileSizesToLog();

            CheckRecon();

            if (!CGlobals.RunSecReport && CGlobals.IsVbr)
            {
                this.PopulateWaits();
            }

            if (CGlobals.IsVbr && !CGlobals.REMOTEEXEC)
            {
                this.ExecVmcReader();
                this.GetRegistryDbInfo();
                if (CGlobals.DBTYPE != CGlobals.PgTypeName)
                {
                    this.ExecSqlQueries();
                }
            }
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
            CRegReader reg = new CRegReader();
            reg.GetDbInfo();

            if (CGlobals.REMOTEEXEC)
            {
                CGlobals.DEFAULTREGISTRYKEYS = reg.DefaultVbrKeysRemote();
            }
            else
            {
                CGlobals.DEFAULTREGISTRYKEYS = reg.DefaultVbrKeys();
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
                    if (CGlobals.IsVbr || CGlobals.REMOTEEXEC)
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

                            // if (CGlobals.IsVbr || CGlobals.REMOTEEXEC)
                            this.ExecVbrScripts(p);
                        }
                        else
                        {
                            this.WeighSuccessContinuation();
                        }
                    }

                    if (CGlobals.IsVb365)
                    {
                        CGlobals.Logger.Info("Checking VB365 MFA Access...", false);
                        if (!this.TestPsMfaVb365(p))
                        {
                            if (CGlobals.IsVb365)
                            {
                                this.ExecVb365Scripts(p);
                            }
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

            // WeighSuccessContinuation();
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
                MessageBox.Show(errorToShow, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Encode password as Base64 for safe transmission
                string base64Password = CredentialHelper.EncodePasswordToBase64(creds.Value.Password);

                // Build PowerShell arguments with Base64-encoded password
                // Use double quotes around Base64 string to avoid issues with special characters
                string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Server {CGlobals.REMOTEHOST} -Username \"{creds.Value.Username}\" -PasswordBase64 \"{base64Password}\"";

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
                string safeArgs = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Server {CGlobals.REMOTEHOST} -Username \"{creds.Value.Username}\" -PasswordBase64 \"****\"";
                CGlobals.Logger.Debug($"ProcessStartInfo Settings:\n  FileName: {processInfo.FileName}\n  Arguments: {safeArgs}\n  RedirectStandardOutput: {processInfo.RedirectStandardOutput}\n  RedirectStandardError: {processInfo.RedirectStandardError}\n  UseShellExecute: {processInfo.UseShellExecute}\n  CreateNoWindow: {processInfo.CreateNoWindow}");
                using var process = System.Diagnostics.Process.Start(processInfo);
                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                error = stdErr;
                if (!string.IsNullOrWhiteSpace(stdOut))
                {
                    output.Add(stdOut);
                }


                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    output.Add(stdErr);
                }


                result = process.ExitCode == 0;

                // Log result summary only - avoid logging full output which could contain sensitive data in error messages
                CGlobals.Logger.Debug($"MFA Test Result: ExitCode={process.ExitCode}, StdOutLength={stdOut?.Length ?? 0}, StdErrLength={stdErr?.Length ?? 0}");

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

                // Simple Connect-VBRServer without credentials
                string script = "Import-Module Veeam.Backup.PowerShell -WarningAction Ignore; Connect-VBRServer -Server localhost ";
                string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"";

                CGlobals.Logger.Debug($"Running local MFA check with Windows auth: {psExe} {args}");

                var processInfo = new ProcessStartInfo
                {
                    FileName = psExe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                // Log summary only - avoid logging full output which could contain sensitive data
                CGlobals.Logger.Debug($"[Local MFA Check] Output lengths - STDOUT: {stdOut?.Length ?? 0}, STDERR: {stdErr?.Length ?? 0}");

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

                bool result = process.ExitCode == 0;
                CGlobals.Logger.Info($"[Local MFA Check] Result: {(result ? "Success" : "Failed")} (ExitCode={process.ExitCode})", false);

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
            // CScripts scripts = new();
            return p.TestMfaVB365();
        }

        private void ExecVbrScripts(PSInvoker p)
        {
            // debug log evaluation of what to run
            CGlobals.Logger.Debug("DEBUG: Evaluating PS Script Execution Conditions:");
            CGlobals.Logger.Debug("IsVbr: " + CGlobals.IsVbr.ToString());
            CGlobals.Logger.Debug("REMOTEEXEC: " + CGlobals.REMOTEEXEC.ToString());

            if (CGlobals.IsVbr || CGlobals.REMOTEEXEC)
            {
                CGlobals.Logger.Info("Entering vbr ps invoker", false);
                this.SCRIPTSUCCESS = p.Invoke();
            }
        }

        private void ExecVbrConfigOnly(PSInvoker p)
        {
            CGlobals.Logger.Info("Entering vbr config collection");
            this.SCRIPTSUCCESS = p.RunVbrConfigCollect();
        }

        private void ExecVb365Scripts(PSInvoker p)
        {
            if (CGlobals.IsVb365)
            {
                CGlobals.Logger.Info("Entering vb365 ps invoker", false);

                // p.InvokeVb365CollectEmbedded();
                p.InvokeVb365Collect();
            }
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
