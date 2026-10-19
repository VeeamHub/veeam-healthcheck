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

        [Fact]
        public void Vb365Controllers_ShortOsVersionString_StillRendersRow()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Controller.csv", s => s.Replace("Microsoft Windows NT 10.0.20348.0", "Windows"));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Controllers());

            Assert.Single(rows);
            Assert.Contains("contoso-vb365-ctl01", rows[0][12]);
            Assert.Equal("Windows", Cell(rows[0][1]));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("Microsoft Windows NT", "")]
        [InlineData("Microsoft Windows NT unknown", "")]
        [InlineData("Microsoft Windows NT 6", "")]
        [InlineData("Microsoft Windows NT 6.1.7601.0", "cell-danger")]
        [InlineData("Microsoft Windows NT 6.3.9600.0", "cell-warning")]
        [InlineData("Microsoft Windows NT 10.0.20348.0", "")]
        public void Vb365Controllers_OsVersionFormats_PreserveRowAndKnownVersionShades(string osVersion, string expectedShade)
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Controller.csv", s => s.Replace("Microsoft Windows NT 10.0.20348.0", osVersion));

            var row = Assert.Single(Vb365ReportHarness.Rows(new CM365Tables().Vb365Controllers()));

            Assert.Contains("contoso-vb365-ctl01", row[12]);
            Assert.Equal(osVersion, Cell(row[1]));
            if (expectedShade.Length == 0)
            {
                Assert.DoesNotContain("class=", row[1]);
            }
            else
            {
                Assert.Contains(expectedShade, row[1]);
            }
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

        [Fact]
        public void Jobs_NotScheduledJob_ScheduleCellIsWarning()
        {
            using var h = new Vb365ReportHarness();

            var row = Vb365ReportHarness.Rows(new CM365Tables().Jobs())[2];

            Assert.Equal("Not scheduled", Cell(row[11]));
            Assert.Contains("cell-warning", row[11]);
        }

        [Fact]
        public void Vb365Repos_RepoUnderFivePercentFree_FreeCellIsDanger()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Repos());

            // Contoso-Düsseldorf-Repo: 0.040 TB free of 1.000 TB.
            Assert.Contains("cell-danger", rows[2][9]);
            Assert.DoesNotContain("class=", rows[0][9]);
            Assert.DoesNotContain("class=", rows[1][9]);
        }

        [Theory]
        [InlineData("0.000 TB (0.00 %)", "cell-danger")]
        [InlineData("0.049 TB (0.00 %)", "cell-danger")]
        [InlineData("0.050 TB (0.00 %)", "cell-warning")]
        [InlineData("0.099 TB (0.00 %)", "cell-warning")]
        [InlineData("0.100 TB (0.00 %)", "")]
        public void Vb365Repos_FreeSpaceThresholds_ApplyExpectedShade(string freeSpace, string expectedShade)
        {
            using var h = new Vb365ReportHarness();
            h.Edit("LocalRepositories.csv", s => s.Replace("0.040 TB (0.00 %)", freeSpace));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Repos());

            Assert.Equal(3, rows.Count);
            Assert.Equal(freeSpace, Cell(rows[2][9]));
            if (expectedShade.Length == 0)
            {
                Assert.DoesNotContain("class=", rows[2][9]);
            }
            else
            {
                Assert.Contains(expectedShade, rows[2][9]);
            }
        }

        [Fact]
        public void Vb365ObjectRepos_AlmostFullRepo_FreeSpaceCellIsShaded()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("ObjectRepositories.csv", s => s.Replace("\"511.75 TB\"", "\"10.00 TB\""));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365ObjectRepos());

            Assert.Contains("class=\"cell-", rows[0][8]);
            Assert.Contains("cell-danger", rows[0][8]);
        }

        [Fact]
        public void Vb365Security_ExpiredCertificate_ExpiryCellIsDanger()
        {
            using var h = new Vb365ReportHarness();

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Security());

            // Restore Portal cert expired 01/15/2020.
            var portal = rows.Single(r => Cell(r[0]) == "Restore Portal");
            Assert.Contains("cell-danger", portal[4]);
        }

        [Theory]
        [InlineData(-1, "cell-danger")]
        [InlineData(30, "cell-warning")]
        [InlineData(90, "")]
        public void Vb365Security_CertificateExpiryThresholds_ShadeEveryService(int daysUntilExpiry, string expectedShade)
        {
            using var h = new Vb365ReportHarness();
            string expiry = System.DateTime.Today.AddDays(daysUntilExpiry).ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            h.Edit("Security.csv", s => s
                .Replace("\"06/30/2099\"", $"\"{expiry}\"")
                .Replace("\"01/15/2020\"", $"\"{expiry}\"")
                .Replace("\"False\",\"\",\"\",\"False\"", $"\"False\",\"\",\"{expiry}\",\"False\""));

            var rows = Vb365ReportHarness.Rows(new CM365Tables().Vb365Security()).Where(r => r.Count == 6).ToList();

            Assert.Equal(5, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.Equal(expiry, Cell(row[4]));
                if (expectedShade.Length == 0)
                {
                    Assert.DoesNotContain("class=", row[4]);
                }
                else
                {
                    Assert.Contains(expectedShade, row[4]);
                }
            });
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

        [Fact]
        public void Globals_ZeroLicensedUsers_StillRendersLicenseStatus()
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Global.csv", s => s.Replace("\"5000\"", "\"0\""));

            string html = new CM365Tables().Globals();

            Assert.Contains("Contoso Holdings Ltd", html);
            var row = Assert.Single(Vb365ReportHarness.Rows(html));
            Assert.Equal("0", Cell(row[6]));
            Assert.Equal("4300", Cell(row[7]));
            Assert.Contains("cell-danger", row[7]);
        }

        [Theory]
        [InlineData("0", "0", "")]
        [InlineData("5000", "4500", "")]
        [InlineData("5000", "4501", "cell-warning")]
        [InlineData("5000", "4750", "cell-warning")]
        [InlineData("5000", "4751", "cell-danger")]
        public void Globals_LicenseUsageThresholds_ApplyExpectedShade(string licensedFor, string used, string expectedShade)
        {
            using var h = new Vb365ReportHarness();
            h.Edit("Global.csv", s => s.Replace("\"5000\"", $"\"{licensedFor}\"").Replace("\"4300\"", $"\"{used}\""));

            var row = Assert.Single(Vb365ReportHarness.Rows(new CM365Tables().Globals()));

            Assert.Equal(licensedFor, Cell(row[6]));
            Assert.Equal(used, Cell(row[7]));
            if (expectedShade.Length == 0)
            {
                Assert.DoesNotContain("class=", row[7]);
            }
            else
            {
                Assert.Contains(expectedShade, row[7]);
            }
        }
    }
}
