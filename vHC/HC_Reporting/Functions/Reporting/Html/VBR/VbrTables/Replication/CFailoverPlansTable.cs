using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Replication
{
    internal class CFailoverPlansTable
    {
        private readonly CHtmlFormatting form = new();

        public CFailoverPlansTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("failoverplans", "Failover Plans", "Failover Plans");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Platform Type", string.Empty);
            s += this.form.TableHeader("VM Count", string.Empty);
            s += this.form.TableHeader("Pre-Failover Script", string.Empty);
            s += this.form.TableHeader("Post-Failover Script", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicFailoverPlans();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No failover plans detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        if (scrub)
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.description ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.platformtype ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.vmcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.prefailoverscript ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.postfailoverscript ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Failover Plans table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
