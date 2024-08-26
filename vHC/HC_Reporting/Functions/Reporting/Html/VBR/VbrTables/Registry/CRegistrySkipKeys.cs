using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Registry
{
    internal class CRegistrySkipKeys
    {
        public static string[] SkipKeys = {
            "AzureArchiveFreezingUnSupportedRegions",
            "HighestDetectedVMCVersion",
            "SqlSecuredPassword", 
            "SqlLogin", 
            "SqlServerName", 
            "SqlInstanceName",
            "SqlDatabaseName",
            "SqlLockInfo"

        };
    }
}
