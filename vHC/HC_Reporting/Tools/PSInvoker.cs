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

namespace VeeamHealthCheck
{
    class PSInvoker
    {
        private readonly string _sessionReport = "Get-VBRConfig.ps1";
        private readonly string _vb365Script = Environment.CurrentDirectory +  @"\Tools\Scripts\";
        public PSInvoker()
        {
        }
        public void Invoke(bool collectSessionData)
        {
            var ps1File = "Get-VBRConfig.ps1";
            UnblockFile(ps1File);
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{ps1File}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var res1 = Process.Start(startInfo);

            if (collectSessionData)
            {
                var ps1File2 = "Get-VeeamSessionReport.ps1";
                UnblockFile(ps1File2);
                var startInfo2 = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{ps1File2}\" -VBRServer localhost -windowstyle hidden",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var result = Process.Start(startInfo2);
                result.WaitForExit();
            }

            res1.WaitForExit();
            //shell.AddCommand("Get-VeeamSessionReport").AddParameter("VBRServer","localhost").AddParameter("ReportPath","C:\\temp\\vbrout");

            //var output = shell.Invoke();

        }
        public void InvokeVb365Collect()
        {
            var scriptFile = Vb365ScriptFile();
            UnblockFile(scriptFile);

            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var result = Process.Start(startInfo);
            result.WaitForExit();

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
