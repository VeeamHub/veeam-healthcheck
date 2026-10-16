using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudGatewayPoolsTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudGatewayPoolsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudgatewaypools", "Cloud Gateway Pools", "Cloud Gateway Pools");

            s += this.form.TableHeaderLeftAligned("Pool Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Member Gateway", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudGatewayPools().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='3' style='text-align: center; padding: 20px; color: #666;'><em>No cloud gateway pools detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string poolName = (string)(item.poolname ?? "");
                        string description = (string)(item.description ?? "");
                        string gatewayName = (string)(item.gatewayname ?? "");
                        if (scrub)
                        {
                            poolName = CGlobals.Scrubber.ScrubItem(poolName, ScrubItemType.Item);
                            description = CGlobals.Scrubber.ScrubItem(description, ScrubItemType.Item);
                            gatewayName = CGlobals.Scrubber.ScrubItem(gatewayName, ScrubItemType.Server);
                        }

                        s += this.form.TableDataLeftAligned(poolName, string.Empty);
                        s += this.form.TableData(description, string.Empty);
                        s += this.form.TableData(gatewayName, string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Gateway Pools table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
