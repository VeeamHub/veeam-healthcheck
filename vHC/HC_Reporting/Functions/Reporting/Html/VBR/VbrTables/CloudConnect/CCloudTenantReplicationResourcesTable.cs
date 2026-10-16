using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudTenantReplicationResourcesTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudTenantReplicationResourcesTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudtenantreplica", "Cloud Tenant Replica Resources", "Cloud Tenant Replica Resources");

            s += this.form.TableHeaderLeftAligned("Tenant", string.Empty);
            s += this.form.TableHeader("Hardware Plan", string.Empty);
            s += this.form.TableHeader("Used CPU (MHz)", string.Empty);
            s += this.form.TableHeader("Used Memory (MB)", string.Empty);
            s += this.form.TableHeader("Datastore", string.Empty);
            s += this.form.TableHeader("Datastore Quota (GB)", string.Empty);
            s += this.form.TableHeader("Datastore Used (GB)", string.Empty);
            s += this.form.TableHeader("CPU Quota", string.Empty);
            s += this.form.TableHeader("Memory Quota", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudTenantReplicationResources().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='9' style='text-align: center; padding: 20px; color: #666;'><em>No cloud tenant replication resources detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string tenantName = (string)(item.tenantname ?? "");
                        string hwPlanName = (string)(item.hardwareplanname ?? "");
                        string datastoreName = (string)(item.datastorefriendlyname ?? "");
                        if (scrub)
                        {
                            tenantName = CGlobals.Scrubber.ScrubItem(tenantName, ScrubItemType.Item);
                            hwPlanName = CGlobals.Scrubber.ScrubItem(hwPlanName, ScrubItemType.Item);
                            datastoreName = CGlobals.Scrubber.ScrubItem(datastoreName, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(tenantName, string.Empty);
                        s += this.form.TableData(hwPlanName, string.Empty);
                        s += this.form.TableData((string)(item.usedcpu ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.usedmemorymb ?? ""), string.Empty);
                        s += this.form.TableData(datastoreName, string.Empty);
                        s += this.form.TableData((string)(item.datastorequotagb ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.datastoreusedspacegb ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.cpuquota ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.memoryquota ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Tenant Replication Resources table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
