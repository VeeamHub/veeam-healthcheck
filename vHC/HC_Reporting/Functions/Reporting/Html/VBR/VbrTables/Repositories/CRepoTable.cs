// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Repositories
{
    /// <summary>
    /// Renders the Repository table.
    /// Extracted from CHtmlTables.AddRepoTable().
    /// </summary>
    internal class CRepoTable
    {
        public CRepoTable() { }

        public string Render(bool scrub)
        {
            CHtmlFormatting form = new();
            CDataFormer df = new();
            CVbrSummaries sum = new();
            CLogger log = CGlobals.Logger;

            string s = form.SectionStartWithButton("repos", VbrLocalizationHelper.RepoTitle, VbrLocalizationHelper.RepoBtn);
            string summary = sum.Repos();
            bool showLocation = CLocationColumn.ShowFor(() => df.RepoInfoToXml(scrub).Select(d => d.Location));
            int o = showLocation ? 1 : 0;
            s +=
form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT, 0) +
(showLocation ? CLocationColumn.Header(form, 1) : string.Empty) +
form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo0TT, 1 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT, 2 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT, 3 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT, 4 + o) +
form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT, 7 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT, 8 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT, 9 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT, 10 + o) +
form.TableHeader(VbrLocalizationHelper.Repo1, VbrLocalizationHelper.Repo1TT, 11 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT, 12 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT, 13 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT, 14 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT, 15 + o) +
form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT, 16 + o);
            s += form.TableHeaderEnd();
            s += form.TableBodyStart();
            List<CRepository> list = new();
            bool fetched = false;
            try
            {
                list = df.RepoInfoToXml(scrub) ?? new List<CRepository>();
                fetched = true;

                foreach (var d in list)
                {
                    var prov = d.Provisioning;
                    int shade = 0;
                    if (prov == "under")
                    {
                        shade = 2;
                    }

                    if (prov == "over")
                    {
                        shade = 1;
                    }

                    s += "<tr>";
                    s += form.TableData(d.Name, string.Empty);
                    if (showLocation)
                    {
                        s += CLocationColumn.Cell(form, d.Location);
                    }

                    s += form.TableData(d.JobCount.ToString(), string.Empty);
                    s += form.TableData(d.MaxTasks.ToString(), string.Empty, shade);
                    s += form.TableData(d.Cores.ToString(), string.Empty);
                    s += form.TableData(d.Ram.ToString(), string.Empty);
                    if (d.IsAutoGate)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    s += form.TableData(form.RenderMultiValueHtml(d.Host), string.Empty);
                    s += form.TableData(d.Path, string.Empty);
                    s += form.TableData(d.FreeSpace.ToString(), string.Empty);
                    s += form.TableData(d.TotalSpace.ToString(), string.Empty);
                    s += RenderStorageProgressBar(d.FreeSpacePercent);
                    if (d.IsPerVmBackupFiles)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.Warn, string.Empty);
                    }

                    if (d.IsDecompress)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    if (d.AlignBlocks)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    if (d.IsRotatedDrives)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    if (d.IsImmutabilitySupported)
                    {
                        s += form.TableData(form.True, string.Empty);
                    }
                    else
                    {
                        s += form.TableData(form.False, string.Empty);
                    }

                    s += form.TableData(d.Type, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                log.Error("REPO Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += form.SectionEndWithEmptyState(s, summary);

            // JSON repos
            if (fetched)
            {
                try
                {
                    List<string> headers = new() { "Name", "JobCount", "MaxTasks", "Cores", "Ram", "IsAutoGate", "Host", "Path", "FreeSpace", "TotalSpace", "FreeSpacePercent", "IsPerVmBackupFiles", "IsDecompress", "AlignBlocks", "IsRotatedDrives", "IsImmutabilitySupported", "Type", "Location" };
                    List<List<string>> rows = list.Select(d => new List<string>
                    {
                        d.Name,
                        d.JobCount.ToString(),
                        d.MaxTasks.ToString(),
                        d.Cores.ToString(),
                        d.Ram.ToString(),
                        d.IsAutoGate ? "True" : "False",
                        d.Host,
                        d.Path,
                        d.FreeSpace.ToString(),
                        d.TotalSpace.ToString(),
                        d.FreeSpacePercent.ToString(),
                        d.IsPerVmBackupFiles ? "True" : "False",
                        d.IsDecompress ? "True" : "False",
                        d.AlignBlocks ? "True" : "False",
                        d.IsRotatedDrives ? "True" : "False",
                        d.IsImmutabilitySupported ? "True" : "False",
                        d.Type,
                        d.Location ?? string.Empty,
                    }).ToList();
                    CHtmlTables.SetSectionPublic("repos", headers, rows, summary);
                }
                catch (Exception ex)
                {
                    log.Error("Failed to capture repos JSON section: " + ex.Message);
                }
            }

            return s;
        }

        private static string RenderStorageProgressBar(decimal freePercent)
        {
            decimal usedPercent = Math.Max(0, Math.Min(100, 100 - freePercent));
            string colorClass = usedPercent >= 80 ? "progress-danger"
                              : usedPercent >= 60 ? "progress-warning"
                              : "progress-ok";
            return $@"<td><div class=""progress-bar""><div class=""progress-track""><div class=""progress-fill {colorClass}"" style=""width:{usedPercent:F0}%""></div></div><div class=""progress-label"">{freePercent:F0}% free</div></div></td>";
        }
    }
}
