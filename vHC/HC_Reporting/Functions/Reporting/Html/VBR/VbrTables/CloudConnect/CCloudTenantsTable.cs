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
    internal class CCloudTenantsTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudTenantsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudtenants", "Cloud Tenants", "Cloud Tenants");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Enabled", string.Empty);
            s += this.form.TableHeader("Lease Expiration", string.Empty);
            s += this.form.TableHeader("Backup Count", string.Empty);
            s += this.form.TableHeader("Replica Count", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudTenants();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No cloud tenants detected.</em></td></tr>";
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
                        s += this.form.TableData((string)(item.enabled ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.leaseexpiration ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.backupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.replicacount ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Tenants table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
