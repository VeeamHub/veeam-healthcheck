using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CComplianceCsv
    {
        [Index(0)]
        public string BestPractice { get; set; }
        [Index(1)]
        public String Status { get; set; }

    }

}
public enum ComplianceStatus
{
    Passed,
    NotImplemented,
    UnableToDetect,
    Suppressed
}
