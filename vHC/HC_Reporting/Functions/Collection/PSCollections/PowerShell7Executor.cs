using DocumentFormat.OpenXml.Math;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using VeeamHealthCheck.Shared; // For CGlobals

namespace VeeamHealthCheck.Functions.Collection.PSCollections
{
    public class Ps7Executor
    {
        // Embed your .ps1 scripts as strings. Example: a simple Veeam health check script.
        private const string EmbeddedScript = @"
param([string]$VeeamServer)
Write-Output ""Checking Veeam on $VeeamServer""
# Your actual PS code here, e.g., Import-Module VeeamPSSnapin; Get-VBRServer
";

        private readonly string vbrConfigScript = Environment.CurrentDirectory + @"\Tools\Scripts\HealthCheck\VBR\Get-VBRConfig.ps1";

        /// Checks if the Veeam.Backup.PowerShell module is available in PS7.

        public  bool IsModuleInstalled(string moduleName)
        {
            string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
            if (!File.Exists(pwshPath))
            {

                throw new FileNotFoundException("PowerShell 7 not found at: " + pwshPath);
            }


            string args = $"-NoProfile -Command \"if (Get-Module -ListAvailable -Name '{moduleName}') {{ Write-Host '0' }} else {{ Write-Host '1' }}\"";

            var psi = new ProcessStartInfo
            {
                FileName = pwshPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process.WaitForExit();
            return process.ExitCode == 0;
        }

        public  int RunScript(string scriptPath, string arguments = "")
        {
            // Find pwsh.exe (PowerShell 7)
            string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
            if (!System.IO.File.Exists(pwshPath))
            {

                throw new FileNotFoundException("PowerShell 7 not found at: " + pwshPath);
            }

            // Build the command line

            string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{scriptPath}\" ";

            var psi = new ProcessStartInfo
            {
                FileName = pwshPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Optionally, handle output/error here
            System.Console.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
            {
                System.Console.Error.WriteLine(error);
            }


            return process.ExitCode;
        }

        public (bool Success, List<string> Output, string Error) ExecuteScript(string scriptName, Dictionary<string, object> parameters = null)
        {
            var output = new List<string>();
            string errorMsg = string.Empty;
            bool success = false;

            try
            {
                // var (isAvailable, modulePath, moduleError) = CheckVeeamModule();
                // if (!isAvailable)
                // {
                //    return (false, output, $"Cannot execute script: {moduleError}");
                // }
                using var runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();
                using var ps = PowerShell.Create();
                ps.Runspace = runspace;

                ps.AddScript(scriptName);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        ps.AddParameter(param.Key, param.Value);
                    }
                }

                Collection<PSObject> results = ps.Invoke();

                var sb = new StringBuilder();
                foreach (var result in results)
                {
                    sb.AppendLine(result.ToString());
                }

                output.AddRange(sb.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

                if (ps.HadErrors)
                {
                    sb.Clear();
                    foreach (var error in ps.Streams.Error)
                    {
                        sb.AppendLine(error.ToString());
                    }

                    errorMsg = sb.ToString();
                }
                else
                {
                    success = true;
                }
            }
            catch (Exception ex)
            {
                errorMsg = $"Execution failed: {ex.Message}";
            }

            return (success, output, errorMsg);
        }

        public void LogPowerShellVersion()
        {
            string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
            if (!File.Exists(pwshPath))
            {
                CGlobals.Logger.Debug("PowerShell 7 not found at: " + pwshPath, false);
                return;
            }

            string args = "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\"";

            var psi = new ProcessStartInfo
            {
                FileName = pwshPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            CGlobals.Logger.Debug($"[PowerShell Version Log] Output: {output}", false);
            if (!string.IsNullOrWhiteSpace(error))
            {

                CGlobals.Logger.Debug($"[PowerShell Version Log] Error: {error}", false);
            }
        }
    }
}