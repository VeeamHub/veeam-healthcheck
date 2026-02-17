using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudGatewaysTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudGatewaysTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudgateways", "Cloud Gateways", "Cloud Gateways");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("IP Address", string.Empty);
            s += this.form.TableHeader("Incoming Port", string.Empty);
            s += this.form.TableHeader("Is Enabled", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudGateways();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='5' style='text-align: center; padding: 20px; color: #666;'><em>No cloud gateways detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string ipAddress = (string)(item.ipaddress ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Server);
                            ipAddress = CGlobals.Scrubber.ScrubItem(ipAddress, ScrubItemType.Server);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.description ?? ""), string.Empty);
                        s += this.form.TableData(ipAddress, string.Empty);
                        s += this.form.TableData((string)(item.incomingport ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.isenabled ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Gateways table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
