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
            "\"Name\",\"Role\",\"Description\"\r\n" +
            "\"CORP\\jdoe\",\"Veeam Backup Administrator\",\"John Doe - IT Admin - john.doe@corp.example.com\"";

        private const string UserRolesCsvEmptyDescription =
            "\"Name\",\"Role\",\"Description\"\r\n" +
            "\"CORP\\jdoe\",\"Veeam Backup Administrator\",\"\"";

        public CUserRolesTableScrubTests() : base("VhcUserRolesScrubTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
        }

        private void WriteUserRolesCsv(string content) =>
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_UserRoles.csv"), content);

        [Fact]
        public void Render_ScrubTrue_DescriptionIsReplacedWithToken()
        {
            WriteUserRolesCsv(UserRolesCsv);
            string html = new CUserRolesTable().Render(scrub: true);

            Assert.DoesNotContain("John Doe", html);
            Assert.DoesNotContain("john.doe@corp.example.com", html);
            Assert.Contains("Item_", html);
        }

        [Fact]
        public void Render_ScrubFalse_DescriptionPassesThroughUnchanged()
        {
            WriteUserRolesCsv(UserRolesCsv);
            string html = new CUserRolesTable().Render(scrub: false);

            Assert.Contains("John Doe - IT Admin - john.doe@corp.example.com", html);
        }

        [Fact]
        public void Render_ScrubTrue_EmptyDescription_DoesNotThrow()
        {
            WriteUserRolesCsv(UserRolesCsvEmptyDescription);
            var exception = Record.Exception(() => new CUserRolesTable().Render(scrub: true));
            Assert.Null(exception);
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
            Assert.Equal(new List<string> { "Name", "Role", "Description" }, section.Headers);
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
            Assert.Contains("John Doe", section.Rows[0][2]);                        // Description
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
                "\"Name\",\"Role\",\"Description\"\r\n");
            new CUserRolesTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("userRoles"));
            Assert.Empty(CGlobals.FullReportJson.Sections["userRoles"].Rows);
        }
    }
}
