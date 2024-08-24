using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.NAS
{
    public class CNasVmcInfo
    {
        [Index(0)]
        public string FileProxy { get; set; }
        [Index(1)]
        public string NFSVersion { get; set; }
        [Index(2)]
        public string FileShareID { get; set; }
        [Index(3)]
        public string FileShareType { get; set; }
        [Index(4)]
        public string TotalFilesCount { get; set; }
        [Index(5)]
        public string ProxyIDs { get; set; }
        [Index(6)]
        public string AvgFoldersCountPerInc { get; set; }
        [Index(7)]
        public string BackupIOControlLevel { get; set; }
        [Index(8)]
        public string TotalFoldersCount { get; set; }
        [Index(9)]
        public string AvgIncrementSize { get; set; }
        [Index(10)]
        public string CacheRepository { get; set; }
        [Index(11)]
        public string AvgFilesCountPerInc { get; set; }
        [Index(12)]
        public string BackupMode { get; set; }
        [Index(13)]
        public string TotalShareSize { get; set; }
    }
}
