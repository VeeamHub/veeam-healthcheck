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
using System.Windows.Shapes;
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
        private readonly string _exportLogsScript = Environment.CurrentDirectory + @"\Tools\Scripts\Collect-VBRLogs.ps1";


        private CLogger log = CGlobals.Logger;
        private string logStart = "[PsInvoker]\t";
        public PSInvoker()
        {
        }
        public void Invoke()
        {
            TryUnblockFiles();

            //RunVbrVhcFunctionSetter();
            RunVbrConfigCollect();
            RunVbrSessionCollection();
        }
        private void TryUnblockFiles()
        {
            UnblockFile(_vbrConfigScript);
            UnblockFile(_vbrSessionScript);
        }
        public void RunVbrConfigCollect()
        {
            var res1 = Process.Start(VbrConfigStartInfo());
            log.Info(CMessages.PsVbrConfigProcId + res1.Id.ToString(), false);

            res1.WaitForExit();

            log.Info(CMessages.PsVbrConfigProcIdDone, false);
        }
        private ProcessStartInfo VbrConfigStartInfo()
        {
            log.Info(CMessages.PsVbrConfigStart, false);
            return ConfigStartInfo(_vbrConfigScript, 0, "");
        }

        private ProcessStartInfo ExportLogsStartInfo(string path)
        {
            log.Info(CMessages.PsVbrConfigStart, false);
            return LogCollectionInfo(_exportLogsScript, path);
        }
        public void RunVbrLogCollect(string path)
        {
            var res1 = Process.Start(ExportLogsStartInfo(path));
            log.Info(CMessages.PsVbrConfigProcId + res1.Id.ToString(), false);

            res1.WaitForExit();

            log.Info(CMessages.PsVbrConfigProcIdDone, false);
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
            string argString;
            if (days != 0)
                argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer localhost -ReportInterval {CGlobals.ReportDays} ";
            else
                argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -VBRServer localhost ";
            if (!String.IsNullOrEmpty(path))
            {
                argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
            }
            //log.Debug(logStart + "PS ArgString = " + argString, false);
            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = argString,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        private ProcessStartInfo LogCollectionInfo(string scriptLocation, string path)
        {
            string argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -ReportPath \"{path}\"";
            //log.Debug(logStart + "PS ArgString = " + argString, false);
            return new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = argString,
                UseShellExecute = true,
                CreateNoWindow = false
            };
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
            catch (Exception ex)
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
