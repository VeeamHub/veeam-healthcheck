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
    public class CCredentialsTableScrubTests : VbrTableScrubTestBase
    {
        // FileFinder matches "_Credentials.csv"; PrepareHeaderForMatch lowercases headers
        // so dynamic object property is "description" (lowercase).
        private const string CredentialsCsv =
            "\"Name\",\"UserName\",\"Description\",\"LastModified\"\r\n" +
            "\"CORP\\svc-backup\",\"svc-backup\",\"Service account for Veeam backup jobs\",\"2024-01-01\"";

        private const string CredentialsCsvEmptyDescription =
            "\"Name\",\"UserName\",\"Description\",\"LastModified\"\r\n" +
            "\"CORP\\svc-backup\",\"svc-backup\",\"\",\"2024-01-01\"";

        public CCredentialsTableScrubTests() : base("VhcCredScrubTests_")
        {
            // Reset global JSON state so tests are isolated.
            CGlobals.FullReportJson = new CFullReportJson();
        }

        private void WriteCredentialsCsv(string content) =>
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_Credentials.csv"), content);

        [Fact]
        public void Render_ScrubTrue_DescriptionIsReplacedWithToken()
        {
            WriteCredentialsCsv(CredentialsCsv);
            string html = new CCredentialsTable().Render(scrub: true);

            Assert.DoesNotContain("Service account for Veeam backup jobs", html);
            Assert.Contains("Item_", html);
        }

        [Fact]
        public void Render_ScrubFalse_DescriptionPassesThroughUnchanged()
        {
            WriteCredentialsCsv(CredentialsCsv);
            string html = new CCredentialsTable().Render(scrub: false);

            Assert.Contains("Service account for Veeam backup jobs", html);
        }

        [Fact]
        public void Render_ScrubTrue_EmptyDescription_DoesNotThrow()
        {
            WriteCredentialsCsv(CredentialsCsvEmptyDescription);
            var exception = Record.Exception(() => new CCredentialsTable().Render(scrub: true));
            Assert.Null(exception);
        }

        // --- JSON section capture ---

        [Fact]
        public void Render_PopulatesJsonSection_CredentialsKey()
        {
            WriteCredentialsCsv(CredentialsCsv);
            new CCredentialsTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("credentials"),
                "Expected Sections[\"credentials\"] to be populated after Render.");
        }

        [Fact]
        public void Render_JsonSection_HasExpectedHeaders()
        {
            WriteCredentialsCsv(CredentialsCsv);
            new CCredentialsTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["credentials"];
            Assert.Equal(new List<string> { "Name", "User Name", "Description", "Last Modified" }, section.Headers);
        }

        [Fact]
        public void Render_JsonSection_ContainsOneRowForOneCsvRecord()
        {
            WriteCredentialsCsv(CredentialsCsv);
            new CCredentialsTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["credentials"];
            Assert.Single(section.Rows);
            Assert.Equal("CORP\\svc-backup", section.Rows[0][0]); // Name column
        }

        [Fact]
        public void Render_ScrubTrue_JsonSection_NameIsScrubbed()
        {
            WriteCredentialsCsv(CredentialsCsv);
            new CCredentialsTable().Render(scrub: true);

            var section = CGlobals.FullReportJson.Sections["credentials"];
            Assert.Single(section.Rows);
            Assert.DoesNotContain("CORP\\svc-backup", section.Rows[0][0]);
        }

        [Fact]
        public void Render_EmptyData_JsonSection_StillPresentWithZeroRows()
        {
            // Write a CSV with headers only — no data rows.
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_Credentials.csv"),
                "\"Name\",\"UserName\",\"Description\",\"LastModified\"\r\n");
            new CCredentialsTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("credentials"));
            Assert.Empty(CGlobals.FullReportJson.Sections["credentials"].Rows);
        }
    }
}
