// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Reflection;

//using System.Management.Automation;
using System.Runtime.InteropServices;
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
        private readonly string _vb365Script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VB365\Collect-VB365Data.ps1");

        private readonly string _vbrConfigScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VBRConfig.ps1");
        private readonly string _vbrSessionScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VeeamSessionReport.ps1");
        private readonly string _vbrSessionScriptVersion13 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-VeeamSessionReportVersion13.ps1");

        private readonly string _nasScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HealthCheck\VBR\Get-NasInfo.ps1");

        private readonly string _exportLogsScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HotfixDetection\Collect-VBRLogs.ps1");
        private readonly string _dumpServers = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\Scripts\HotfixDetection\DumpManagedServerToText.ps1");

        public static readonly string SERVERLISTFILE = "serverlist.txt";

        private readonly CLogger log = CGlobals.Logger;
        private readonly string logStart = "[PsInvoker]\t";
        private PowerShellVersion? _preferredVersion = null;
        private string _pwshPath = null;
        private string _powershellPath = null;

        // Remove duplicate constructor, keep only one with detection logic
        public PSInvoker()
        {
            DetectPowerShellVersions();
        }

        private void DetectPowerShellVersions()
        {
            // Try to find pwsh.exe (PowerShell 7)
            _pwshPath = FindExecutableInPath("pwsh.exe");
            // Try to find powershell.exe (PowerShell 5)
            _powershellPath = FindExecutableInPath("powershell.exe");

            if (!string.IsNullOrEmpty(_pwshPath))
                _preferredVersion = PowerShellVersion.PowerShell7;
            else if (!string.IsNullOrEmpty(_powershellPath))
                _preferredVersion = PowerShellVersion.PowerShell5;
            else
                _preferredVersion = null;
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
                        return exePath;
                }
                catch { }
            }
            // Return default path if not found in PATH
            if (exeName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase))
                return @"C:\Program Files\PowerShell\7\pwsh.exe";
            return null;
        }

        private string GetPowerShellExecutable(PowerShellVersion version)
        {
            return version == PowerShellVersion.PowerShell7 ? _pwshPath : _powershellPath;
        }

        private ProcessStartInfo CreatePsStartInfo(string arguments, bool useShellExecute, bool createNoWindow, bool redirectStdErr, PowerShellVersion version)
        {
            var exePath = GetPowerShellExecutable(version);
            if (string.IsNullOrEmpty(exePath))
                throw new InvalidOperationException($"PowerShell executable for version {version} not found.");
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
            if (_preferredVersion == null)
            {
                log.Error("No PowerShell executable found on system.", false);
                return false;
            }
            // Try PowerShell 7 first, then 5
            foreach (var version in new[] { PowerShellVersion.PowerShell7, PowerShellVersion.PowerShell5 })
            {
                var exePath = GetPowerShellExecutable(version);
                if (string.IsNullOrEmpty(exePath))
                {
                    // add debug log to say which powershell version was not found
                    log.Debug($"[PS] {version} not found on system, skipping...", false);

                    continue;
                }
                try
                {
                    var startInfo = CreatePsStartInfo(arguments, useShellExecute, createNoWindow, redirectStdErr, version);
                    //log.Debug($"[PS] StartInfo: {startInfo.FileName} {startInfo.Arguments}");
                    var process = new Process { StartInfo = startInfo };
                    process.Start();
                    log.Info($"[PS] Script execution started with {version}. PID: {process.Id}", false);
                    process.WaitForExit();

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(stdOut))
                        log.Debug($"[PS][STDOUT] {stdOut}", false);
                    if (!string.IsNullOrWhiteSpace(stdErr))
                        log.Debug($"[PS][STDERR] {stdErr}", false);
                    log.Debug("Exit Code: " + process.ExitCode);

                    // Optionally check exit code or errors here
                    if (process.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        log.Warning($"[PS] Script failed with {version}. Exit code: {process.ExitCode}", false);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[PS] Exception running script with {version}: {ex.Message}", false);
                }
                return false;
            }
            log.Error("[PS] Script failed with all available PowerShell versions. Exiting program", false);
            Environment.Exit(1);
            return false;
        }

        public bool Invoke()
        {
            bool res = true;
            TryUnblockFiles();

            //RunVbrVhcFunctionSetter();
            res = RunVbrConfigCollect();
            if (!res)
                return false;
            //RunVbrSessionCollection();
            return res;
        }
        public void TryUnblockFiles()
        {
            UnblockFile(_vbrConfigScript);
            UnblockFile(_vbrSessionScript);
            UnblockFile(_nasScript);
            UnblockFile(_exportLogsScript);
            UnblockFile(_dumpServers);
            UnblockFile(_vb365Script);
        }
        public bool TestMfa()
        {
            var res = new Process();
            if (CGlobals.REMOTEHOST == "")
                CGlobals.REMOTEHOST = "localhost";
            string argString = $"Connect-VBRServer -Server \"{CGlobals.REMOTEHOST}\"";
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = argString,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            log.Info($"[TestMfa] Creating ProcessStartInfo for MFA test:");
            log.Info($"[TestMfa] FileName: {startInfo.FileName}");
            log.Info($"[TestMfa] Arguments: {startInfo.Arguments}");
            log.Info($"[TestMfa] RedirectStandardOutput: {startInfo.RedirectStandardOutput}");
            log.Info($"[TestMfa] RedirectStandardError: {startInfo.RedirectStandardError}");
            log.Info($"[TestMfa] UseShellExecute: {startInfo.UseShellExecute}");
            log.Info($"[TestMfa] CreateNoWindow: {startInfo.CreateNoWindow}");
            log.Info("[TestMfa] Starting PowerShell process for MFA test...");
            try
            {
                res.StartInfo = startInfo;
                res.Start();
                log.Info($"[TestMfa] PowerShell process started. PID: {res.Id}");

                res.WaitForExit();
                log.Info($"[TestMfa] PowerShell process exited with code: {res.ExitCode}");

                string stdOut = res.StandardOutput.ReadToEnd();
                string stdErr = res.StandardError.ReadToEnd();

                log.Info($"[TestMfa] STDOUT: {stdOut}");
                log.Info($"[TestMfa] STDERR: {stdErr}");

                List<string> errorarray = new();

                bool mfaFound = true;
                string errString = "";
                while ((errString = res.StandardError.ReadLine()) != null)
                {
                    var errResults = ParseErrors(errString);
                    if (!errResults.Success)
                    {
                        log.Error(errString, false);
                        log.Error(errResults.Message);
                        mfaFound = true;
                        return mfaFound;

                    }
                    errorarray.Add(errString);
                }
                PushPsErrorsToMainLog(errorarray);

                return mfaFound;
            }
            catch (Exception ex)
            {
                log.Error($"[TestMfa] Exception during PowerShell execution: {ex.Message}");
            }
            return true;
        }



        public bool TestMfaVB365()
        {
            if (CGlobals.REMOTEHOST == "")
                CGlobals.REMOTEHOST = "localhost";
            string argString = $"Connect-VBOServer -Server \"{CGlobals.REMOTEHOST}\"";
            CGlobals.Logger.Info("[MFA Check] args:\t" + argString, false);
            return ExecutePsScriptWithFailover(argString, useShellExecute: false, createNoWindow: false, redirectStdErr: true);
        }
        public bool RunVbrConfigCollect()
        {
            bool success = true;
            success = ExecutePsScript(VbrConfigStartInfo());
            if (success)
                success = ExecutePsScript(VbrNasStartInfo());
            if (success)
                success = ExecutePsScript(VbrSessionStartInfo());
            return success;
        }
        public bool ExecutePsScript(ProcessStartInfo startInfo)
        {
            var res1 = new Process();
            res1.StartInfo = startInfo;
            res1.Start();

            log.Info("[PS] Script execution started. PID: " + res1.Id.ToString(), false);

            if (res1 != null && !res1.HasExited)
                res1.WaitForExit();
            List<string> errorarray = new();

            bool failed = false;
            string errString = "";
            while ((errString = res1.StandardError.ReadLine()) != null)
            {
                var errResults = ParseErrors(errString);
                if (!errResults.Success)
                {
                    log.Error(errString, false);
                    log.Error(errResults.Message);
                    failed = true;
                    //return false;

                }
                errorarray.Add(errString);
            }
            if (errorarray.Count > 0)
                PushPsErrorsToMainLog(errorarray);

            log.Info(CMessages.PsVbrConfigProcIdDone, false);
            if (failed)
                return false;
            else
                return true;
        }
        private void PushPsErrorsToMainLog(List<string> errors)
        {
            if (errors.Count > 0)
            {
                log.Error("PowerShell Errors: ");
                foreach (var e in errors)
                {
                    log.Error("\t" + e);
                }
            }

        }
        private PsErrorTypes ParseErrors(string errorLine)
        {
            if (errorLine.Contains("Unable to connect to the server with MFA-enabled user account"))
            {
                return new PsErrorTypes
                {
                    Success = false,
                    Message = "MFA Enabled, please execute the utility from a CMD or PS using a non-MFA enabled account."
                };
            }

            else return new PsErrorTypes { Success = true, Message = "Success" };
        }
        private ProcessStartInfo VbrConfigStartInfo()
        {
            log.Info(CMessages.PsVbrConfigStart, false);
            return ConfigStartInfo(_vbrConfigScript, 0, "");
        }
        private ProcessStartInfo VbrNasStartInfo()
        {
            log.Info("");
            return ConfigStartInfo(_nasScript, 0, "");
        }
        private ProcessStartInfo VbrSessionStartInfo()
        {
            if (CGlobals.VBRMAJORVERSION == 13)
            {
                return ConfigStartInfo(_vbrSessionScriptVersion13, CGlobals.ReportDays, "");
            }
            else
            {
                return ConfigStartInfo(_vbrSessionScript, CGlobals.ReportDays, "");
            }
        }

        private ProcessStartInfo ExportLogsStartInfo(string path, string server)
        {
            log.Info(CMessages.PsVbrConfigStart, false);
            return LogCollectionInfo(_exportLogsScript, path, server);
        }
        private ProcessStartInfo DumpServersStartInfo()
        {
            log.Info("Starting dump servers script", false);
            return ServerDumpInfo(_dumpServers);
        }
        public void RunServerDump()
        {
            ProcessStartInfo p = DumpServersStartInfo();
            var result = Process.Start(p);
            log.Info("Starting PowerShell Server Dump. Process ID: " + result.Id.ToString(), false);
            result.WaitForExit();
            log.Info("Powershell server dump complete.", false);
        }
        public void RunVbrLogCollect(string path, string server)
        {
            ProcessStartInfo p = ExportLogsStartInfo(path, server);
            //log.Debug(p., false);
            var res1 = Process.Start(p);
            log.Info(CMessages.PsVbrConfigProcId + res1.Id.ToString(), false);
            log.Info("\tPS Window is minimized by default. Progress indicators can be found there.", false);


            res1.WaitForExit();

            log.Info(CMessages.PsVbrConfigProcIdDone, false);
        }
        private ProcessStartInfo LogCollectionInfo(string scriptLocation, string path, string server)
        {
            string argString;
            argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";

            //string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
            if (CGlobals.DEBUG)
                log.Debug(logStart + "PS ArgString = " + argString, false);
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
            if (CGlobals.REMOTEHOST == "")
                server = "localhost";
            else
            {
                server = CGlobals.REMOTEHOST;
            }
            argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";

            //string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
            if (CGlobals.DEBUG)
                log.Debug(logStart + "PS ArgString = " + argString, false);
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
            log.Info("[PS][VBR Sessions] Enter Session Collection Invoker...", false);


            var startInfo2 = ConfigStartInfo(_vbrSessionScript, CGlobals.ReportDays, "");


            log.Info("[PS][VBR Sessions] Starting Session Collection PowerShell Process...", false);

            var result = Process.Start(startInfo2);

            log.Info("[PS][VBR Sessions] Process started with ID: " + result.Id.ToString(), false);
            result.WaitForExit();

            log.Info("[PS][VBR Sessions] Session collection complete!", false);
        }
        private ProcessStartInfo ConfigStartInfo(string scriptLocation, int days, string path)
        {
            
            if (CGlobals.REMOTEHOST == ""){
                CGlobals.REMOTEHOST = "localhost";
            }
            string argString;
            if (days != 0)
            {
                argString =
                    $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer \"{CGlobals.REMOTEHOST}\" -ReportInterval {CGlobals.ReportDays} ";
                if (CGlobals.REMOTEEXEC)
                {
                    CredsHandler ch = new();
                    var creds = ch.GetCreds();
                    argString += $"-User {creds.Value.Username} -Password {creds.Value.Password} ";
                }
            }
            else
            {
                argString =
                    $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer \"{CGlobals.REMOTEHOST}\" -VBRVersion \"{CGlobals.VBRMAJORVERSION}\" ";
                    if(CGlobals.REMOTEEXEC ){
                        CredsHandler ch = new();
                    var creds = ch.GetCreds();
                    argString += $"-User {creds.Value.Username} -Password {creds.Value.Password} ";
                    }
            }
            if (!string.IsNullOrEmpty(path))
            {
                argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
            }
            log.Debug(logStart + "PS ArgString = " + argString, false);

            // Use the same PowerShell version failover logic as ExecutePsScriptWithFailover
            // Prefer PowerShell 7, then 5, else throw
            string exePath = null;
            if (!string.IsNullOrEmpty(_pwshPath))
                exePath = _pwshPath;
            else if (!string.IsNullOrEmpty(_powershellPath))
                exePath = _powershellPath;
            else
                throw new InvalidOperationException("No PowerShell executable found on system.");
            // log which powershell we're using as debug logging
            log.Debug(logStart + $"Using PowerShell executable: {exePath}", false);
            return new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = argString,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
        }

        public void InvokeVb365CollectEmbedded()
        {
            log.Info("[PS] Enter VB365 collection invoker...", false);

            string scriptContent = GetEmbeddedScript("VeeamHealthCheck.Functions.Collection.PSCollections.Scripts.Collect-VB365Data.ps1");

            if (string.IsNullOrEmpty(scriptContent))
            {
                log.Error("[PS] Failed to load embedded script.", false);
                return;
            }

            ExecuteEmbeddedScript(scriptContent);
        }
        private void ExecuteEmbeddedScript(string scriptContent)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(scriptContent)
                    .AddParameter("ReportingIntervalDays", CGlobals.ReportDays);


                log.Info("[PS] Starting VB365 Collection PowerShell process", false);
                try
                {
                    var results = ps.Invoke();
                }
                catch (Exception ex)
                {
                    log.Error("[PS] VB365 collection failed.", false);
                    log.Error(ex.Message, false);
                }
                if (ps.HadErrors)
                {
                    foreach (var error in ps.Streams.Error)
                    {
                        log.Error($"[PS] {error}", false);
                    }
                }
                else
                {
                    log.Info("[PS] VB365 collection complete!", false);
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
                    log.Error($"[PS] Resource '{resourceName}' not found.", false);
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
            log.Info("[PS] Enter VB365 collection invoker...", false);
            var scriptFile = _vb365Script;
            UnblockFile(scriptFile);

            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptFile}\" -ReportingIntervalDays \"{CGlobals.ReportDays}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            log.Info("[PS] Starting VB365 Collection Powershell process", false);
            log.Info("[PS] [ARGS]: " + startInfo.Arguments, false);
            var result = Process.Start(startInfo);
            log.Info("[PS] Process started with ID: " + result.Id.ToString(), false);
            result.WaitForExit();
            log.Info("[PS] VB365 collection complete!", false);

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
                log.Warning("Script unblock failed. Manual unblocking of files may be required.\n\t");
                log.Warning(ex.Message);
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
