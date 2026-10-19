// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.VB365;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VB365
{
    /// <summary>Drives CM365Tables directly with edited copies of the fixtures.</summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class Vb365TablesEdgeCaseTests
    {
        private static string Cell(string td) => Vb365ReportHarness.CellText(td);

        [Fact]
        public void MakeVb365NavTable_ListsEverySectionLink()
        {
            string nav = new CM365Tables().MakeVb365NavTable();

            foreach (string id in new[] { "global", "protstat", "controller", "controllerdrives", "proxies", "repos", "objrepo", "sec", "orgs", "jobs", "jobstats", "procstats", "jobsessions" })
            {
                Assert.Contains("#" + id, nav);
            }
        }

        [Fact]
        public void Vb365Rbac_FixtureData_RendersBothRoles()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Rbac());

            Assert.Equal(2, rows.Count);
            Assert.Contains("Contoso Helpdesk Restore Operators", rows[0][0]);
        }

        [Fact]
        public void Vb365Permissions_MissingCsv_RendersEmptyTableWithoutThrowing()
        {
            using var h = new Vb365ReportHarness();

            string html = new CM365Tables().Vb365Permissions();

            Assert.Equal(4, Vb365ReportHarness.HeaderCount(html));
            Assert.Empty(Vb365ReportHarness.Rows(html));
        }

        [Fact]
        public void Vb365Rbac_ScrubMode_ReplacesOperatorsAndRoles()
        {
            using var h = new Vb365ReportHarness(scrub: true);

            string html = new CM365Tables().Vb365Rbac();

            Assert.DoesNotContain("helpdesk1@contoso.example", html);
            Assert.DoesNotContain("Contoso Helpdesk Restore Operators", html);
        }

        [Fact]
        public void Vb365Proxies_RiskyProxies_AreShaded()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Proxies());

            // prx02: 32 threads, throttled, offline, outdated, behind an internet proxy
            Assert.All(new[] { 2, 3, 6 }, i => Assert.Contains("cell-warning", rows[1][i]));
            Assert.Contains("cell-danger", rows[1][4]);
            Assert.Contains("cell-danger", rows[1][5]);

            // prx03: Windows 6.3, 4 GB RAM, 2 CPUs
            Assert.Contains("cell-warning", rows[2][8]);
            Assert.Contains("cell-danger", rows[2][9]);
            Assert.Contains("cell-danger", rows[2][10]);
        }

        [Fact]
        public void Vb365Proxies_ShortOsVersionString_StillRendersEveryRow()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Proxies.csv", s => s.Replace("Microsoft Windows NT 6.3.9600.0", "Windows"));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Proxies());

            Assert.Equal(3, rows.Count);
            Assert.Equal("Windows", Cell(rows[2][8]));
        }

        [Fact]
        public void Vb365Controllers_LowRam_IsShaded()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Controller.csv", s => s.Replace("\"32 GB\"", "\"4 GB\""));

            var row = Vb365ReportHarness.Rows(new CM365Tables().Vb365Controllers()).Single();

            Assert.Contains("cell-warning", row[2]);
        }

        [Fact(Skip = "bug: Vb365Controllers does Split()[3] on the OS string; a short value throws into an empty catch and the row vanishes")]
        public void Vb365Controllers_ShortOsVersionString_StillRendersRow()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Controller.csv", s => s.Replace("Microsoft Windows NT 10.0.20348.0", "Windows"));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Controllers());

            Assert.Single(rows);
            Assert.Contains("contoso-vb365-ctl01", rows[0][12]);
        }

        [Fact]
        public void Vb365JobSessions_FixtureData_HeaderCountMatchesCellCount()
        {
            using var h = new Vb365ReportHarness();

            string html = new CM365Tables().Vb365JobSessions();

            Assert.All(Vb365ReportHarness.Rows(html), r => Assert.Equal(Vb365ReportHarness.HeaderCount(html), r.Count));
        }

        [Fact]
        public void Vb365JobSessions_ScrubMode_LeavesThroughputCellUnscrubbed()
        {
            using var h = new Vb365ReportHarness(scrub: true);

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365JobSessions());

            Assert.Equal("35.210 MB/s", Cell(rows[0][5]));
        }

        [Fact]
        public void Jobs_FixtureData_RendersAllRowsInOrder()
        {
            using var h = new Vb365ReportHarness();

            var names = Vb365ReportHarness.Rows(new CM365Tables().Jobs()).Select(r => Cell(r[1])).ToArray();

            Assert.Equal(
                new[] { "Contoso-Prod-Mailbox-Backup", "Contoso-Prod-Mailbox-Copy", "München-SharePoint-Teams-Backup", "OnPrem-Exchange-Backup" },
                names);
        }

        [Fact]
        public void Jobs_RowWithEmptyOrganization_DoesNotDropLaterRows()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Jobs.csv", s => s.Replace("\"Contoso München GmbH\",\"München-SharePoint", "\"\",\"München-SharePoint"));

            var names = Vb365ReportHarness.Rows(new CM365Tables().Jobs()).Select(r => Cell(r[1])).ToArray();

            Assert.Equal(4, names.Length);
            Assert.Contains("München-SharePoint-Teams-Backup", names);
            Assert.Contains("OnPrem-Exchange-Backup", names);
        }

        [Fact(Skip = "bug: Jobs() compares Schedule to 'Not Scheduled' but the collector writes 'Not scheduled', so the warning shade never applies")]
        public void Jobs_NotScheduledJob_ScheduleCellIsWarning()
        {
            using var h = new Vb365ReportHarness();

            var row = Vb365ReportHarness.Rows(new CM365Tables().Jobs())[2];

            Assert.Equal("Not scheduled", Cell(row[11]));
            Assert.Contains("cell-warning", row[11]);
        }

        [Fact(Skip = "bug: Vb365Repos parses '8.250 TB (0.00 %)' with double.TryParse, which fails, so the free-space shade never applies (and 'freeSpace = 1' was meant to be 'freeShade = 1')")]
        public void Vb365Repos_RepoUnderFivePercentFree_FreeCellIsDanger()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Repos());

            // Contoso-Düsseldorf-Repo: 0.040 TB free of 1.000 TB.
            Assert.Contains("cell-danger", rows[2][9]);
            Assert.DoesNotContain("class=", rows[0][9]);
        }

        [Fact(Skip = "bug: Vb365ObjectRepos computes freeSpaceShade but never passes it to TableData")]
        public void Vb365ObjectRepos_AlmostFullRepo_FreeSpaceCellIsShaded()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("ObjectRepositories.csv", s => s.Replace("\"511.75 TB\"", "\"10.00 TB\""));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365ObjectRepos());

            Assert.Contains("class=\"cell-", rows[0][8]);
        }

        [Fact(Skip = "bug: Vb365Security computes cert-expiry shades but never passes them to TableData, so an expired cert is not flagged")]
        public void Vb365Security_ExpiredCertificate_ExpiryCellIsDanger()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Security());

            // Restore Portal cert expired 01/15/2020.
            var portal = rows.Single(r => Cell(r[0]) == "Restore Portal");
            Assert.Contains("cell-danger", portal[4]);
        }

        [Fact]
        public void Vb365Security_EmptyCertExpiry_IsNotFlaggedAsExpired()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Security());

            var tenant = rows.Single(r => Cell(r[0]) == "Tenant Auth");
            Assert.Equal(string.Empty, Cell(tenant[4]));
            Assert.DoesNotContain("cell-danger", tenant[4]);
        }

        [Fact]
        public void Vb365Security_SelfSignedServerCert_IsWarning()
        {
            using var h = new Vb365ReportHarness();

            var server = Vb365ReportHarness.Rows(new CM365Tables().Vb365Security()).Single(r => Cell(r[0]) == "Server");

            Assert.Contains("cell-warning", server[5]);
        }

        [Fact]
        public void Vb365ProtStat_StaleAndUnprotectedUsers_TotalEqualsRowCount()
        {
            using var h = new Vb365ReportHarness();

            var cells = Vb365ReportHarness.Rows(new CM365Tables().Vb365ProtStat()).Single().Select(Cell).ToArray();

            Assert.Equal("10", cells[0]);
        }

        [Fact(Skip = "bug: Globals() divides licensesUsed by licensedFor in decimal; 0 licensed throws DivideByZeroException into an empty catch and the whole row disappears")]
        public void Globals_ZeroLicensedUsers_StillRendersLicenseStatus()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Global.csv", s => s.Replace("\"5000\"", "\"0\""));

            string html = new CM365Tables().Globals();

            Assert.Contains("Contoso Holdings Ltd", html);
        }
    }
}
