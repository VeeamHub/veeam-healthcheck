using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings
{
    internal class CEmailNotificationTable
    {
        private readonly CHtmlFormatting form = new();

        public CEmailNotificationTable() { }

        [SuppressMessage(
            "Security",
            "cs/exposure-of-sensitive-information",
            Justification = "Intentional by design: VHC is a configuration health-check report generator. SMTP server and email addresses are part of the VBR configuration being audited. The 'scrub' parameter (when true) anonymizes these fields via CGlobals.Scrubber for sharing reports externally. End user controls scrub mode via CLI/GUI.")]
        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("emailnotification", "Email Notification", "Email Notification");

            s += this.form.TableHeader("Enabled", string.Empty);
            s += this.form.TableHeader("SMTP Server", string.Empty);
            s += this.form.TableHeader("Sender", string.Empty);
            s += this.form.TableHeader("Recipient", string.Empty);
            s += this.form.TableHeader("Notify On Success", string.Empty);
            s += this.form.TableHeader("Notify On Warning", string.Empty);
            s += this.form.TableHeader("Notify On Failure", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            // Declared before the try so the section is always emitted (empty rows when no data).
            var jsonRows = new List<List<string>>();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicEmailNotification().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='7' style='text-align: center; padding: 20px; color: #666;'><em>No email notification settings detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        var row = (IDictionary<string, object>)item;
                        string enabled = row.TryGetValue("enabled", out var en) ? (string)(en ?? "") : "";
                        string smtpServer = row.TryGetValue("smtpserver", out var sv) ? (string)(sv ?? "") : "";
                        string sender = row.TryGetValue("sender", out var sn) ? (string)(sn ?? "") : "";
                        string recipient = row.TryGetValue("recipient", out var rc) ? (string)(rc ?? "") : "";
                        string notifySuccess = row.TryGetValue("notifyonsuccess", out var ns) ? (string)(ns ?? "") : "";
                        string notifyWarning = row.TryGetValue("notifyonwarning", out var nw) ? (string)(nw ?? "") : "";
                        string notifyFailure = row.TryGetValue("notifyonfailure", out var nf) ? (string)(nf ?? "") : "";

                        if (scrub)
                        {
                            smtpServer = CGlobals.Scrubber.ScrubItem(smtpServer, ScrubItemType.Server);
                            sender = CGlobals.Scrubber.ScrubItem(sender, ScrubItemType.Item);
                            recipient = CGlobals.Scrubber.ScrubItem(recipient, ScrubItemType.Item);
                        }

                        s += this.form.TableData(enabled, string.Empty);
                        s += this.form.TableData(smtpServer, string.Empty);
                        s += this.form.TableData(sender, string.Empty);
                        s += this.form.TableData(recipient, string.Empty);
                        s += this.form.TableData(notifySuccess, string.Empty);
                        s += this.form.TableData(notifyWarning, string.Empty);
                        s += this.form.TableData(notifyFailure, string.Empty);

                        s += "</tr>";

                        jsonRows.Add(new List<string> { enabled, smtpServer, sender, recipient, notifySuccess, notifyWarning, notifyFailure });
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Email Notification table: " + e.Message);
            }

            s += this.form.SectionEnd();

            CHtmlTables.SetSectionPublic(
                "emailNotification",
                new List<string> { "Enabled", "SMTP Server", "Sender", "Recipient", "Notify On Success", "Notify On Warning", "Notify On Failure" },
                jsonRows,
                null);

            return s;
        }
    }
}
