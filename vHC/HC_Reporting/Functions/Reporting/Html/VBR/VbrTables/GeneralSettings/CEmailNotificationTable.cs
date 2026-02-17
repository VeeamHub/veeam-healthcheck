using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings
{
    internal class CEmailNotificationTable
    {
        private readonly CHtmlFormatting form = new();

        public CEmailNotificationTable() { }

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

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicEmailNotification();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='7' style='text-align: center; padding: 20px; color: #666;'><em>No email notification settings detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string smtpServer = (string)(item.smtpserver ?? "");
                        if (scrub)
                            smtpServer = CGlobals.Scrubber.ScrubItem(smtpServer, ScrubItemType.Server);

                        s += this.form.TableData((string)(item.isenabled ?? ""), string.Empty);
                        s += this.form.TableData(smtpServer, string.Empty);
                        s += this.form.TableData((string)(item.from ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.to ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.notifyonsuccess ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.notifyonwarning ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.notifyonerror ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Email Notification table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
