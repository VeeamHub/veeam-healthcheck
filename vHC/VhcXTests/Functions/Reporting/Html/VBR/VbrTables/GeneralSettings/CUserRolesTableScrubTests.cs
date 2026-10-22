// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;
using System.IO;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings
{
    [Trait("Category", "Scrubbing")]
    [Collection("GlobalState")]
    public class CUserRolesTableScrubTests : VbrTableScrubTestBase
    {
        // FileFinder matches "_UserRoles.csv"; row dict keys match CSV header casing exactly.
        private const string UserRolesCsv =
            "\"Name\",\"Role\"\r\n" +
            "\"CORP\\jdoe\",\"Veeam Backup Administrator\"";

        public CUserRolesTableScrubTests() : base("VhcUserRolesScrubTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
        }

        private void WriteUserRolesCsv(string content) =>
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_UserRoles.csv"), content);

        [Fact]
        public void Render_ScrubTrue_NameIsReplacedWithToken()
        {
            WriteUserRolesCsv(UserRolesCsv);
            string html = new CUserRolesTable().Render(scrub: true);

            Assert.DoesNotContain("jdoe", html);
            Assert.Contains("Item_", html);
        }

        [Fact]
        public void Render_ScrubFalse_NamePassesThroughUnchanged()
        {
            WriteUserRolesCsv(UserRolesCsv);
            string html = new CUserRolesTable().Render(scrub: false);

            Assert.Contains("CORP\\jdoe", html);
        }

        // Get-VBRUserRoleAssignment exposes no description, so the table must not render an always-blank column.
        [Fact]
        public void Render_NoDescriptionColumn()
        {
            WriteUserRolesCsv(UserRolesCsv);
            string html = new CUserRolesTable().Render(scrub: false);

            Assert.DoesNotContain("Description", html);
        }

        // --- JSON section capture ---

        [Fact]
        public void Render_PopulatesJsonSection_UserRolesKey()
        {
            WriteUserRolesCsv(UserRolesCsv);
            new CUserRolesTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("userRoles"),
                "Expected Sections[\"userRoles\"] to be populated after Render.");
        }

        [Fact]
        public void Render_JsonSection_HasExpectedHeaders()
        {
            WriteUserRolesCsv(UserRolesCsv);
            new CUserRolesTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["userRoles"];
            Assert.Equal(new List<string> { "Name", "Role" }, section.Headers);
        }

        [Fact]
        public void Render_JsonSection_ContainsOneRowForOneCsvRecord()
        {
            WriteUserRolesCsv(UserRolesCsv);
            new CUserRolesTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["userRoles"];
            Assert.Single(section.Rows);
            Assert.Equal("CORP\\jdoe", section.Rows[0][0]);                         // Name
            Assert.Equal("Veeam Backup Administrator", section.Rows[0][1]);         // Role
            Assert.Equal(2, section.Rows[0].Count);
        }

        [Fact]
        public void Render_ScrubTrue_JsonSection_NameIsScrubbed()
        {
            WriteUserRolesCsv(UserRolesCsv);
            new CUserRolesTable().Render(scrub: true);

            var section = CGlobals.FullReportJson.Sections["userRoles"];
            Assert.Single(section.Rows);
            Assert.DoesNotContain("CORP\\jdoe", section.Rows[0][0]);
        }

        [Fact]
        public void Render_ScrubTrue_JsonSection_RoleIsNotScrubbed()
        {
            // Role is a Veeam-internal enum string, not PII — should pass through unchanged.
            WriteUserRolesCsv(UserRolesCsv);
            new CUserRolesTable().Render(scrub: true);

            var section = CGlobals.FullReportJson.Sections["userRoles"];
            Assert.Equal("Veeam Backup Administrator", section.Rows[0][1]);
        }

        [Fact]
        public void Render_EmptyData_JsonSection_StillPresentWithZeroRows()
        {
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_UserRoles.csv"),
                "\"Name\",\"Role\"\r\n");
            new CUserRolesTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("userRoles"));
            Assert.Empty(CGlobals.FullReportJson.Sections["userRoles"].Rows);
        }
    }
}
