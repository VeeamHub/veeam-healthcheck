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
    public class CEmailNotificationTableTests : VbrTableScrubTestBase
    {
        // FileFinder matches "_EmailNotification.csv"; CsvHelper lowercases headers via
        // PrepareHeaderForMatch, so dynamic properties are accessed as "smtpserver", "from", etc.
        private const string EmailNotificationCsv =
            "\"IsEnabled\",\"SmtpServer\",\"From\",\"To\",\"NotifyOnSuccess\",\"NotifyOnWarning\",\"NotifyOnError\"\r\n" +
            "\"True\",\"smtp.corp.example.com\",\"veeam@corp.example.com\",\"admin@corp.example.com\",\"False\",\"True\",\"True\"";

        public CEmailNotificationTableTests() : base("VhcEmailNotifTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
        }

        private void WriteEmailCsv(string content) =>
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_EmailNotification.csv"), content);

        // --- HTML rendering ---

        [Fact]
        public void Render_ScrubFalse_ContainsSmtpServer()
        {
            WriteEmailCsv(EmailNotificationCsv);
            string html = new CEmailNotificationTable().Render(scrub: false);

            Assert.Contains("smtp.corp.example.com", html);
        }

        [Fact]
        public void Render_ScrubTrue_SmtpServerIsScrubbed()
        {
            WriteEmailCsv(EmailNotificationCsv);
            string html = new CEmailNotificationTable().Render(scrub: true);

            Assert.DoesNotContain("smtp.corp.example.com", html);
        }

        [Fact]
        public void Render_ScrubTrue_DoesNotThrow()
        {
            WriteEmailCsv(EmailNotificationCsv);
            var exception = Record.Exception(() => new CEmailNotificationTable().Render(scrub: true));
            Assert.Null(exception);
        }

        // --- JSON section capture ---

        [Fact]
        public void Render_PopulatesJsonSection_EmailNotificationKey()
        {
            WriteEmailCsv(EmailNotificationCsv);
            new CEmailNotificationTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("emailNotification"),
                "Expected Sections[\"emailNotification\"] to be populated after Render.");
        }

        [Fact]
        public void Render_JsonSection_HasExpectedHeaders()
        {
            WriteEmailCsv(EmailNotificationCsv);
            new CEmailNotificationTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["emailNotification"];
            Assert.Equal(
                new List<string> { "Is Enabled", "SMTP Server", "From", "To", "Notify On Success", "Notify On Warning", "Notify On Error" },
                section.Headers);
        }

        [Fact]
        public void Render_JsonSection_ContainsOneRowForOneCsvRecord()
        {
            WriteEmailCsv(EmailNotificationCsv);
            new CEmailNotificationTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["emailNotification"];
            Assert.Single(section.Rows);
            Assert.Equal("True", section.Rows[0][0]);                       // Is Enabled
            Assert.Equal("smtp.corp.example.com", section.Rows[0][1]);      // SMTP Server
            Assert.Equal("veeam@corp.example.com", section.Rows[0][2]);     // From
            Assert.Equal("admin@corp.example.com", section.Rows[0][3]);     // To
        }

        [Fact]
        public void Render_ScrubTrue_JsonSection_SmtpServerIsScrubbed()
        {
            WriteEmailCsv(EmailNotificationCsv);
            new CEmailNotificationTable().Render(scrub: true);

            var section = CGlobals.FullReportJson.Sections["emailNotification"];
            Assert.Single(section.Rows);
            Assert.DoesNotContain("smtp.corp.example.com", section.Rows[0][1]);
        }

        [Fact]
        public void Render_EmptyData_JsonSection_StillPresentWithZeroRows()
        {
            File.WriteAllText(System.IO.Path.Combine(VbrDir, "_EmailNotification.csv"),
                "\"IsEnabled\",\"SmtpServer\",\"From\",\"To\",\"NotifyOnSuccess\",\"NotifyOnWarning\",\"NotifyOnError\"\r\n");
            new CEmailNotificationTable().Render(scrub: false);

            Assert.True(CGlobals.FullReportJson.Sections.ContainsKey("emailNotification"));
            Assert.Empty(CGlobals.FullReportJson.Sections["emailNotification"].Rows);
        }
    }
}
