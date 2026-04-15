using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Html.VBR;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SOBR
{
    internal class CArchiveTierExtTable : CSectionTableBase<CArchiveTierExtent>
    {
        private readonly CDataFormer df = new();

        protected override string SectionId => "archextents";
        protected override string Title => "Archive Tier Configuration";
        protected override string ButtonText => "Archive";

        protected override string GetSummary() => "Archive tier extent retention and immutability policies";

        protected override string RenderHeaders() =>
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
            this.form.TableHeader("Type", "Archive repository type (e.g., AmazonS3, AzureBlob)");

        protected override List<CArchiveTierExtent> LoadData(bool scrub) => this.df.ArchiveTierXmlFromCsv(scrub);

        protected override string RenderRow(CArchiveTierExtent d) =>
            "<tr>" +
            this.form.TableData(d.Name, string.Empty) +
            this.form.TableData(d.SobrName, string.Empty) +
            this.form.TableData(CHtmlTables.Badge(d.Status), string.Empty) +
            this.form.TableData(d.GatewayMode ?? string.Empty, string.Empty) +
            this.form.TableData(d.GatewayServer ?? string.Empty, string.Empty) +
            this.form.TableData(d.OffloadPeriod ?? string.Empty, string.Empty) +
            this.BoolCell(d.ArchiveTierEnabled) +
            this.BoolCell(d.CostOptimizedEnabled) +
            this.BoolCell(d.FullBackupModeEnabled) +
            this.BoolCell(d.EncryptionEnabled) +
            this.BoolCell(d.ImmutableEnabled) +
            this.form.TableData(d.Type ?? string.Empty, string.Empty) +
            "</tr>";

        protected override List<string> JsonHeaders => new()
        {
            "Name", "SobrName", "Status", "GatewayMode", "GatewayServer", "OffloadPeriod",
            "ArchiveTierEnabled", "CostOptimizedEnabled", "FullBackupModeEnabled",
            "EncryptionEnabled", "ImmutableEnabled", "Type"
        };

        protected override List<string> ToJsonRow(CArchiveTierExtent d) => new()
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
        };
    }
}
