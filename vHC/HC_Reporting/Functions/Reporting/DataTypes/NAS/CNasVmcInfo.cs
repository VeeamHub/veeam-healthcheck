using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.NAS
{
    public class CNasVmcInfo
    {
        public string FileProxy { get; set; }

        public string FileShareID { get; set; }

        public string FileShareType { get; set; }

        public string TotalFilesCount { get; set; }

        public string ProxyIDs { get; set; }

        public string AvgFoldersCountPerInc { get; set; }

        public string BackupIOControlLevel { get; set; }

        public string TotalFoldersCount { get; set; }

        public string AvgIncrementSize { get; set; }

        public string CacheRepository { get; set; }

        public string AvgFilesCountPerInc { get; set; }

        public string BackupMode { get; set; }

        public string TotalShareSize { get; set; }
    }
}
