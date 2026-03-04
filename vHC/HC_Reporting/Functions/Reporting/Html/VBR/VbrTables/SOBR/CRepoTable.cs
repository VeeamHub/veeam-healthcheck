using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SOBR
{
    internal class CRepoTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CRepoTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("repos", VbrLocalizationHelper.RepoTitle, VbrLocalizationHelper.RepoBtn);
            string summary = this.sum.Repos();
            s +=
this.form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT, 0) +
this.form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo0TT, 1) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT, 2) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT, 3) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT, 4) +
this.form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
this.form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT, 7) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT, 8) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT, 9) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT, 10) +
this.form.TableHeader(VbrLocalizationHelper.Repo1, VbrLocalizationHelper.Repo1TT, 11) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT, 12) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT, 13) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT, 14) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT, 15) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT, 16);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                List<CRepository> list = this.df.RepoInfoToXml(scrub);

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

                    int freeSpaceShade = 0;

                    if (d.FreeSpacePercent < 20) { freeSpaceShade = 1; }
                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.JobCount.ToString(), string.Empty);
                    s += this.form.TableData(d.MaxTasks.ToString(), string.Empty, shade);
                    s += this.form.TableData(d.Cores.ToString(), string.Empty);
                    s += this.form.TableData(d.Ram.ToString(), string.Empty);

                    s += this.BoolCell(d.IsAutoGate);

                    s += this.form.TableData(d.Host, string.Empty);
                    s += this.form.TableData(d.Path, string.Empty);
                    s += this.form.TableData(d.FreeSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.TotalSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.FreeSpacePercent.ToString(), string.Empty, freeSpaceShade);

                    if (d.IsPerVmBackupFiles)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.Warn, string.Empty);
                    }

                    s += this.BoolCell(d.IsDecompress);
                    s += this.BoolCell(d.AlignBlocks);
                    s += this.BoolCell(d.IsRotatedDrives);
                    s += this.BoolCell(d.IsImmutabilitySupported);

                    s += this.form.TableData(d.Type, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("REPO Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON repos
            try
            {
                var list = this.df.RepoInfoToXml(scrub) ?? new List<CRepository>();
                List<string> headers = new() { "Name", "JobCount", "MaxTasks", "Cores", "Ram", "IsAutoGate", "Host", "Path", "FreeSpace", "TotalSpace", "FreeSpacePercent", "IsPerVmBackupFiles", "IsDecompress", "AlignBlocks", "IsRotatedDrives", "IsImmutabilitySupported", "Type" };
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
                }).ToList();
                CHtmlTables.SetSectionPublic("repos", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture repos JSON section: " + ex.Message);
            }

            return s;
        }

        private string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);
    }
}
