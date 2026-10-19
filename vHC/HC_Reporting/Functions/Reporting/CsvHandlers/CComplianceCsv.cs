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
        [Name("Best Practice")]
        public string BestPractice { get; set; }

        [Name("Status")]
        public string Status { get; set; }

        [Name("RuleType")]
        [Optional]
        public string RuleType { get; set; }

        [Name("IsMapped")]
        [Optional]
        public bool? IsMapped { get; set; }

        [Name("LabelSource")]
        [Optional]
        public string LabelSource { get; set; }

        /// <summary>SDK rule Id GUID; CI triage input only. Never rendered in the report or JSON export.</summary>
        [Name("RuleId")]
        [Optional]
        public string RuleId { get; set; }
    }
}

public enum ComplianceStatus
{
    Passed,
    NotImplemented,
    UnableToDetect,
    Suppressed
}
