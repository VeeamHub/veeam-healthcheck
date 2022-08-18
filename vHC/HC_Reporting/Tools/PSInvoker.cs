// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    class PSInvoker
    {
        private readonly string _sessionReport = "Get-VBRConfig.ps1";
        private readonly string _vb365Script = Environment.CurrentDirectory + @"\Tools\Scripts\Collect-VB365Data.ps1";
        private CLogger log = VhcGui.log;
        public PSInvoker()
        {
        }
        public void Invoke(bool collectSessionData)
        {
            log.Info("Enter Config Collection Invoker...");
            var ps1File = Environment.CurrentDirectory + @"\Tools\Scripts\Get-VBRConfig.ps1";
            //log.Info("Checking file block..");
            UnblockFile(ps1File);
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{ps1File}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            log.Info("Starting PowerShell Process...");
            var res1 = Process.Start(startInfo);
            log.Info("Process started with ID: " +  res1.Id.ToString());

            if (collectSessionData)
            {
                log.Info("Enter Session Collection Invoker...");
                var ps1File2 = Environment.CurrentDirectory + @"\Tools\Scripts\Get-VeeamSessionReport.ps1";
                UnblockFile(ps1File2);
                var startInfo2 = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{ps1File2}\" -VBRServer localhost -windowstyle hidden",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                log.Info("Starting Session Collection PowerShell Process...");
                var result = Process.Start(startInfo2);
                log.Info("Process started with ID: " + result.Id.ToString());
                result.WaitForExit();
                log.Info("Session collection complete!");
            }

            res1.WaitForExit();
            log.Info("Config collection complete!");
            //shell.AddCommand("Get-VeeamSessionReport").AddParameter("VBRServer","localhost").AddParameter("ReportPath","C:\\temp\\vbrout");

            //var output = shell.Invoke();

        }
        public void InvokeVb365Collect()
        {
            log.Info("Enter VB365 collection invoker...");
            var scriptFile = _vb365Script;
            UnblockFile(scriptFile);

            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            log.Info("Starting VB365 Collection Powershell process");
            var result = Process.Start(startInfo);
            log.Info("Process started with ID: " + result.Id.ToString());
            result.WaitForExit();
            log.Info("VB365 collection complete!");

        }
        private ProcessStartInfo psInfo(string scriptFile)
        {
            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"",
                UseShellExecute=false,
                CreateNoWindow=true
            };
        }
        private string Vb365ScriptFile()
        {
            string[] script = Directory.GetFiles(_vb365Script);
            return script[0];
        }
        private void UnblockFile(string file)
        {
            FileUnblocker fu = new();
            fu.Unblock(file);
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
