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
        public PSInvoker()
        {
            //PowerShell shell = PowerShell.Create();
            //shell.AddCommand("Set-ExecutionPolicy").AddArgument("Unrestricted").AddParameter("Force");
            ////shell.AddParameter("-ExecutionPolicy RemoteSigned -Scope LocalMachine");
        
            //shell.Invoke();
        }

        public void Invoke2(string backupServer)
        {
            if (String.IsNullOrEmpty(backupServer))
            {
                //backupServer = Microsoft.VisualBasic.Interaction.InputBox("Prompt", "Title", "Default", 0, 0);

            }
            //string backupServer = "";
            var ps1File = "Get-VBRConfig - Copy.ps1";
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted -file \"{ps1File}\"",
                //Arguments = $"-NoProfile -file \"{ps1File}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            //Process.Start(startInfo);
            var result = Process.Start(startInfo);
        }
        public void Invoke(bool collectSessionData)
        {
            string s = "";
            //Invoke2(s);

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
