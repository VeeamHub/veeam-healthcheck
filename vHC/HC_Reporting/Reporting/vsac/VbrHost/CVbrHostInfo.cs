using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared.Logging;
using System.Management.Automation;
using Microsoft.Management.Infrastructure;
using Microsoft.PowerShell;
using System.Diagnostics;

namespace VeeamHealthCheck.Reporting.vsac.VbrHost
{
    internal class CVbrHostInfo
    {
        private CLogger log = CVsacGlobals.LOG;

        public CVbrHostInfo()
        {
            GetOsInfo();
            ConfirmVbrServer();
        }

        private void GetOsInfo()
        {
            var version = Environment.OSVersion.ToString();
            string[] segments = version.Split('.');
            bool isValid = ParseVersion(segments[2]);
            string line = "VBR Server is running on OS version ";
            if (isValid)
            {
                line = line + version + ", which is valid LTS system.";
                log.Info(line);
            }
            else
            {
                line = line + version + ", which is NOT a valid LTS system.";
                log.Warning(line);
            }
        }
        private void ConfirmVbrServer()
        {
            string hostName = System.Net.Dns.GetHostName();

            var hosts = RunPowerShellScript("get-vbrserver");
            string[] h = hosts.Split(" ");


        }
        private string RunPowerShellScript(string script)
        {
            var startInfo = psInfo(script);
            using var process = Process.Start(startInfo);
            using var reader = process.StandardOutput;
            using var errors = process.StandardError;
            process.EnableRaisingEvents = true;

            var result = reader.ReadToEnd();

            return result;

        }
        private ProcessStartInfo psInfo(string scriptFile)
        {
            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = $"{scriptFile}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        private bool ParseVersion(string version)
        {
            switch (version)
            {
                case "20348":
                    return true;
                case "17763":
                    return true;
                case "14393":
                    return true;
                default: return false;
            }
        }
    }
}
