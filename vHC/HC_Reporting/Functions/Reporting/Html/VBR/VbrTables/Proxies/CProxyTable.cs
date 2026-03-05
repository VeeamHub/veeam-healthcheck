// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Proxies
{
    /// <summary>
    /// Renders the Proxy table.
    /// Extracted from CHtmlTables.AddProxyTable().
    /// </summary>
    internal class CProxyTable
    {
        public CProxyTable() { }

        public string Render(bool scrub)
        {
            CHtmlFormatting form = new();
            CDataFormer df = new();
            CVbrSummaries sum = new();
            CLogger log = CGlobals.Logger;
            CScrubHandler scrubber = CGlobals.Scrubber;

            string s = form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = sum.Proxies();
            s += form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT);
            s += form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT);
            s += form.TableHeaderEnd();
            s += form.TableBodyStart();

            // CDataFormer cd = new(true);
            try
            {
                List<string[]> list = df.ProxyXmlFromCsv(scrub);

                // Check if proxy data is available
                if (list == null || list.Count == 0)
                {
                    s += "<tr><td colspan='12' style='text-align: center; padding: 20px; color: #666;'>" +
                         "<em>No proxy data available. The proxy CSV file may be missing or empty.</em>" +
                         "</td></tr>";
                    log.Warning("[CProxyTable] No proxy data found - displaying placeholder message.");
                }
                else
                {
                    foreach (var d in list)
                    {
                        s += "<tr>";
                        if (scrub)
                        {
                            s += form.TableData(scrubber.ScrubItem(d[0], ScrubItemType.Server), string.Empty); // server name
                        }
                        else
                        {
                            s += form.TableData(d[0], string.Empty);
                        }

                        s += form.TableData(d[1], string.Empty);
                        s += form.TableData(d[2], string.Empty);
                        s += form.TableData(d[3], string.Empty);
                        s += form.TableData(d[4], string.Empty);
                        s += form.TableData(d[5], string.Empty);

                        // s += _form.TableData(d[6], "");
                        if (d[6] == "True")
                        {
                            s += form.TableData(form.True, string.Empty);
                        }
                        else
                        {
                            s += form.TableData(form.False, string.Empty);
                        }

                        s += form.TableData(d[7], string.Empty);
                        s += form.TableData(d[8], string.Empty);
                        s += form.TableData(d[9], string.Empty);
                        if (scrub)
                        {
                            s += form.TableData(scrubber.ScrubItem(d[10], ScrubItemType.Server), string.Empty); // host
                        }
                        else
                        {
                            s += form.TableData(d[10], string.Empty);
                        }

                        if (d[11] == "True")
                        {
                            s += form.TableData(form.True, string.Empty);
                        }
                        else
                        {
                            s += form.TableData(form.False, string.Empty);
                        }

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("PROXY Data import failed. ERROR:");
                log.Error("\t" + e.Message);
                s += "<tr><td colspan='12' style='text-align: center; padding: 20px; color: #d9534f;'>" +
                     "<em>Error loading proxy data. See logs for details.</em>" +
                     "</td></tr>";
            }

            s += form.SectionEnd(summary);

            // JSON proxies
            try
            {
                var list = df.ProxyXmlFromCsv(scrub);
                List<string> headers = new() { "Name", "Type", "Tasks", "Cores", "Ram", "IsOnHost", "TransportMode", "NetBufferSize", "MaxConcurrentJobs", "Host", "IsHvOffload" };

                // mapping indices from original array
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d[0], d[1], d[2], d[3], d[4], d[6], d[7], d[8], d[9], d[10], d[11],
                }).ToList();
                SetSection("proxies", headers, rows, summary);
            }
            catch (Exception ex)
            {
                log.Error("Failed to capture proxies JSON section: " + ex.Message);
            }

            return s;
        }

        private static void SetSection(string key, List<string> headers, List<List<string>> rows, string summary)
        {
            if (CGlobals.FullReportJson == null)
            {
                CGlobals.FullReportJson = new();
            }

            CGlobals.FullReportJson.Sections[key] = new HtmlSection
            {
                SectionName = key,
                Headers = headers,
                Rows = rows,
            };
        }
    }
}
