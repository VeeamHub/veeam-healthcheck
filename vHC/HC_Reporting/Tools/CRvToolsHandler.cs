// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck
{
    class CRvToolsHandler
    {
        private readonly string _installPath = @"C:\Program Files (x86)\Robware\RVTools";
        private bool _isInstalled;
        public CRvToolsHandler()
        {

        }

        public void CheckInstall()
        {
            if (!Directory.Exists(_installPath))
            {
                try
                {
                    Install();
                }
                catch(Exception e) { }
            }
            else
            {
                try
                {
                    ExecTools();//
                }
                catch(Exception e) { }
            }
        }

        private void Install()
        {
            Process p = new();
            p.StartInfo.FileName = "msiexe.exe";
            p.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            p.StartInfo.Arguments = " /quiet /i \\RVTools4.1.4.msi ADDLOCAL=test";
            p.StartInfo.Verb = "runas";
            p.Start();
            p.WaitForExit(60000);

            //ProcessStartInfo si = new ProcessStartInfo
            //{
                
            //    FileName = Environment.CurrentDirectory + "\\RVTools4.1.4.msi",
            //};
            //Process.Start(si);
        }

        private void ExecTools()
        {
            ProcessStartInfo pi = new ProcessStartInfo
            {
                FileName = "RVTools.exe",
            };
            Process.Start(pi);
        }

    }
}
