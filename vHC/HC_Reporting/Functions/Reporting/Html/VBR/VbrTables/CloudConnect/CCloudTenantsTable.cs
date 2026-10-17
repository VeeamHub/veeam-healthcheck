using System;
using System.Linq;
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
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Last Active", string.Empty);
            s += this.form.TableHeader("Last Result", string.Empty);
            s += this.form.TableHeader("VMs", string.Empty);
            s += this.form.TableHeader("Servers", string.Empty);
            s += this.form.TableHeader("Workstations", string.Empty);
            s += this.form.TableHeader("Replicas", string.Empty);
            s += this.form.TableHeader("New VM Backups", string.Empty);
            s += this.form.TableHeader("New Servers", string.Empty);
            s += this.form.TableHeader("New Workstations", string.Empty);
            s += this.form.TableHeader("New Replicas", string.Empty);
            s += this.form.TableHeader("Rental VMs", string.Empty);
            s += this.form.TableHeader("Rental Servers", string.Empty);
            s += this.form.TableHeader("Rental Workstations", string.Empty);
            s += this.form.TableHeader("Rental Replicas", string.Empty);
            s += this.form.TableHeader("Max Concurrent Tasks", string.Empty);
            s += this.form.TableHeader("Throttling Enabled", string.Empty);
            s += this.form.TableHeader("Max Bandwidth", string.Empty);
            s += this.form.TableHeader("Bandwidth Unit", string.Empty);
            s += this.form.TableHeader("Gateway Selection", string.Empty);
            s += this.form.TableHeader("Gateway Pool", string.Empty);
            s += this.form.TableHeader("Gateway Failover", string.Empty);
            s += this.form.TableHeader("Lease Expiration", string.Empty);
            s += this.form.TableHeader("Lease Expiry Date", string.Empty);
            s += this.form.TableHeader("Backup Protection", string.Empty);
            s += this.form.TableHeader("Protection Period (days)", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudTenants().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='29' style='text-align: center; padding: 20px; color: #666;'><em>No cloud tenants detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string description = (string)(item.description ?? "");
                        string gatewayPoolName = (string)(item.gatewaypoolname ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            description = CGlobals.Scrubber.ScrubItem(description, ScrubItemType.Item);
                            gatewayPoolName = CGlobals.Scrubber.ScrubItem(gatewayPoolName, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(description, string.Empty);
                        s += this.form.TableData((string)(item.enabled ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.type ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.lastactive ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.lastresult ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.vmcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.servercount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.workstationcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.replicacount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.newvmbackupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.newserverbackupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.newworkstationbackupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.newreplicacount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.rentalvmbackupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.rentalserverbackupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.rentalworkstationbackupcount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.rentalreplicacount ?? ""), string.Empty);
                        s += this.form.TableData(CCloudConnectHelpers.FormatMaxConcurrent((string)(item.maxconcurrenttask ?? "")), string.Empty);
                        string throttlingEnabled = (string)(item.throttlingenabled ?? "");
                        s += this.form.TableData(throttlingEnabled, string.Empty);
                        s += this.form.TableData(CCloudConnectHelpers.FormatThrottleField(throttlingEnabled, (string)(item.throttlingvalue ?? "")), string.Empty);
                        s += this.form.TableData(CCloudConnectHelpers.FormatThrottleField(throttlingEnabled, (string)(item.throttlingunit ?? "")), string.Empty);
                        s += this.form.TableData((string)(item.gatewayselectiontype ?? ""), string.Empty);
                        s += this.form.TableData(gatewayPoolName, string.Empty);
                        s += this.form.TableData((string)(item.gatewayfailoverenabled ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.leaseexpirationenabled ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.leaseexpirationdate ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.backupprotectionenabled ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.backupprotectionperiod ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Tenants table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }

    }
}
