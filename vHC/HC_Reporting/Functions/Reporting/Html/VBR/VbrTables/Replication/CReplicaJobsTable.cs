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
    internal class CReplicaJobsTable
    {
        private readonly CHtmlFormatting form = new();

        public CReplicaJobsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("replicajobs", "Replica Jobs", "Replica Jobs");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Job Type", string.Empty);
            s += this.form.TableHeader("Schedule Options", string.Empty);
            s += this.form.TableHeader("Restore Points", string.Empty);
            s += this.form.TableHeader("Encryption", string.Empty);
            s += this.form.TableHeader("Replica Suffix", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicReplicaJobs();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No replica jobs detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        if (scrub)
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Job);

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.jobtype ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.scheduleoptions ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.restorepoints ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.pwdkeyid ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.replicasuffix ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Replica Jobs table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
