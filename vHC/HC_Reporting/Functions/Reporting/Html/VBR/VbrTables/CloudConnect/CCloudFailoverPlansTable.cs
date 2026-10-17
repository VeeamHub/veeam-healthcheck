using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudFailoverPlansTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudFailoverPlansTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudfailoverplans", "Cloud Failover Plans", "Cloud Failover Plans");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Platform", string.Empty);
            s += this.form.TableHeader("Status", string.Empty);
            s += this.form.TableHeader("VM Count", string.Empty);
            s += this.form.TableHeader("Pre-Failover Command", string.Empty);
            s += this.form.TableHeader("Post-Failover Command", string.Empty);
            s += this.form.TableHeader("Public IP Enabled", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudFailoverPlans().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='9' style='text-align: center; padding: 20px; color: #666;'><em>No cloud failover plans detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string description = (string)(item.description ?? "");
                        string preCmd = (string)(item.prefailovercommand ?? "");
                        string postCmd = (string)(item.postfailovercommand ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            description = CGlobals.Scrubber.ScrubItem(description, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(description, string.Empty);
                        s += this.form.TableData((string)(item.type ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.platform ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.status ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.vmcount ?? ""), string.Empty);
                        s += this.form.TableData(preCmd, string.Empty);
                        s += this.form.TableData(postCmd, string.Empty);
                        s += this.form.TableData((string)(item.publicipenabled ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Failover Plans table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
