// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
//using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    class PSInvoker
    {
        private readonly string _sessionReport = "Get-VBRConfig.ps1";
        private readonly string _vb365Script = Environment.CurrentDirectory + @"\Tools\Scripts\Collect-VB365Data.ps1";
        private readonly string _vbrConfigScript = Environment.CurrentDirectory + @"\Tools\Scripts\Get-VBRConfig.ps1";
        private readonly string _vbrSessionScript = Environment.CurrentDirectory + @"\Tools\Scripts\Get-VeeamSessionReport.ps1";

        private CLogger log = CGlobals.Logger;
        public PSInvoker()
        {
        }
        public void Invoke()
        {
            TryUnblockFiles();
            
            RunVbrConfigCollect();
            RunVbrSessionCollection();
        }
        private void TryUnblockFiles()
        {
            UnblockFile(_vbrConfigScript);
            UnblockFile(_vbrSessionScript);
        }
        private void RunVbrConfigCollect()
        {
            var res1 = Process.Start(VbrConfigStartInfo());
            log.Info(CMessages.PsVbrConfigProcId + res1.Id.ToString());
            
            res1.WaitForExit();
            
            log.Info(CMessages.PsVbrConfigProcIdDone);
        }
        private ProcessStartInfo VbrConfigStartInfo()
        {
            log.Info(CMessages.PsVbrConfigStart);
            return ConfigStartInfo(_vbrConfigScript, 0);
        }
        private void RunVbrSessionCollection()
        {
            log.Info("[PS][VBR Sessions] Enter Session Collection Invoker...");


            var startInfo2 = ConfigStartInfo(_vbrSessionScript, CGlobals.ReportDays);
                
                
            log.Info("[PS][VBR Sessions] Starting Session Collection PowerShell Process...");

            var result = Process.Start(startInfo2);
            
            log.Info("[PS][VBR Sessions] Process started with ID: " + result.Id.ToString());
            result.WaitForExit();
            
            log.Info("[PS][VBR Sessions] Session collection complete!");
        }
        private ProcessStartInfo ConfigStartInfo(string scriptLocation, int days)
        {
            string argString;
            if (days != 0)
                argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer localhost -ReportInterval {CGlobals.ReportDays} ";
            else
                argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer localhost ";
            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = argString,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        public void InvokeVb365Collect()
        {
            log.Info("[PS] Enter VB365 collection invoker...");
            var scriptFile = _vb365Script;
            UnblockFile(scriptFile);

            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptFile}\" -ReportingIntervalDays \"{CGlobals.ReportDays}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            log.Info("[PS] Starting VB365 Collection Powershell process");
            log.Info("[PS] [ARGS]: " + startInfo.Arguments);
            var result = Process.Start(startInfo);
            log.Info("[PS] Process started with ID: " + result.Id.ToString());
            result.WaitForExit();
            log.Info("[PS] VB365 collection complete!");

        }
        private ProcessStartInfo psInfo(string scriptFile)
        {
            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        private string Vb365ScriptFile()
        {
            string[] script = Directory.GetFiles(_vb365Script);
            return script[0];
        }
        private void UnblockFile(string file)
        {
            try
            {
                FileUnblocker fu = new();
                fu.Unblock(file);
            }
            catch(Exception ex)
            {
                log.Warning("Script unblock failed. Manual unblocking of files may be required.\n\t");
                log.Warning(ex.Message);
            }
            
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
