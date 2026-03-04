using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SOBR
{
    internal class CArchiveTierExtTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CLogger log = CGlobals.Logger;

        public CArchiveTierExtTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("archextents", "Archive Tier Configuration", "Archive");
            string summary = "Archive tier extent retention and immutability policies";
            s += "<tr>" +
                this.form.TableHeader("Name", "Name of archive tier repository/bucket") +
                this.form.TableHeader("SOBR Name", "Scale-Out Backup Repository name") +
                this.form.TableHeader("Status", "Archive extent status") +
                this.form.TableHeader("Auto Gateway / Direct Connection", "Gateway connection mode (e.g., Direct, Gateway)") +
                this.form.TableHeader("Specified Gateway(s)", "Gateway servers assigned to this archive extent") +
                this.form.TableHeader("Offload Period", "How long before data is offloaded to archive") +
                this.form.TableHeader("Archive Tier Enabled", "Whether archive tier is active") +
                this.form.TableHeader("Cost Optimized", "Cost optimization policy enabled") +
                this.form.TableHeader("Full Backup Mode", "Full backup mode for archive tier") +
                this.form.TableHeader("Encryption", "Whether encryption is enabled for archive tier") +
                this.form.TableHeader("Use Immutability", "Whether immutability is enforced on archive") +
                this.form.TableHeader("Type", "Archive repository type (e.g., AmazonS3, AzureBlob)") +
                "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            List<CArchiveTierExtent> list = new();
            try
            {
                this.log.Info("Attempting to load Archive Tier Extent data...");
                list = this.df.ArchiveTierXmlFromCsv(scrub) ?? list;

                if (list.Count == 0)
                {
                    this.log.Warning("No Archive Tier Extent data found. No SOBRs with archive tier may be configured.");
                }

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.SobrName, string.Empty);
                    s += this.form.TableData(CHtmlTables.Badge(d.Status), string.Empty);
                    s += this.form.TableData(d.GatewayMode ?? string.Empty, string.Empty);
                    s += this.form.TableData(d.GatewayServer ?? string.Empty, string.Empty);
                    s += this.form.TableData(d.OffloadPeriod ?? string.Empty, string.Empty);
                    s += this.BoolCell(d.ArchiveTierEnabled);
                    s += this.BoolCell(d.CostOptimizedEnabled);
                    s += this.BoolCell(d.FullBackupModeEnabled);
                    s += this.BoolCell(d.EncryptionEnabled);
                    s += this.BoolCell(d.ImmutableEnabled);
                    s += this.form.TableData(d.Type ?? string.Empty, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("Archive Tier Extent Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON Archive Tier Extents
            try
            {
                List<string> headers = new() { "Name", "SobrName", "Status", "GatewayMode", "GatewayServer", "OffloadPeriod", "ArchiveTierEnabled", "CostOptimizedEnabled", "FullBackupModeEnabled", "EncryptionEnabled", "ImmutableEnabled", "Type" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.SobrName,
                    d.Status,
                    d.GatewayMode ?? string.Empty,
                    d.GatewayServer ?? string.Empty,
                    d.OffloadPeriod ?? string.Empty,
                    d.ArchiveTierEnabled ? "True" : "False",
                    d.CostOptimizedEnabled ? "True" : "False",
                    d.FullBackupModeEnabled ? "True" : "False",
                    d.EncryptionEnabled ? "True" : "False",
                    d.ImmutableEnabled ? "True" : "False",
                    d.Type ?? string.Empty,
                }).ToList();
                CHtmlTables.SetSectionPublic("archextents", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture archive tier extents JSON section: " + ex.Message);
            }

            return s;
        }

        private string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);
    }
}
