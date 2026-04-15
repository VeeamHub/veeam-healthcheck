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
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Registry
{
    /// <summary>
    /// Renders the Registry Keys table.
    /// Extracted from CHtmlTables.AddRegKeysTable().
    /// </summary>
    internal class CRegKeysTable
    {
        public CRegKeysTable() { }

        public string Render(bool scrub)
        {
            CHtmlFormatting form = new();
            CDataFormer df = new();
            CVbrSummaries sum = new();
            CLogger log = CGlobals.Logger;

            string s = form.SectionStartWithButton("regkeys", VbrLocalizationHelper.RegTitle, VbrLocalizationHelper.RegBtn);
            string summary = sum.RegKeys();

            try
            {
                Dictionary<string, string> list = df.RegOptions();
                if (list.Count == 0)
                {
                    s += form.TableHeader(VbrLocalizationHelper.Reg0, VbrLocalizationHelper.Reg0TT);
                    s += form.TableHeader(VbrLocalizationHelper.Reg1, VbrLocalizationHelper.Reg1TT);
                    s += form.TableHeaderEnd();
                    s += form.TableBodyStart();
                    if (CGlobals.REMOTEEXEC) // remote exec does not support registry and VBR could be linux based without regsitry
                    {
                        s += "<tr><td colspan=\"2\">Registry key collection not supported in remote execution mode</td></tr>";
                    }
                    else
                    {
                        s += "<tr><td colspan=\"2\">No modified registry keys found</td></tr>";
                    }
                }
                else
                {
                    s +=
                    form.TableHeader(VbrLocalizationHelper.Reg0, VbrLocalizationHelper.Reg0TT) +
                    form.TableHeader(VbrLocalizationHelper.Reg1, VbrLocalizationHelper.Reg1TT);
                    s += form.TableHeaderEnd();
                    s += form.TableBodyStart();
                    foreach (var d in list)
                    {
                        s += "<tr>";
                        s += form.TableData(d.Key, string.Empty);
                        s += form.TableData(d.Value.ToString(), string.Empty);
                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Registry Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += form.SectionEnd(summary);

            // JSON reg keys
            try
            {
                var list = df.RegOptions();
                List<string> headers = new() { "Key", "Value" };
                List<List<string>> rows = list.Select(kv => new List<string> { kv.Key, kv.Value }).ToList();
                SetSection("regKeys", headers, rows, summary);
            }
            catch (Exception ex)
            {
                log.Error("Failed to capture regKeys JSON section: " + ex.Message);
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
