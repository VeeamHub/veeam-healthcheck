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
        private readonly CHtmlFormatting form = new();

        public string Table()
        {
            string t = string.Empty;
            try
            {
                CCsvParser c = new();
                List<CEntraTenantJobs> entraTenantJobs = new();
                List<CEntraLogJobs> entraLogJobs = new();

                try
                {
                    CGlobals.Logger.Info("Attempting to parse Entra job CSVs...", false);

                    var logJobsResult = c.GetDynamicEntraLogJobs();
                    if (logJobsResult != null)
                    {
                        entraLogJobs = logJobsResult.ToList();
                        CGlobals.Logger.Info($"Parsed {entraLogJobs.Count} Entra log jobs", false);
                    }
                    else
                    {
                        CGlobals.Logger.Info("No Entra log job CSV found", false);
                    }

                    var tenantJobsResult = c.GetDynamicEntraTenantJobs();
                    if (tenantJobsResult != null)
                    {
                        entraTenantJobs = tenantJobsResult.ToList();
                        CGlobals.Logger.Info($"Parsed {entraTenantJobs.Count} Entra tenant jobs", false);
                    }
                    else
                    {
                        CGlobals.Logger.Info("No Entra tenant job CSV found", false);
                    }
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Warning("Error parsing Entra job CSVs. Use debug logging for more details: ");
                    CGlobals.Logger.Debug("Error: " + ex.Message);
                    CGlobals.Logger.Debug("Stack trace: " + ex.StackTrace);
                    return null;
                }

                // If no Entra jobs exist, return null to skip the table
                if (entraTenantJobs.Count == 0 && entraLogJobs.Count == 0)
                {
                    CGlobals.Logger.Info("No Entra backup jobs detected", false);
                    return null;
                }

                CGlobals.Logger.Info($"Building Entra jobs table with {entraTenantJobs.Count} tenant jobs and {entraLogJobs.Count} log jobs", false);

                // Tenant Job Table
                if (entraTenantJobs.Count > 0)
                {
                    t += this.form.Table();
                    t += this.form.TableHeaderLeftAligned("Job Name (Tenant)", string.Empty);
                    t += this.form.TableHeader("Retention Policy", string.Empty);
                    t += this.form.TableBodyStart();

                    foreach (var tenantJob in entraTenantJobs)
                    {
                        CGlobals.Logger.Debug($"Processing tenant job: {tenantJob.Name}, Retention: {tenantJob.RetentionPolicy}");
                        t += "<tr>";
                        t += this.form.TableDataLeftAligned(tenantJob.Name, "colspan='2'");
                        t += this.form.TableData(tenantJob.RetentionPolicy.ToString(), string.Empty);
                        t += "</tr>";
                    }

                    t += "</tbody>";
                    t += "</table>";
                }

                // Tenant log table
                if (entraLogJobs.Count > 0)
                {
                    t += this.form.Table();
                    t += this.form.TableHeaderLeftAligned("Job Name (Logs)", string.Empty);
                    t += this.form.TableHeader("Tenant", string.Empty);
                    t += this.form.TableHeader("Short Term Retention", string.Empty);
                    t += this.form.TableHeader("Short Term Repo", string.Empty);
                    t += this.form.TableHeader("Copy Enabled", string.Empty);
                    t += this.form.TableBodyStart();

                    foreach (var tj in entraLogJobs)
                    {
                        CGlobals.Logger.Debug($"Processing log job: {tj.Name}, Tenant: {tj.Tenant}");

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
                        t += this.form.TableDataLeftAligned(jobName, string.Empty);
                        t += this.form.TableData(tenant, string.Empty);
                        t += this.form.TableData(tj.ShortTermRepoRetention.ToString(), string.Empty);
                        t += this.form.TableData(stRepo, string.Empty);
                        t += tj.CopyModeEnabled ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                        t += "</tr>";
                    }

                    t += "</tbody>";
                    t += "</table>";
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Exception in CEntraJobsTable.Table(): " + e.Message);
                CGlobals.Logger.Error("Stack trace: " + e.StackTrace);
                return null;
            }

            return t;
        }
    }
}
