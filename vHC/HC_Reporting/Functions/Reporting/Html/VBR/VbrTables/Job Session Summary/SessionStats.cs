using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary
{
    internal class SessionStats
    {
        public SessionStats()
        {
            
        }

        public int SessionCount { get; set; }

        public int FailCounts { get; set; }

        public int RetryCounts { get; set; }

        public List<TimeSpan> JobDuration { get; set; } = new List<TimeSpan>();

        public List<string> VmNames { get; set; } = new List<string>();

        public List<double> DataSize { get; set; } = new List<double>();

        public List<double> BackupSize { get; set; } = new List<double>();

        public string JobType { get; set; }

    }
}
