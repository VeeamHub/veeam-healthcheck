// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CVmcReader
    {
        private string LOGLOCATION;
        public string INSTALLID;

        private readonly string mode;
        private readonly string vb365Logs = @"C:\ProgramData\Veeam\Backup365\Logs\";

        private DateTime DbLineDate;

        public CVmcReader(string mode)
        {
            this.mode = mode;
        }

        public void PopulateVmc()
        {
            try
            {
                this.GetLogDir();
                if (!string.IsNullOrEmpty(this.LOGLOCATION))
                {
                    this.ReadVmc();
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error(e.Message);
            }
        }

        private void GetLogDir()
        {
            if (this.mode == "vbr")
            {
                CRegReader reg = new();
                string regDir = reg.DefaultLogDir();
                this.LOGLOCATION = Path.Combine(regDir + CLogOptions.VMCLOG);
            }
            else if (this.mode == "vb365")
            {
                string[] filesList = Directory.GetFiles(this.vb365Logs);
                string match = CVmcLogFileSelector.SelectVmcLogFile(filesList);
                if (match == null)
                {
                    CGlobals.Logger.Warning($"[VMC reader] No VMC.log file found under '{this.vb365Logs}' - skipping VB365 install ID lookup.");
                    return;
                }

                this.LOGLOCATION = match;
            }
        }

        private void ReadVmc()
        {
            using (StreamReader sr = new StreamReader(this.LOGLOCATION))
            {
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(CLogOptions.installIdLine))
                    {
                        this.ParseInstallId(line);
                    }
                    else if (line.Contains("[SQL Server version]"))
                    {
                        this.ParseConfigDbInfo(line);
                    }
                }
            }
        }

        private void ParseConfigDbInfo(string line)
        {
            DateTime dbLineDate = this.ParseLineDate(line);
            if ( dbLineDate.Ticks - this.DbLineDate.Ticks == 0)
            {
                this.DbLineDate = this.ParseLineDate(line);
            }
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
            this.INSTALLID = id[1];
        }

        private void TrimLogLine(string line)
        {
            string newLine = line.Substring(40);
        }
    }
}
