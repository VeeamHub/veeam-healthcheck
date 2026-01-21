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
        private readonly string vbrSessionScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VeeamSessionReport.ps1");
        private readonly string vbrSessionScriptVersion13 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VeeamSessionReportVersion13.ps1");
        private readonly string mfaTestScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Functions\Collection\PSCollections\Scripts\TestMfa.ps1");

        private readonly string nasScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-NasInfo.ps1");

        private readonly string exportLogsScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HotfixDetection\Collect-VBRLogs.ps1");
        private readonly string dumpServers = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HotfixDetection\DumpManagedServerToText.ps1");

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
                    string stdErr = process.StandardError.ReadToEnd();
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

            // RunVbrSessionCollection();

            return res;
        }

        public void TryUnblockFiles()
        {
            this.UnblockFile(this.vbrConfigScript);
            this.UnblockFile(this.vbrSessionScript);
            this.UnblockFile(this.nasScript);
            UnblockFile(vbrSessionScriptVersion13);
            UnblockFile(mfaTestScript);
            this.UnblockFile(this.exportLogsScript);
            this.UnblockFile(this.dumpServers);
            this.UnblockFile(this.vb365Script);
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

                // Properly escape the password
                string escapedPassword = CredentialHelper.EscapePasswordForPowerShell(creds.Value.Password);


                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    // Use single quotes for the password to avoid interpretation of special characters
                    Arguments = $"Import-Module Veeam.Backup.PowerShell; Connect-VBRServer -Server '{CGlobals.REMOTEHOST ?? "localhost"}' -User '{creds.Value.Username}' -Password '{escapedPassword}'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Log the command with masked password
                CGlobals.Logger.Info("[TestMfa] Arguments: " + startInfo.Arguments.Replace(escapedPassword, "****"));

                this.log.Info($"[TestMfa] Creating ProcessStartInfo for MFA test:");
                this.log.Info($"[TestMfa] FileName: {startInfo.FileName}");
                this.log.Info($"[TestMfa] Arguments: {startInfo.Arguments}");
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
                    string stdErr = res.StandardError.ReadToEnd();

                    this.log.Debug($"[TestMfa] STDOUT: {stdOut}");
                    this.log.Debug($"[TestMfa] STDERR: {stdErr}");

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
            if (CGlobals.REMOTEHOST == string.Empty)
            {
                CGlobals.REMOTEHOST = "localhost";
            }


            string argString = $"Connect-VBOServer -Server \"{CGlobals.REMOTEHOST}\"";
            CGlobals.Logger.Info("[MFA Check] args:\t" + argString, false);
            return this.ExecutePsScriptWithFailover(argString, useShellExecute: false, createNoWindow: false, redirectStdErr: true);
        }

        public bool RunVbrConfigCollect()
        {
            bool success = true;
            success = this.ExecutePsScript(this.VbrConfigStartInfo());
            if (success)
            {
                success = this.ExecutePsScript(this.VbrNasStartInfo());
            }

            success = this.ExecutePsScript(this.VbrSessionStartInfo());


            return success;
        }

        public bool ExecutePsScript(ProcessStartInfo startInfo)
        {
            var res1 = new Process();
            res1.StartInfo = startInfo;
            res1.Start();

            this.log.Info("[PS] Script execution started. PID: " + res1.Id.ToString(), false);

            // Read output streams asynchronously to prevent deadlocks
            string stdOut = res1.StandardOutput.ReadToEnd();
            string stdErr = res1.StandardError.ReadToEnd();

            // Wait for process to complete (with timeout)
            bool exited = res1.WaitForExit(300000); // 5 minute timeout
            if (!exited)
            {
                this.log.Error("[PS] Script execution timeout after 5 minutes", false);
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
                this.log.Error($"[PS] Script failed with exit code: {res1.ExitCode}", false);
                failed = true;
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
            string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{this.vbrConfigScript}\" " +
                               $"-VBRServer \"{CGlobals.REMOTEHOST}\" " +
                               $"-VBRVersion \"{CGlobals.VBRMAJORVERSION}\" " +
                               $"-ReportInterval {CGlobals.ReportDays} ";

            // Add ReportPath parameter
            if (!string.IsNullOrEmpty(CVariables.vbrDir))
            {
                argString += $"-ReportPath \"{CVariables.vbrDir}\" ";
            }

            // Add credentials if needed for remote execution
            if (needsCredentials)
            {
                CredsHandler ch = new();
                var creds = ch.GetCreds();
                if (creds != null)
                {
                    byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(creds.Value.Password);
                    string passwordBase64 = Convert.ToBase64String(passwordBytes);
                    argString += $"-User \"{creds.Value.Username}\" -PasswordBase64 \"{passwordBase64}\" ";
                }
            }

            this.log.Debug(this.logStart + "PS ArgString = " + argString, false);

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

        private ProcessStartInfo VbrSessionStartInfo()
        {
            // Pass the VBR directory path which now includes server name and timestamp
            if (CGlobals.VBRMAJORVERSION == 13)
            {
                return this.ConfigStartInfo(this.vbrSessionScriptVersion13, CGlobals.ReportDays, CVariables.vbrDir);
            }
            else
            {
                return this.ConfigStartInfo(this.vbrSessionScript, CGlobals.ReportDays, CVariables.vbrDir);
            }
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
            string argString;
            argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";

            // string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
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

            argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";

            // string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
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

        private void RunVbrSessionCollection()
        {
            this.log.Info("[PS][VBR Sessions] Enter Session Collection Invoker...", false);

            var startInfo2 = this.ConfigStartInfo(this.vbrSessionScript, CGlobals.ReportDays, string.Empty);

            this.log.Info("[PS][VBR Sessions] Starting Session Collection PowerShell Process...", false);

            var result = Process.Start(startInfo2);

            this.log.Info("[PS][VBR Sessions] Process started with ID: " + result.Id.ToString(), false);
            result.WaitForExit();

            this.log.Info("[PS][VBR Sessions] Session collection complete!", false);
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
            if (days != 0)
            {
                argString =
                    $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer \"{CGlobals.REMOTEHOST}\" -ReportInterval {CGlobals.ReportDays} ";
                // Add ReportPath parameter if provided
                if (!string.IsNullOrEmpty(path))
                {
                    argString += $"-ReportPath \"{path}\" ";
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
                        argString += $"-User \"{creds.Value.Username}\" -PasswordBase64 \"{passwordBase64}\" ";
                    }
                }
            }
            else
            {
                argString =
                    $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer \"{CGlobals.REMOTEHOST}\" -VBRVersion \"{CGlobals.VBRMAJORVERSION}\" ";
                // Add ReportPath parameter if provided
                if (!string.IsNullOrEmpty(path))
                {
                    argString += $"-ReportPath \"{path}\" ";
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
                        argString += $"-User \"{creds.Value.Username}\" -PasswordBase64 \"{passwordBase64}\" ";
                    }
                }
            }

            this.log.Debug(this.logStart + "PS ArgString = " + argString, false);

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

        public void InvokeVb365Collect()
        {
            this.log.Info("[PS] Enter VB365 collection invoker...", false);
            var scriptFile = this.vb365Script;
            this.UnblockFile(scriptFile);

            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptFile}\" -ReportingIntervalDays \"{CGlobals.ReportDays}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            this.log.Info("[PS] Starting VB365 Collection Powershell process", false);
            this.log.Info("[PS] [ARGS]: " + startInfo.Arguments, false);
            var result = Process.Start(startInfo);
            this.log.Info("[PS] Process started with ID: " + result.Id.ToString(), false);
            result.WaitForExit();
            this.log.Info("[PS] VB365 collection complete!", false);
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
