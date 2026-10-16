using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudReplicasTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudReplicasTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudreplicas", "Cloud Replicas", "Cloud Replicas");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Job Name", string.Empty);
            s += this.form.TableHeader("Status", string.Empty);
            s += this.form.TableHeader("Restore Points", string.Empty);
            s += this.form.TableHeader("Original Location", string.Empty);
            s += this.form.TableHeader("Replica Location", string.Empty);
            s += this.form.TableHeader("Platform", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudReplicas().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='7' style='text-align: center; padding: 20px; color: #666;'><em>No cloud replicas detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string jobName = (string)(item.jobname ?? "");
                        string origLoc = (string)(item.originallocation ?? "");
                        string replicaLoc = (string)(item.replicalocation ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            jobName = CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Item);
                            origLoc = CGlobals.Scrubber.ScrubItem(origLoc, ScrubItemType.Item);
                            replicaLoc = CGlobals.Scrubber.ScrubItem(replicaLoc, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(jobName, string.Empty);
                        s += this.form.TableData((string)(item.status ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.restorepointcount ?? ""), string.Empty);
                        s += this.form.TableData(origLoc, string.Empty);
                        s += this.form.TableData(replicaLoc, string.Empty);
                        s += this.form.TableData((string)(item.platform ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Replicas table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
