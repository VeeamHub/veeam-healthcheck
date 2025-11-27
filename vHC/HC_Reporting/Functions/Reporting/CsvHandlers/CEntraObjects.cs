using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CEntraTenant
    {
        // tenantName	CacheRepoName
        [Index(0)]
        public string TenantName { get; set; }

        [Index(1)]
        public string CacheRepoName { get; set; }
    }

    public class CEntraLogJobs
    {
        // Name	Tenant	shortTermRetType	ShortTermRepo	ShortTermRepoRetention	CopyModeEnabled	SecondaryTarget
        [Index(0)]
        public string Name { get; set; }

        [Index(1)]
        public string Tenant { get; set; }

        [Index(2)]
        public string ShortTermRetType { get; set; }

        [Index(3)]
        public string ShortTermRepo { get; set; }

        [Index(4)]
        public int ShortTermRepoRetention { get; set; }

        [Index(5)]
        public bool CopyModeEnabled { get; set; }

        [Index(6)]
        public string SecondaryTarget { get; set; }
    }

    public class CEntraTenantJobs
    {
        // create a list of malware objects from this: ObjectId	ObjectName	DetectedDateTime	Severity	Types	Platform	ObjectHostName
        [Index(0)]
        public string Name { get; set; }

        [Index(1)]
        public int RetentionPolicy { get; set; }
    }
}
