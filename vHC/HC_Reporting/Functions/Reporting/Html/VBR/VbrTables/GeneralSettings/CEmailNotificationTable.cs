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

            s += this.form.TableHeader("Is Enabled", string.Empty);
            s += this.form.TableHeader("SMTP Server", string.Empty);
            s += this.form.TableHeader("From", string.Empty);
            s += this.form.TableHeader("To", string.Empty);
            s += this.form.TableHeader("Notify On Success", string.Empty);
            s += this.form.TableHeader("Notify On Warning", string.Empty);
            s += this.form.TableHeader("Notify On Error", string.Empty);

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

                        string smtpServer = (string)(item.smtpserver ?? "");
                        string fromAddr = (string)(item.from ?? "");
                        string toAddr = (string)(item.to ?? "");
                        if (scrub)
                        {
                            smtpServer = CGlobals.Scrubber.ScrubItem(smtpServer, ScrubItemType.Server);
                            fromAddr = CGlobals.Scrubber.ScrubItem(fromAddr, ScrubItemType.Item);
                            toAddr = CGlobals.Scrubber.ScrubItem(toAddr, ScrubItemType.Item);
                        }

                        string isEnabled = (string)(item.isenabled ?? "");
                        string notifySuccess = (string)(item.notifyonsuccess ?? "");
                        string notifyWarning = (string)(item.notifyonwarning ?? "");
                        string notifyError = (string)(item.notifyonerror ?? "");

                        s += this.form.TableData(isEnabled, string.Empty);
                        s += this.form.TableData(smtpServer, string.Empty);
                        s += this.form.TableData(fromAddr, string.Empty);
                        s += this.form.TableData(toAddr, string.Empty);
                        s += this.form.TableData(notifySuccess, string.Empty);
                        s += this.form.TableData(notifyWarning, string.Empty);
                        s += this.form.TableData(notifyError, string.Empty);

                        s += "</tr>";

                        jsonRows.Add(new List<string> { isEnabled, smtpServer, fromAddr, toAddr, notifySuccess, notifyWarning, notifyError });
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
                new List<string> { "Is Enabled", "SMTP Server", "From", "To", "Notify On Success", "Notify On Warning", "Notify On Error" },
                jsonRows,
                null);

            return s;
        }
    }
}
