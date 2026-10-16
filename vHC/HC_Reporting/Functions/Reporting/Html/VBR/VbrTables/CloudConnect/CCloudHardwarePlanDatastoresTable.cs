using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudHardwarePlanDatastoresTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudHardwarePlanDatastoresTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudhardwareplandatastores", "Cloud Hardware Plan Datastores", "Cloud HW Plan Datastores");

            s += this.form.TableHeaderLeftAligned("Hardware Plan", string.Empty);
            s += this.form.TableHeader("Datastore Friendly Name", string.Empty);
            s += this.form.TableHeader("Datastore Path", string.Empty);
            s += this.form.TableHeader("Quota (GB)", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudHardwarePlanDatastores().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='4' style='text-align: center; padding: 20px; color: #666;'><em>No cloud hardware plan datastores detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string planName = (string)(item.hardwareplanname ?? "");
                        string friendlyName = (string)(item.datastorefriendlyname ?? "");
                        string path = (string)(item.datastorepath ?? "");
                        if (scrub)
                        {
                            planName = CGlobals.Scrubber.ScrubItem(planName, ScrubItemType.Item);
                            friendlyName = CGlobals.Scrubber.ScrubItem(friendlyName, ScrubItemType.Item);
                            path = CGlobals.Scrubber.ScrubItem(path, ScrubItemType.Path);
                        }

                        s += this.form.TableDataLeftAligned(planName, string.Empty);
                        s += this.form.TableData(friendlyName, string.Empty);
                        s += this.form.TableData(path, string.Empty);
                        s += this.form.TableData((string)(item.quotagb ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Hardware Plan Datastores table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
