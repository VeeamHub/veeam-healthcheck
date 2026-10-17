// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VB365
{
    /// <summary>Compiles the VB365 report once over the synthetic fixtures and asserts the HTML.</summary>
    public sealed class Vb365CompiledReportFixture
    {
        public string Html { get; }

        public string ScrubbedHtml { get; }

        public Vb365CompiledReportFixture()
        {
            using (var h = new Vb365ReportHarness())
            {
                this.Html = h.CompileReport();
            }

            using (var h = new Vb365ReportHarness(scrub: true))
            {
                this.ScrubbedHtml = h.CompileReport();
            }
        }
    }

    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class Vb365ReportCompilerTests : IClassFixture<Vb365CompiledReportFixture>
    {
        private static readonly string[] SectionIds =
        {
            "global", "protstat", "controller", "controllerdrives", "proxies", "repos", "objrepo",
            "sec", "orgs", "jobs", "jobstats", "procstats", "jobsessions",
        };

        private readonly Vb365CompiledReportFixture report;

        public Vb365ReportCompilerTests(Vb365CompiledReportFixture report) => this.report = report;

        private string Content => Vb365ReportHarness.StripScriptAndStyle(this.report.Html);

        private string ScrubbedContent => Vb365ReportHarness.StripScriptAndStyle(this.report.ScrubbedHtml);

        private static string Section(string content, string id) => Vb365ReportHarness.Section(content, id);

        [Theory]
        [MemberData(nameof(SectionIdData))]
        public void CompileReport_FixtureData_ContainsSectionDiv(string id)
        {
            Assert.Contains($"id=\"{id}\"", this.Content);
        }

        [Theory]
        [MemberData(nameof(SectionIdData))]
        public void CompileReport_FixtureData_NavLinksToEverySection(string id)
        {
            Assert.Contains($"#{id}", this.Content);
        }

        public static TheoryData<string> SectionIdData()
        {
            var data = new TheoryData<string>();
            foreach (string id in SectionIds)
            {
                data.Add(id);
            }

            return data;
        }

        [Fact]
        public void CompileReport_FixtureData_HasTopLevelHeadersInOrder()
        {
            string[] headers = Regex.Matches(this.Content, "<h1><u>(.*?)</u></h1>").Select(m => m.Groups[1].Value).ToArray();

            Assert.Equal(new[] { "Overview", "Backup Infrastructure", "Security", "M365 Backups" }, headers);
        }

        [Fact]
        public void CompileReport_FixtureData_SectionsAppearInDocumentOrder()
        {
            int[] positions = SectionIds.Select(id => this.Content.IndexOf($"id=\"{id}\"", StringComparison.Ordinal)).ToArray();

            Assert.All(positions, p => Assert.True(p > 0));
            Assert.Equal(positions.OrderBy(p => p), positions);
        }

        [Theory]
        [InlineData("global", 1)]
        [InlineData("protstat", 1)]
        [InlineData("controller", 1)]
        [InlineData("controllerdrives", 3)]
        [InlineData("proxies", 3)]
        [InlineData("repos", 3)]
        [InlineData("objrepo", 2)]
        [InlineData("orgs", 3)]
        [InlineData("jobs", 4)]
        [InlineData("jobstats", 4)]
        [InlineData("procstats", 18)]
        [InlineData("jobsessions", 6)]
        public void CompileReport_FixtureData_RowCountMatchesFixture(string id, int expectedRows)
        {
            Assert.Equal(expectedRows, Vb365ReportHarness.RowCount(Section(this.Content, id)));
        }

        [Fact]
        public void CompileReport_FixtureData_SecuritySectionHasSummaryRowAndFiveServiceRows()
        {
            var rows = Vb365ReportHarness.Rows(Section(this.Content, "sec"));

            Assert.Equal(6, rows.Count);
            Assert.Equal(
                new[] { "Server", "API", "Tenant Auth", "Restore Portal", "Operator Auth" },
                rows.Skip(1).Select(r => Vb365ReportHarness.CellText(r[0])).ToArray());
        }

        [Theory]
        [InlineData("global", "Contoso Holdings Ltd")]
        [InlineData("controller", "contoso-vb365-ctl01")]
        [InlineData("controllerdrives", "Contoso Data Disk")]
        [InlineData("proxies", "contoso-prx02.contoso.example")]
        [InlineData("repos", "Contoso-Düsseldorf-Repo")]
        [InlineData("objrepo", "contoso-s3-primary")]
        [InlineData("sec", "Contoso Portal Cert")]
        [InlineData("orgs", "Contoso München GmbH")]
        [InlineData("jobs", "München-SharePoint-Teams-Backup")]
        [InlineData("jobstats", "Contoso-Prod-Mailbox-Copy")]
        [InlineData("procstats", "Contoso-Prod-Mailbox-Backup")]
        [InlineData("jobsessions", "OnPrem-Exchange-Backup")]
        public void CompileReport_FixtureData_SectionShowsKeyFixtureValue(string id, string expected)
        {
            Assert.Contains(expected, Section(this.Content, id));
        }

        [Fact]
        public void CompileReport_FixtureData_ProtectionStatusCountsMatchFixture()
        {
            var cells = Vb365ReportHarness.Rows(Section(this.Content, "protstat")).Single()
                .Select(Vb365ReportHarness.CellText).ToArray();

            // 10 users: 7 protected, 2 with no backup, 1 stale shared mailbox.
            Assert.Equal(new[] { "10", "7", "2", "1" }, cells);
        }

        [Fact]
        public void CompileReport_FixtureData_ProtectionStatusUnprotectedShareIsDanger()
        {
            // 2 of 9 backed-up-or-not users (22%) is above the 20% target.
            string cell = Vb365ReportHarness.Rows(Section(this.Content, "protstat")).Single()[2];

            Assert.Contains("cell-danger", cell);
        }

        [Fact]
        public void CompileReport_FixtureData_FlagsRiskyCellsAndLeavesHealthyOnesPlain()
        {
            var proxies = Vb365ReportHarness.Rows(Section(this.Content, "proxies"));
            var repos = Vb365ReportHarness.Rows(Section(this.Content, "repos"));
            var drives = Vb365ReportHarness.Rows(Section(this.Content, "controllerdrives"));
            var jobs = Vb365ReportHarness.Rows(Section(this.Content, "jobs"));

            Assert.Contains("cell-danger", proxies[1][4]);   // Offline proxy
            Assert.DoesNotContain("class=", proxies[0][4]);  // Online proxy
            Assert.Contains("cell-danger", repos[1][7]);     // Out of Sync repository state
            Assert.Contains("cell-danger", repos[2][4]);     // repository on C:
            Assert.Contains("cell-warning", drives[1][3]);   // HDD, not SSD
            Assert.DoesNotContain("class=", drives[0][3]);   // SSD
            Assert.Contains("cell-warning", drives[2][8]);   // Degraded disk
            Assert.Contains("cell-warning", jobs[2][10]);    // disabled job
            Assert.Contains("cell-warning", jobs[0][6]);     // 5210 selected objects > 5000
        }

        [Fact]
        public void CompileReport_FixtureData_LicenseUsageBelowThresholdIsNotShaded()
        {
            var cells = Vb365ReportHarness.Rows(Section(this.Content, "global")).Single();

            Assert.DoesNotContain("class=", cells[7]); // 4300 of 5000 = 86%
            Assert.DoesNotContain("class=", cells[1]); // expiry in 2099
        }

        [Fact]
        public void CompileReport_FixtureData_PreservesMultiLineSessionLog()
        {
            string sessions = Section(this.Content, "jobsessions");

            Assert.Contains("Throttled by Microsoft 365 (HTTP 429)", sessions);
            Assert.Contains("repository Contoso-Archive-Repo is out of sync", sessions);
        }

        [Fact]
        public void CompileReport_FixtureData_HasNoPlaceholderValuesOutsideScriptAndStyle()
        {
            Assert.DoesNotMatch(@"\bNaN\b|\bundefined\b|\bInfinity\b", this.Content);
        }

        [Fact]
        public void CompileReport_FixtureData_EndsWithVersionFooterAndScript()
        {
            Assert.Contains("vHC Version: ", this.report.Html);
            Assert.Contains("<script type=\"text/javascript\">", this.report.Html);
        }

        [Fact]
        public void CompileReport_FixtureData_BalancesSectionDivs()
        {
            int opens = Regex.Matches(this.Content, "<div\\b").Count;
            int closes = Regex.Matches(this.Content, "</div>").Count;

            Assert.Equal(opens, closes);
        }

        // ----- scrub mode -----

        [Theory]
        [InlineData("contoso-m365-prod")]
        [InlineData("Contoso München GmbH")]
        [InlineData("contoso.onmicrosoft.com")]
        [InlineData("contoso-prx01.contoso.example")]
        [InlineData("contoso-prx03.contoso.example")]
        [InlineData("Contoso-Primary-Repo")]
        [InlineData("Contoso-Düsseldorf-Repo")]
        [InlineData("contoso-archive-bucket")]
        [InlineData("Contoso-Prod-Mailbox-Backup")]
        [InlineData("München-SharePoint-Teams-Backup")]
        [InlineData("OnPrem-Exchange-Backup")]
        [InlineData("contoso-vb365-ctl01")]
        [InlineData("Contoso Holdings Ltd")]
        [InlineData(@"D:\VB365\Repo01")]
        public void CompileReport_ScrubMode_RemovesFixtureIdentifiers(string identifier)
        {
            Assert.DoesNotContain(identifier, this.ScrubbedContent);
        }

        [Fact]
        public void CompileReport_ScrubMode_ReplacesNamesWithTypedPlaceholders()
        {
            string content = this.ScrubbedContent;

            Assert.Matches(@"<td>Job_\d+</td>", content);
            Assert.Matches(@"<td>Server_\d+</td>", content);
            Assert.Matches(@"<td>Repository_\d+</td>", content);
            Assert.Matches(@"<td>Path_\d+</td>", content);
            Assert.Matches(@"<td>VM_\d+</td>", content);
        }

        [Fact]
        public void CompileReport_ScrubMode_KeepsSameStructureAsOriginal()
        {
            foreach (string id in SectionIds)
            {
                Assert.Equal(
                    Vb365ReportHarness.RowCount(Section(this.Content, id)),
                    Vb365ReportHarness.RowCount(Section(this.ScrubbedContent, id)));
            }
        }

        [Fact]
        public void CompileReport_ScrubMode_MapsSameNameToSamePlaceholder()
        {
            var sessions = Vb365ReportHarness.Rows(Section(this.ScrubbedContent, "jobsessions"));

            // The first two sessions belong to the same job.
            Assert.Equal(Vb365ReportHarness.CellText(sessions[0][0]), Vb365ReportHarness.CellText(sessions[1][0]));
            Assert.NotEqual(Vb365ReportHarness.CellText(sessions[0][0]), Vb365ReportHarness.CellText(sessions[2][0]));
        }

        [Fact]
        public void CompileReport_ScrubMode_HasNoPlaceholderValuesOutsideScriptAndStyle()
        {
            Assert.DoesNotMatch(@"\bNaN\b|\bundefined\b|\bInfinity\b", this.ScrubbedContent);
        }

        [Fact]
        public void Vb365JobSessions_ScrubMode_KeepsThroughputValues()
        {
            string sessions = Section(this.ScrubbedContent, "jobsessions");

            Assert.Contains("35.210 MB/s", sessions);
        }

        [Fact]
        public void Globals_ScrubMode_RemovesLicenseContact()
        {
            Assert.DoesNotContain("Alex Contoso", Section(this.ScrubbedContent, "global"));
        }
    }
}
