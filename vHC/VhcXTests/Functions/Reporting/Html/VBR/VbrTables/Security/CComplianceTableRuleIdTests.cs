// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.IO;
using System.Linq;
using System.Text.Json;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.Security
{
    /// <summary>
    /// The RuleId column on _SecurityCompliance.csv is CI-triage input only. These tests prove the
    /// customer-visible HTML and JSON are unchanged whether the column is absent (older CSV),
    /// blank, or populated.
    /// </summary>
    [Trait("Category", "Scrubbing")]
    [Collection("GlobalState")]
    public class CComplianceTableRuleIdTests : VbrTableScrubTestBase
    {
        private const string GuidA = "5b1f7d7e-3c1a-4c52-9a4e-0d6b7c1e9a11";
        private const string GuidB = "c0a8e2f4-77d3-4f0b-8e5a-2b9f4d6a1c22";

        private const string OldCsv =
            "\"Best Practice\",\"Status\",\"RuleType\",\"IsMapped\",\"LabelSource\"\r\n" +
            "\"Backup Server is Up To Date\",\"Passed\",\"BackupServerUpdates\",\"True\",\"Config\"\r\n" +
            "\"MFA is enabled\",\"Not Implemented\",\"MfaEnabled\",\"False\",\"EnumName\"";

        private const string PreRuleTypeCsv =
            "\"Best Practice\",\"Status\"\r\n" +
            "\"Backup Server is Up To Date\",\"Passed\"\r\n" +
            "\"MFA is enabled\",\"Not Implemented\"";

        private static readonly string BlankIdCsv =
            "\"Best Practice\",\"Status\",\"RuleType\",\"IsMapped\",\"LabelSource\",\"RuleId\"\r\n" +
            "\"Backup Server is Up To Date\",\"Passed\",\"BackupServerUpdates\",\"True\",\"Config\",\"\"\r\n" +
            "\"MFA is enabled\",\"Not Implemented\",\"MfaEnabled\",\"False\",\"EnumName\",\"\"";

        private static readonly string FilledIdCsv =
            "\"Best Practice\",\"Status\",\"RuleType\",\"IsMapped\",\"LabelSource\",\"RuleId\"\r\n" +
            $"\"Backup Server is Up To Date\",\"Passed\",\"BackupServerUpdates\",\"True\",\"Config\",\"{GuidA}\"\r\n" +
            $"\"MFA is enabled\",\"Not Implemented\",\"MfaEnabled\",\"False\",\"EnumName\",\"{GuidB}\"";

        private const string MetaCsv =
            "\"ScanStartedAt\",\"ScanCompletedAt\",\"ScanDurationSeconds\",\"ScanStatus\"\r\n" +
            "\"2024-01-01T10:00:00\",\"2024-01-01T10:00:05\",\"5\",\"Completed\"";

        public CComplianceTableRuleIdTests() : base("VhcComplianceRuleIdTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
        }

        private (string Html, string Json) Render(string complianceCsv)
        {
            File.WriteAllText(Path.Combine(this.VbrDir, "_SecurityCompliance.csv"), complianceCsv);
            File.WriteAllText(Path.Combine(this.VbrDir, "_SecurityComplianceMeta.csv"), MetaCsv);
            CGlobals.FullReportJson = new CFullReportJson();

            var ct = new CComplianceTable();
            string html = ct.ComplianceSummaryTable() + ct.ComplianceTable();
            string json = JsonSerializer.Serialize(CGlobals.FullReportJson);
            return (html, json);
        }

        [Fact]
        public void ComplianceTable_RuleIdColumnAbsentBlankOrFilled_RendersIdenticalHtmlAndJson()
        {
            var baseline = this.Render(OldCsv);
            Assert.Contains("Backup Server is Up To Date", baseline.Html);
            Assert.Contains("MFA is enabled", baseline.Json);

            Assert.Equal(baseline, this.Render(BlankIdCsv));
            Assert.Equal(baseline, this.Render(FilledIdCsv));
        }

        [Fact]
        public void ComplianceTable_CsvWithOnlyBestPracticeAndStatus_RendersIdenticalToRuleIdCsv()
        {
            var legacy = this.Render(PreRuleTypeCsv);
            var current = this.Render(FilledIdCsv);

            // Legacy CSV has no IsMapped, so no NEW badge; the filled CSV has an unmapped row.
            // Compare the JSON rows (plain text) which must match, and the RuleId must be absent from both.
            Assert.Equal(legacy.Json, current.Json);
            Assert.DoesNotContain(GuidA, legacy.Html);
            Assert.DoesNotContain(GuidA, current.Html);
        }

        [Fact]
        public void ComplianceTable_RuleIdPopulated_GuidAppearsNowhereInHtmlOrJson()
        {
            var result = this.Render(FilledIdCsv);

            foreach (var guid in new[] { GuidA, GuidB })
            {
                Assert.DoesNotContain(guid, result.Html);
                Assert.DoesNotContain(guid, result.Json);
            }

            Assert.DoesNotContain("RuleId", result.Html);
            Assert.DoesNotContain("RuleId", result.Json);
        }

        [Fact]
        public void ComplianceTable_RuleIdPopulated_JsonHeadersAndRowWidthUnchanged()
        {
            this.Render(FilledIdCsv);

            var section = CGlobals.FullReportJson.Sections["complianceTable"];
            Assert.Equal(new[] { "Best Practice", "Status" }, section.Headers);
            Assert.All(section.Rows, r => Assert.Equal(2, r.Count));
        }

        [Fact]
        public void ComplianceCsv_RuleIdPopulated_ParsesRuleIdPerRow()
        {
            File.WriteAllText(Path.Combine(this.VbrDir, "_SecurityCompliance.csv"), FilledIdCsv);

            var rows = new CCsvParser().ComplianceCsv().ToList();

            Assert.Equal(new[] { GuidA, GuidB }, rows.Select(r => r.RuleId).ToArray());
        }

        [Fact]
        public void ComplianceCsv_RuleIdBlank_ParsesWithoutError()
        {
            File.WriteAllText(Path.Combine(this.VbrDir, "_SecurityCompliance.csv"), BlankIdCsv);

            var rows = new CCsvParser().ComplianceCsv().ToList();

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.True(string.IsNullOrEmpty(r.RuleId)));
        }

        [Fact]
        public void ComplianceCsv_NoRuleIdColumn_ParsesWithNullRuleId()
        {
            File.WriteAllText(Path.Combine(this.VbrDir, "_SecurityCompliance.csv"), PreRuleTypeCsv);

            var rows = new CCsvParser().ComplianceCsv().ToList();

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Null(r.RuleId));
            Assert.Equal("Passed", rows[0].Status);
        }
    }
}
