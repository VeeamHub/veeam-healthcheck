using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Replication
{
    internal class CReplicasTable
    {
        private readonly CHtmlFormatting form = new();

        public CReplicasTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("replicas", "Replicas", "Replicas");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("VM Name", string.Empty);
            s += this.form.TableHeader("Job Name", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Restore Points Count", string.Empty);
            s += this.form.TableHeader("Last Restore Point", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicReplicas();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No replicas detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string vmName = (string)(item.vmname ?? "");
                        string jobName = (string)(item.jobname ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            vmName = CGlobals.Scrubber.ScrubItem(vmName, ScrubItemType.VM);
                            jobName = CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Job);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(vmName, string.Empty);
                        s += this.form.TableData(jobName, string.Empty);
                        s += this.form.TableData((string)(item.type ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.restorepointscount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.lastrestorepoint ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Replicas table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
