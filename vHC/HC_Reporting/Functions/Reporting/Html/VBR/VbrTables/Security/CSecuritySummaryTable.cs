using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security
{
    public class CSecuritySummaryTable
    {
        public bool ImmutabilityEnabled { get;set;}

        public bool TrafficEncrptionEnabled { get; set; }

        public bool BackupFileEncrptionEnabled { get; set; }

        public bool ConfigBackupEncrptionEnabled { get; set; }

        public bool MFAEnabled { get; set; }

        public bool IsConsoleInstalled { get; set; }

        public CSecuritySummaryTable()
        {
            
        }
    }
}
