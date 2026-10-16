// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudTenantPerformanceTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudTenantPerformanceTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudtenantperf", "Tenant Performance Settings", "Tenant Performance Settings");

            s += this.form.TableHeaderLeftAligned("Tenant", string.Empty);
            s += this.form.TableHeader("Enabled", string.Empty);
            s += this.form.TableHeader("Max Concurrent Tasks", string.Empty);
            s += this.form.TableHeader("Bandwidth Throttling", string.Empty);
            s += this.form.TableHeader("Max Bandwidth", string.Empty);
            s += this.form.TableHeader("Bandwidth Unit", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudTenants().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No cloud tenants detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                        }

                        string throttlingEnabled = (string)(item.throttlingenabled ?? "");

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.enabled ?? ""), string.Empty);
                        s += this.form.TableData(CCloudConnectHelpers.FormatMaxConcurrent((string)(item.maxconcurrenttask ?? "")), string.Empty);
                        s += this.form.TableData(throttlingEnabled, string.Empty);
                        s += this.form.TableData(CCloudConnectHelpers.FormatThrottleField(throttlingEnabled, (string)(item.throttlingvalue ?? "")), string.Empty);
                        s += this.form.TableData(CCloudConnectHelpers.FormatThrottleField(throttlingEnabled, (string)(item.throttlingunit ?? "")), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Tenant Performance table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }

    }
}
