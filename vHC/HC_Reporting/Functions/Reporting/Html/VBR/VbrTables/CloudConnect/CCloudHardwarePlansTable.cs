using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudHardwarePlansTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudHardwarePlansTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudhardwareplans", "Cloud Hardware Plans (Replica Resources)", "Cloud Hardware Plans");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Platform", string.Empty);
            s += this.form.TableHeader("CPU (MHz)", string.Empty);
            s += this.form.TableHeader("Memory (MB)", string.Empty);
            s += this.form.TableHeader("Networks w/ Internet", string.Empty);
            s += this.form.TableHeader("Networks w/o Internet", string.Empty);
            s += this.form.TableHeader("Subscribers", string.Empty);
            s += this.form.TableHeader("Total Datastore Quota (GB)", string.Empty);
            s += this.form.TableHeader("Host", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudHardwarePlans().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='9' style='text-align: center; padding: 20px; color: #666;'><em>No cloud hardware plans detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string hostName = (string)(item.hostname ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            hostName = CGlobals.Scrubber.ScrubItem(hostName, ScrubItemType.Server);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.platform ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.cpumhz ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.memorymb ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.networkswithinternet ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.networkswithoutinternet ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.subscribedtenantcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.totaldatastorequotagb ?? ""), string.Empty);
                        s += this.form.TableData(hostName, string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Hardware Plans table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
