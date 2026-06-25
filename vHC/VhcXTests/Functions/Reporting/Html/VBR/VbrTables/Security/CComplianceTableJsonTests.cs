// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.Security
{
    [Trait("Category", "Scrubbing")]
    [Collection("GlobalState")]
    public class CComplianceTableJsonTests : VbrTableScrubTestBase
    {
        // Minimal _SecurityCompliance.csv with two rows of different statuses.
        private const string ComplianceCsv =
            "\"Best Practice\",\"Status\"\r\n" +
            "\"Backup Server is Up To Date\",\"Passed\"\r\n" +
            "\"MFA is enabled\",\"Not Implemented\"";

        // Minimal _SecurityComplianceMeta.csv required by the constructor.
        private const string ComplianceMetaCsv =
            "\"ScanStartedAt\",\"ScanCompletedAt\",\"ScanDurationSeconds\",\"ScanStatus\"\r\n" +
            "\"2024-01-01T10:00:00\",\"2024-01-01T10:00:05\",\"5\",\"Completed\"";

        public CComplianceTableJsonTests() : base("VhcComplianceJsonTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
        }

        private void WriteComplianceCsvs()
        {
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_SecurityCompliance.csv"), ComplianceCsv);
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_SecurityComplianceMeta.csv"), ComplianceMetaCsv);
        }

        [Fact]
        public void ComplianceTable_PopulatesJsonSection_ComplianceTableKey()
        {
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("complianceTable"),
                "Expected Sections[\"complianceTable\"] to be populated after ComplianceTable().");
        }

        [Fact]
        public void ComplianceTable_JsonSection_HasExpectedHeaders()
        {
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            var section = CGlobals.FullReportJson.Sections["complianceTable"];
            Assert.Equal(new[] { "Best Practice", "Status" }, section.Headers);
        }

        [Fact]
        public void ComplianceTable_JsonSection_ContainsOneRowPerRule()
        {
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            var section = CGlobals.FullReportJson.Sections["complianceTable"];
            Assert.Equal(2, section.Rows.Count);
        }

        [Fact]
        public void ComplianceTable_JsonSection_RowsContainRawBestPracticeText()
        {
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            var section = CGlobals.FullReportJson.Sections["complianceTable"];
            var bestPractices = section.Rows.Select(r => r[0]).ToList();

            Assert.Contains("Backup Server is Up To Date", bestPractices);
            Assert.Contains("MFA is enabled", bestPractices);
        }

        [Fact]
        public void ComplianceTable_JsonSection_RowsContainRawStatusWithoutHtmlMarkup()
        {
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            var section = CGlobals.FullReportJson.Sections["complianceTable"];

            // JSON rows must carry plain text, no badge <span> or NEW markup.
            foreach (var row in section.Rows)
            {
                Assert.DoesNotContain("<span", row[1]);
                Assert.DoesNotContain("badge", row[1]);
            }

            var statuses = section.Rows.Select(r => r[1]).ToList();
            Assert.Contains("Passed", statuses);
            Assert.Contains("Not Implemented", statuses);
        }

        [Fact]
        public void ComplianceTable_JsonRows_DoNotContainNewBadgeMarkup()
        {
            // Even if a rule is unmapped (IsMapped == false), the JSON row must not contain
            // the NEW badge HTML that appears in the HTML report.
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            var section = CGlobals.FullReportJson.Sections["complianceTable"];
            foreach (var row in section.Rows)
            {
                Assert.DoesNotContain("badge-new", row[0]);
                Assert.DoesNotContain("NEW", row[0]);
            }
        }

        [Fact]
        public void ComplianceScan_MetadataStillPresentAfterComplianceTable()
        {
            // Verify the stable VIP-ingestion ComplianceScan property is unaffected by the change.
            WriteComplianceCsvs();
            var ct = new CComplianceTable();
            ct.ComplianceTable();

            Assert.NotNull(CGlobals.FullReportJson.ComplianceScan);
            Assert.Equal(2, CGlobals.FullReportJson.ComplianceScan.RuleCount);
        }
    }
}
