using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.NAS
{
    public class CObjectShareVmcInfo
    {
        [Index(0)]
        public string ParentObjectStorageID { get; set; }

        [Index(1)]
        public string AvgObjectsCountPerInc { get; set; }

        [Index(2)]
        public string ObjectStorageBucketID { get; set; }

        [Index(3)]
        public string TotalObjectStorageSize { get; set; }

        [Index(4)]
        public string TotalObjectsCount { get; set; }

        [Index(5)]
        public string AvgIncrementSize { get; set; }
    }
}
