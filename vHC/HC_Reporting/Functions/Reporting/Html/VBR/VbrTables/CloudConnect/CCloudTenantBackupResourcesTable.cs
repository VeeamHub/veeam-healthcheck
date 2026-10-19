using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudTenantBackupResourcesTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudTenantBackupResourcesTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudtenantbackup", "Cloud Tenant Backup Storage", "Cloud Tenant Backup Storage");

            s += this.form.TableHeaderLeftAligned("Tenant", string.Empty);
            s += this.form.TableHeader("Friendly Name", string.Empty);
            s += this.form.TableHeader("Repository", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Quota (GB)", string.Empty);
            s += this.form.TableHeader("Used (GB)", string.Empty);
            s += this.form.TableHeader("Free (GB)", string.Empty);
            s += this.form.TableHeader("Used %", string.Empty);
            s += this.form.TableHeader("Quota Path", string.Empty);
            s += this.form.TableHeader("Perf Tier Used (GB)", string.Empty);
            s += this.form.TableHeader("Cap Tier Used (GB)", string.Empty);
            s += this.form.TableHeader("Archive Tier Used (GB)", string.Empty);
            s += this.form.TableHeader("WAN Accel", string.Empty);
            s += this.form.TableHeader("WAN Accel Name", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudTenantBackupResources().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='14' style='text-align: center; padding: 20px; color: #666;'><em>No cloud tenant backup resources detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string tenantName = (string)(item.tenantname ?? "");
                        string friendlyName = (string)(item.repositoryfriendlyname ?? "");
                        string repoName = (string)(item.repositoryname ?? "");
                        string quotaPath = (string)(item.repositoryquotapath ?? "");
                        string wanAccelName = (string)(item.wanacceleratorname ?? "");
                        if (scrub)
                        {
                            tenantName = CGlobals.Scrubber.ScrubItem(tenantName, ScrubItemType.Item);
                            friendlyName = CGlobals.Scrubber.ScrubItem(friendlyName, ScrubItemType.Item);
                            repoName = CGlobals.Scrubber.ScrubItem(repoName, ScrubItemType.Item);
                            quotaPath = CGlobals.Scrubber.ScrubItem(quotaPath, ScrubItemType.Path);
                            wanAccelName = CGlobals.Scrubber.ScrubItem(wanAccelName, ScrubItemType.Server);
                        }

                        // Convert MB→GB (divide by 1024)
                        static string MbToGb(object raw)
                        {
                            if (raw == null) return "";
                            if (double.TryParse(raw.ToString(), out double mb))
                                return Math.Round(mb / 1024.0, 2).ToString();
                            return raw.ToString();
                        }

                        s += this.form.TableDataLeftAligned(tenantName, string.Empty);
                        s += this.form.TableData(friendlyName, string.Empty);
                        s += this.form.TableData(repoName, string.Empty);
                        s += this.form.TableData((string)(item.repositorytype ?? ""), string.Empty);
                        s += this.form.TableData(MbToGb(item.repositoryquotamb), string.Empty);
                        s += this.form.TableData(MbToGb(item.usedspacemb), string.Empty);
                        s += this.form.TableData(MbToGb(item.freespacemb), string.Empty);
                        s += this.form.TableData((string)(item.usedspacepercentage ?? ""), string.Empty);
                        s += this.form.TableData(quotaPath, string.Empty);
                        s += this.form.TableData(MbToGb(item.performancetierusedmb), string.Empty);
                        s += this.form.TableData(MbToGb(item.capacitytierusedmb), string.Empty);
                        s += this.form.TableData(MbToGb(item.archivetierusedmb), string.Empty);
                        s += this.form.TableData((string)(item.wanaccelerationenabled ?? ""), string.Empty);
                        s += this.form.TableData(wanAccelName, string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Tenant Backup Resources table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
