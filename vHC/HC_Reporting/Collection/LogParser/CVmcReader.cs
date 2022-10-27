using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Collection.LogParser
{
    class CVmcReader
    {
        private string LOGLOCATION;
        public string INSTALLID;

        private string _mode;
        private string _vb365Logs = @"C:\ProgramData\Veeam\Backup365\Logs\";

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
                VhcGui.log.Error(e.Message);
            }
        }

        private void GetLogDir()
        {
            if(_mode == "vbr")
            {
                DB.CRegReader reg = new();
                string regDir = reg.DefaultLogDir();
                LOGLOCATION = Path.Combine(regDir + CLogOptions.VMCLOG);
            }
            else if(_mode == "vb365")
            {
                string[] filesList = Directory.GetFiles(_vb365Logs);
                List<FileInfo> fileInfoList = new();
                foreach(var f in filesList)
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
                string line = "";
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(CLogOptions.installIdLine))
                    {
                        ParseInstallId(line);
                    }
                }
            }
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
