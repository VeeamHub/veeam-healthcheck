using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SOBR
{
    internal class CSobrTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CSobrTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("sobr", VbrLocalizationHelper.SbrTitle, VbrLocalizationHelper.SbrBtn);
            string summary = this.sum.Sobr();
            s += "<tr>" +
           this.form.TableHeader(VbrLocalizationHelper.Sbr0, VbrLocalizationHelper.Sbr0TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr1, VbrLocalizationHelper.Sbr1TT) +
           this.form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo1TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr2, VbrLocalizationHelper.Sbr2TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr3, VbrLocalizationHelper.Sbr3TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr4, VbrLocalizationHelper.Sbr4TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr5, VbrLocalizationHelper.Sbr5TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr6, VbrLocalizationHelper.Sbr6TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr7, VbrLocalizationHelper.Sbr7TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr8, VbrLocalizationHelper.Sbr8TT) +
           this.form.TableHeader("CapTier Immutable", VbrLocalizationHelper.Sbr9TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr10, VbrLocalizationHelper.Sbr10TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr11, VbrLocalizationHelper.Sbr11TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr12, VbrLocalizationHelper.Sbr12TT) +
           "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                this.log.Info("Attempting to load SOBR data...");
                List<CSobrTypeInfos> list = this.df.SobrInfoToXml(scrub);

                if (list == null || list.Count == 0)
                {
                    this.log.Warning("No SOBR data found. The SOBRs CSV file may be missing or empty.");
                    this.log.Info("This could indicate: 1) No SOBRs configured, 2) Collection script failed, or 3) CSV file not found");
                }

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.ExtentCount.ToString(), string.Empty);
                    s += this.form.TableData(d.JobCount.ToString(), string.Empty);
                    s += this.form.TableData(d.PolicyType, string.Empty);
                    s += this.BoolCell(d.EnableCapacityTier);
                    s += this.BoolCell(d.CapacityTierCopyPolicyEnabled);
                    s += this.BoolCell(d.CapacityTierMovePolicyEnabled);
                    s += this.BoolCell(d.ArchiveTierEnabled);
                    s += this.BoolCell(d.UsePerVMBackupFiles);
                    s += this.form.TableData(d.CapTierType, string.Empty);
                    s += this.BoolCell(d.ImmuteEnabled);
                    s += this.form.TableData(d.ImmutePeriod, string.Empty);
                    s += this.BoolCell(d.SizeLimitEnabled);
                    s += this.form.TableData(d.SizeLimit, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("SOBR Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON SOBR
            try
            {
                var list = this.df.SobrInfoToXml(scrub) ?? new List<CSobrTypeInfos>();
                List<string> headers = new() { "Name", "ExtentCount", "JobCount", "PolicyType", "EnableCapacityTier", "CapacityTierCopy", "CapacityTierMove", "ArchiveTierEnabled", "UsePerVMFiles", "CapTierType", "ImmutableEnabled", "ImmutablePeriod", "SizeLimitEnabled", "SizeLimit" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.ExtentCount.ToString(),
                    d.JobCount.ToString(),
                    d.PolicyType,
                    d.EnableCapacityTier ? "True" : "False",
                    d.CapacityTierCopyPolicyEnabled ? "True" : "False",
                    d.CapacityTierMovePolicyEnabled ? "True" : "False",
                    d.ArchiveTierEnabled ? "True" : "False",
                    d.UsePerVMBackupFiles ? "True" : "False",
                    d.CapTierType,
                    d.ImmuteEnabled ? "True" : "False",
                    d.ImmutePeriod,
                    d.SizeLimitEnabled ? "True" : "False",
                    d.SizeLimit,
                }).ToList();
                CHtmlTables.SetSectionPublic("sobr", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture sobr JSON section: " + ex.Message);
            }

            return s;
        }

        private string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);
    }
}
