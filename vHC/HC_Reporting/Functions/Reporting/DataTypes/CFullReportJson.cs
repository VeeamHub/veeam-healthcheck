using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Analysis.DataModels;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    internal class CFullReportJson
    {
        // The vHC version that produced this report (CGlobals.VHCVERSION), stamped at export time.
        public string VhcVersion { get; set; }

        public System.Collections.Generic.List<VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups.OrphanedSupersededBackupRecord> OrphanedSupersededBackups { get; set; } = new();
        public bool OrphanedBackupsSweepEvaluated { get; set; } = false;

        // License data captured from CHtmlTables.LicTable
        public List<License> Licenses { get; set; } = new();

        // Compliance scan telemetry — populated by CComplianceTable from
        // _SecurityComplianceMeta.csv. Stable contract for VIP ingestion.
        public ComplianceScanMeta ComplianceScan { get; set; }

        // Generic sections for other HTML tables
        public Dictionary<string, HtmlSection> Sections { get; set; } = new();
    }
}
