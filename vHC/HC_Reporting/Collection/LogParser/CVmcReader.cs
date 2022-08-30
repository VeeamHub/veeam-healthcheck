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
        public CVmcReader()
        {

        }
        public void PopulateVmc()
        {
            GetLogDir();
            ReadVmc();
        }
        
        private void GetLogDir()
        {
            DB.CRegReader reg = new();
            string regDir = reg.DefaultLogDir();
            LOGLOCATION = Path.Combine(regDir + CLogOptions.VMCLOG);
        }
        private void ReadVmc()
        {
            using(StreamReader sr = new StreamReader(LOGLOCATION))
            {
                string line = "";
                while((line = sr.ReadLine())!= null)
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
