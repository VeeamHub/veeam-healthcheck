// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Managed_Server_Table
{
    /// <summary>
    /// Renders the Managed Servers table.
    /// Extracted from CHtmlTables.AddManagedServersTable().
    /// </summary>
    internal class CManagedServerTable
    {
        public CManagedServerTable() { }

        public string Render(bool scrub)
        {
            CHtmlFormatting form = new();
            CDataFormer df = new();
            CVbrSummaries sum = new();
            CLogger log = CGlobals.Logger;

            string s = form.SectionStartWithButton("managedServerInfo", VbrLocalizationHelper.ManSrvTitle, VbrLocalizationHelper.ManSrvBtn);
            string summary = sum.ManagedServers();
            s +=
           form.TableHeader(VbrLocalizationHelper.ManSrv0, VbrLocalizationHelper.ManSrv0TT, 0) +
           form.TableHeader(VbrLocalizationHelper.ManSrv1, VbrLocalizationHelper.ManSrv1TT, 1) +
           form.TableHeader(VbrLocalizationHelper.ManSrv2, VbrLocalizationHelper.ManSrv2TT, 2) +
           form.TableHeader(VbrLocalizationHelper.ManSrv3, VbrLocalizationHelper.ManSrv3TT, 3) +
           form.TableHeader("OS Info", VbrLocalizationHelper.ManSrv3TT, 4) +
           form.TableHeader(VbrLocalizationHelper.ManSrv4, VbrLocalizationHelper.ManSrv4TT, 5) +
           form.TableHeader(VbrLocalizationHelper.ManSrv5, VbrLocalizationHelper.ManSrv5TT, 6) +
           form.TableHeader(VbrLocalizationHelper.ManSrv6, VbrLocalizationHelper.ManSrv6TT, 7) +
           form.TableHeader(VbrLocalizationHelper.ManSrv7, VbrLocalizationHelper.ManSrv7TT, 8) +
           form.TableHeader(VbrLocalizationHelper.ManSrv8, VbrLocalizationHelper.ManSrv8TT, 9) +
           form.TableHeader(VbrLocalizationHelper.ManSrv9, VbrLocalizationHelper.ManSrv9TT, 10) +
           form.TableHeader(VbrLocalizationHelper.ManSrv10, VbrLocalizationHelper.ManSrv10TT, 11) +
           form.TableHeader(VbrLocalizationHelper.ManSrv11, VbrLocalizationHelper.ManSrv11TT, 12);
            s += form.TableHeaderEnd();
            s += form.TableBodyStart();

            // CDataFormer cd = new(true);
            try
            {
                List<CManagedServer> list = df.ServerXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    s += "<tr>";

                    s += form.TableData(d.Name, string.Empty);
                    s += form.TableData(d.Cores.ToString(), string.Empty);
                    s += form.TableData(d.Ram.ToString(), string.Empty);
                    s += form.TableData(d.Type, string.Empty);
                    s += form.TableData(d.OsInfo, string.Empty);
                    s += form.TableData(d.ApiVersion, string.Empty);
                    s += form.TableData(d.ProtectedVms.ToString(), string.Empty);
                    s += form.TableData(d.NotProtectedVms.ToString(), string.Empty);
                    s += form.TableData(d.TotalVms.ToString(), string.Empty);
                    if (d.IsProxy)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    if (d.IsRepo)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    if (d.IsWan)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    s += form.TableData(d.IsUnavailable.ToString(), string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                log.Error("Managed Server Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += form.SectionEnd(summary);

            // JSON managed servers
            try
            {
                var list = df.ServerXmlFromCsv(scrub);
                List<string> headers = new() { "Name", "Cores", "Ram", "Type", "OsInfo", "ApiVersion", "ProtectedVms", "NotProtectedVms", "TotalVms", "IsProxy", "IsRepo", "IsWan", "IsUnavailable" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.Cores.ToString(),
                    d.Ram.ToString(),
                    d.Type,
                    d.OsInfo,
                    d.ApiVersion,
                    d.ProtectedVms.ToString(),
                    d.NotProtectedVms.ToString(),
                    d.TotalVms.ToString(),
                    d.IsProxy ? "True" : "False",
                    d.IsRepo ? "True" : "False",
                    d.IsWan ? "True" : "False",
                    d.IsUnavailable.ToString(),
                }).ToList();
                SetSection("managedServers", headers, rows, summary);
            }
            catch (Exception ex)
            {
                log.Error("Failed to capture managedServers JSON section: " + ex.Message);
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
