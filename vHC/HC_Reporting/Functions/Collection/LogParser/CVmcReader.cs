// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CVmcReader
    {
        private string LOGLOCATION;
        public string INSTALLID;

        private string _mode;
        private string _vb365Logs = @"C:\ProgramData\Veeam\Backup365\Logs\";

        private DateTime _DbLineDate;

        public CVmcReader(string mode)
        {
            _mode = mode;
        }

        public void PopulateVmc()
        {
            GetLogDir();
            try
            {
                ReadVmc();

            }
            catch (Exception e)
            {
                CGlobals.Logger.Error(e.Message);
            }
        }

        private void GetLogDir()
        {
            if (_mode == "vbr")
            {
                CRegReader reg = new();
                string regDir = reg.DefaultLogDir();
                LOGLOCATION = Path.Combine(regDir + CLogOptions.VMCLOG);
            }
            else if (_mode == "vb365")
            {
                string[] filesList = Directory.GetFiles(_vb365Logs);
                List<FileInfo> fileInfoList = new();
                foreach (var f in filesList)
                {
                    if (f.Contains("VMC.log"))
                    {
                        FileInfo fileInfo = new FileInfo(f);
                        fileInfoList.Add(fileInfo);

                    }
                }
                fileInfoList.OrderBy(x => x.Name);
                string fileName = fileInfoList.FirstOrDefault().Name;
                LOGLOCATION = Path.Combine(_vb365Logs + fileName);
            }

        }

        private void ReadVmc()
        {
            using (StreamReader sr = new StreamReader(LOGLOCATION))
            {
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(CLogOptions.installIdLine))
                    {
                        ParseInstallId(line);
                    }
                    else if (line.Contains("[SQL Server version]"))
                    {
                        ParseConfigDbInfo(line);
                    }
                }
            }
        }

        private void ParseConfigDbInfo(string line)
        {
            DateTime dbLineDate = ParseLineDate(line);
            if ( dbLineDate.Ticks - _DbLineDate.Ticks == 0)
                _DbLineDate = ParseLineDate(line);

        }

        private DateTime ParseLineDate(string line)
        {
            string newLine = line.Substring(1, 25);
            DateTime.TryParse(newLine, out DateTime dt);
            return dt;

        }

        private void ParseInstallId(string line)
        {
            string[] id = line.Substring(40).Split();
            INSTALLID = id[1];
        }

        private void TrimLogLine(string line)
        {
            string newLine = line.Substring(40);
        }
    }
}
