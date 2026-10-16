using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudFailoverPlanObjectsTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudFailoverPlanObjectsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudfailoverplanobjects", "Cloud Failover Plan VMs", "Cloud Failover Plan VMs");

            s += this.form.TableHeaderLeftAligned("Failover Plan", string.Empty);
            s += this.form.TableHeader("VM Name", string.Empty);
            s += this.form.TableHeader("Boot Order", string.Empty);
            s += this.form.TableHeader("Boot Delay (sec)", string.Empty);
            s += this.form.TableHeader("Public IP Rule", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudFailoverPlanObjects().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='5' style='text-align: center; padding: 20px; color: #666;'><em>No cloud failover plan VMs detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string planName = (string)(item.failoverplanname ?? "");
                        string vmName = (string)(item.vmname ?? "");
                        if (scrub)
                        {
                            planName = CGlobals.Scrubber.ScrubItem(planName, ScrubItemType.Item);
                            vmName = CGlobals.Scrubber.ScrubItem(vmName, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(planName, string.Empty);
                        s += this.form.TableData(vmName, string.Empty);
                        s += this.form.TableData((string)(item.bootorder ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.bootdelay ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.publiciprule ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Failover Plan VMs table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
