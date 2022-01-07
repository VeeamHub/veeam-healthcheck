// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HC_Reporting
{
    class CDataExport
    {
        private readonly string _targetPath = @"C:\temp\vHC";
        public void OpenFolder()
        {
            if (Directory.Exists(_targetPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = _targetPath,
                    FileName = "explorer.exe",
                };

                Process.Start(startInfo);
            }
            else
            {
                MessageBox.Show(string.Format("{0} Directory does not exist!", _targetPath));
            }
        }

    }
}
