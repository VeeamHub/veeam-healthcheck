// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Renders the Security Summary section with grid items and malware tables
    /// (formerly AddSecSummaryTable in CHtmlTables).
    /// </summary>
    internal class CSecuritySummarySection
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CSecuritySummarySection() { }

        public string Render(bool scrub)
        {
            CGlobals.Scrub = scrub;

            string s = this.form.SectionStartWithButtonNoTable("secsummary", VbrLocalizationHelper.SSTitle, string.Empty);
            string summary = this.sum.SecSum();

            try
            {
                CSecuritySummaryTable t = this.df.SecSummary();

                s += "<div class=\"security-grid\">";
                s += SecurityGridItem("Immutability", t.ImmutabilityEnabled);
                s += SecurityGridItem("Traffic Encryption", t.TrafficEncrptionEnabled);
                s += SecurityGridItem("Backup File Encryption", t.BackupFileEncrptionEnabled);
                s += SecurityGridItem("Config Backup Encryption", t.ConfigBackupEncrptionEnabled);
                s += SecurityGridItem("MFA Enabled", t.MFAEnabled);
                s += "</div>";
            }

            catch (Exception e)
            {
                this.log.Error("Security Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            if (CGlobals.VBRMAJORVERSION > 11)
            {
                // add malware table
                try
                {
                    var malware = new CMalwareTable();
                    s += malware.MalwareSettingsTable();

                    s += malware.MalwareExclusionsTable();
                    s += malware.MalwareEventsTable();
                    s += malware.MalwareInfectedObjectsTable();
                }
                catch (Exception e)
                {
                    this.log.Error("Malware Settings Data import failed. ERROR:");
                    this.log.Error("\t" + e.Message);
                }

                // Compliance tables extracted to separate sections
            }

            s += this.form.SectionEndNoTable(summary);

            // JSON section capture security summary
            try
            {
                var t = this.df.SecSummary();
                List<string> headers = new() { "ImmutabilityEnabled", "TrafficEncryptionEnabled", "BackupFileEncryptionEnabled", "ConfigBackupEncryptionEnabled", "MFAEnabled" };
                List<List<string>> rows = new()
                {
                    new List<string>
                    {
                        t.ImmutabilityEnabled ? "True" : "False",
                        t.TrafficEncrptionEnabled ? "True" : "False",
                        t.BackupFileEncrptionEnabled ? "True" : "False",
                        t.ConfigBackupEncrptionEnabled ? "True" : "False",
                        t.MFAEnabled ? "True" : "False"
                    },
                };
                CHtmlTables.SetSectionPublic("securitySummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture securitySummary JSON section: " + ex.Message);
            }

            return s;
        }

        private static string SecurityGridItem(string label, bool enabled)
        {
            string dotColor = enabled ? "green" : "red";
            return $"<div class=\"security-item\">" +
                   $"<div class=\"status-dot {dotColor}\"></div>" +
                   $"<div class=\"label\">{System.Net.WebUtility.HtmlEncode(label)}</div>" +
                   "</div>";
        }
    }
}
