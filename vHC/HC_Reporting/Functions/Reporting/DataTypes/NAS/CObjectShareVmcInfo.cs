using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.NAS
{
    internal class CObjectShareVmcInfo
    {
        // Make members for each of these properties: ParentObjectStorageID	AvgObjectsCountPerInc	ObjectStorageBucketID	TotalObjectStorageSize	TotalObjectsCount	AvgIncrementSize

        public string ParentObjectStorageID { get; set; }
        public string AvgObjectsCountPerInc { get; set; }
        public string ObjectStorageBucketID { get; set; }
        public string TotalObjectStorageSize { get; set; }
        public string TotalObjectsCount { get; set; }
        public string AvgIncrementSize { get; set; }

    }
}
