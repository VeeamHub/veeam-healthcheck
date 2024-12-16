using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info
{
    internal class CEntraJobsTable
    {
        private readonly CHtmlFormatting _form = new();
        private readonly CScrubHandler _scrubber = CGlobals.Scrubber;
        public string Table()
        {
            string t = "";
            try
            {
                CCsvParser c = new();
                var entraTenantJobs = c.GetDynamicEntraTenantJobs().ToList();
                var entraLogJobs = c.GetDynamicEntraLogJobs().ToList();
                if (entraTenantJobs.Count() == 0 && entraLogJobs.Count() == 0)
                    return "";
                CGlobals.Logger.Debug("Tenant Job count = "+ entraTenantJobs.Count());
                CGlobals.Logger.Debug("Entra Log job count = " + entraLogJobs.Count());

                // Tenant Job Table
                t += _form.Table();
                t += _form.TableHeaderLeftAligned("Job Name", "");
                t += _form.TableHeader("Retention Policy", "");
                t += _form.TableBodyStart();
                foreach (var tenantJob in entraTenantJobs) {
                    CGlobals.Logger.Debug("Processing tenant job: " + tenantJob.Name);
                    CGlobals.Logger.Debug("Processing tenant job: " + tenantJob.RetentionPolicy);
                    t += "<tr>";
                    t += _form.TableDataLeftAligned(tenantJob.Name, "colspan='2'");
                    t += _form.TableData(tenantJob.RetentionPolicy.ToString(), "");
                    t += "</tr>";

                }
                // Tenant log table

                t += _form.Table();
                t += _form.TableHeaderLeftAligned("Job Name", "");
                t += _form.TableHeader("Tenant", "");
                t += _form.TableHeader("Short Term Retention", "");
                t += _form.TableHeader("Short Term Repo", "");
                t += _form.TableHeader("Copy Enabled", "");
                //t += _form.TableHeader("Secondary Target", ""); // disaabled for now because PS doesn't return right


                t += _form.TableBodyStart();
                foreach (var tj in entraLogJobs)
                {
                    string jobName = tj.Name;
                    string tenant = tj.Tenant;
                    string stRepo = tj.ShortTermRepo;
                    if (CGlobals.Scrub)
                    {
                        jobName = CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Job);
                        tenant = CGlobals.Scrubber.ScrubItem(tenant, ScrubItemType.MediaPool);
                        stRepo = CGlobals.Scrubber.ScrubItem(stRepo, ScrubItemType.MediaPool);
                    }

                    t += "<tr>";
                    t += _form.TableDataLeftAligned(jobName, "");
                    t += _form.TableData(tenant, "");
                    t += _form.TableData(tj.ShortTermRepoRetention.ToString(), "");
                    t += _form.TableData(stRepo, "");
                    t += tj.CopyModeEnabled ? _form.TableData(_form.True, "") : _form.TableData(_form.False, "");
                    t += "</tr>";


                }
                t += "</tbody>";
                t += "</table>";

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            return t;

        }
    }
}
