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
    internal class CSobrExtentTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CSobrExtentTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("extents", VbrLocalizationHelper.SbrExtTitle, VbrLocalizationHelper.SbrExtBtn);
            string summary = this.sum.Extents();
            s += "<tr>" +
this.form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt1, VbrLocalizationHelper.SbrExt1TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT) +
this.form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
this.form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT) +
"</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                this.log.Info("Attempting to load SOBR Extent data...");
                List<CRepository> list = this.df.ExtentXmlFromCsv(scrub);

                if (list == null || list.Count == 0)
                {
                    this.log.Warning("No SOBR Extent data found. The SOBRExtents CSV file may be missing or empty.");
                    this.log.Info("This could indicate: 1) No SOBRs configured, 2) Collection script failed, or 3) CSV file not found");
                }

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
                    s += this.form.TableData(d.SobrName, string.Empty);
                    s += this.form.TableData(d.MaxTasks.ToString(), string.Empty, shade);
                    s += this.form.TableData(d.Cores.ToString(), string.Empty);
                    s += this.form.TableData(d.Ram.ToString(), string.Empty);

                    s += this.BoolCell(d.IsAutoGate);

                    s += this.form.TableData(d.Host, string.Empty);
                    s += this.form.TableData(d.Path, string.Empty);
                    s += this.form.TableData(d.FreeSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.TotalSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.FreeSpacePercent.ToString(), string.Empty, freeSpaceShade);

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
                this.log.Error("Extents Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON extents
            try
            {
                var list = this.df.ExtentXmlFromCsv(scrub) ?? new List<CRepository>();
                List<string> headers = new() { "Name", "SobrName", "MaxTasks", "Cores", "Ram", "IsAutoGate", "Host", "Path", "FreeSpace", "TotalSpace", "FreeSpacePercent", "IsDecompress", "AlignBlocks", "IsRotatedDrives", "IsImmutabilitySupported", "Type" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.SobrName,
                    d.MaxTasks.ToString(),
                    d.Cores.ToString(),
                    d.Ram.ToString(),
                    d.IsAutoGate ? "True" : "False",
                    d.Host,
                    d.Path,
                    d.FreeSpace.ToString(),
                    d.TotalSpace.ToString(),
                    d.FreeSpacePercent.ToString(),
                    d.IsDecompress ? "True" : "False",
                    d.AlignBlocks ? "True" : "False",
                    d.IsRotatedDrives ? "True" : "False",
                    d.IsImmutabilitySupported ? "True" : "False",
                    d.Type,
                }).ToList();
                CHtmlTables.SetSectionPublic("extents", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture extents JSON section: " + ex.Message);
            }

            return s;
        }

        private string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);
    }
}
