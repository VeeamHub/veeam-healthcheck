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
    internal class CCredentialsTable
    {
        private readonly CHtmlFormatting form = new();

        public CCredentialsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("credentials", "Credentials", "Credentials");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("User Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Last Modified", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCredentials();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='4' style='text-align: center; padding: 20px; color: #666;'><em>No credentials detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string userName = (string)(item.username ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            userName = CGlobals.Scrubber.ScrubItem(userName, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(userName, string.Empty);
                        s += this.form.TableData((string)(item.description ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.lastmodified ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Credentials table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
