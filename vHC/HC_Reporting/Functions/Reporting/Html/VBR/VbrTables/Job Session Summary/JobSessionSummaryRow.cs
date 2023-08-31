using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    class JobSessionSummaryRow
    {
        //public JobSessionSummaryRow()
        //{

        //}
        public string Name { get; set; }
        public int Items { get; set; }
        public TimeSpan  MinTime { get; set; }
        public TimeSpan MaxTime { get; set; }
        public TimeSpan AvgTime { get; set; }
        public int TotalSessions { get; set; }
        public int Fails { get; set; }
        public int Retries { get; set; }
        public int SuccessRate { get; set; }
        public double AvgBackupSizeTb { get; set; }
        public double MaxBackupSizeTb { get; set; }
        public double AvgDataSizeTb { get; set; }
        public double MaxDataSizeTb { get; set; }
        public int AvgChangeRate { get; set; }
        public int WaitForResourceCount { get; set; }
        public TimeSpan MaxWait { get; set; }
        public TimeSpan AvgWait { get; set; }
        public string JobType { get; set; }
    }
}
