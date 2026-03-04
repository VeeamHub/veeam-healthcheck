using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Html.VBR;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SOBR
{
    internal class CCapacityTierExtTable : CSectionTable<CCapacityTierExtent>
    {
        private readonly CDataFormer df = new();

        protected override string SectionId => "capextents";
        protected override string Title => "Capacity Tier Configuration";
        protected override string ButtonText => "Capacity";

        protected override string GetSummary() => "Capacity tier extent configuration and retention policies";

        protected override string RenderHeaders() =>
            this.form.TableHeader("Name", "Name of the capacity tier extent") +
            this.form.TableHeader("SOBR Name", "Parent Scale-Out Backup Repository") +
            this.form.TableHeader("Status", "Capacity tier status") +
            this.form.TableHeader("Auto Gateway / Direct Connection", "Gateway connection type (e.g., Direct, Gateway)") +
            this.form.TableHeader("Specified Gateway(s)", "Gateway servers assigned to this capacity extent") +
            this.form.TableHeader("Copy Mode Enabled", "Whether copy mode is enabled for capacity tier") +
            this.form.TableHeader("Move Mode Enabled", "Whether move mode is enabled for capacity tier") +
            this.form.TableHeader("Move Period (Days)", "Age threshold before data is moved to capacity tier") +
            this.form.TableHeader("Encryption", "Whether encryption is enabled for capacity tier") +
            this.form.TableHeader("Use Immutability", "Whether immutability is enforced") +
            this.form.TableHeader("Immutable Period", "How long data is locked in capacity tier") +
            this.form.TableHeader("Immutability Mode", "Immutability mode (v13+, e.g. RepositoryRetention)") +
            this.form.TableHeader("Size Limit Enabled", "Whether size limiting is enforced") +
            this.form.TableHeader("Size Limit", "Maximum capacity tier size") +
            this.form.TableHeader("Type", "Repository type (e.g., AWS S3, Azure Blob)");

        protected override List<CCapacityTierExtent> LoadData(bool scrub) => this.df.CapacityTierXmlFromCsv(scrub);

        protected override string RenderRow(CCapacityTierExtent d) =>
            "<tr>" +
            this.form.TableData(d.Name, string.Empty) +
            this.form.TableData(d.SobrName, string.Empty) +
            this.form.TableData(CHtmlTables.Badge(d.Status), string.Empty) +
            this.form.TableData(d.ConnectionType ?? string.Empty, string.Empty) +
            this.form.TableData(d.GatewayServer ?? string.Empty, string.Empty) +
            this.BoolCell(d.CopyModeEnabled) +
            this.BoolCell(d.MoveModeEnabled) +
            this.form.TableData(d.MovePeriodDays.ToString(), string.Empty) +
            this.BoolCell(d.EncryptionEnabled) +
            this.BoolCell(d.ImmutableEnabled) +
            this.form.TableData(d.ImmutablePeriod, string.Empty) +
            this.form.TableData(d.ImmutabilityMode ?? string.Empty, string.Empty) +
            this.BoolCell(d.SizeLimitEnabled) +
            this.form.TableData(d.SizeLimit ?? string.Empty, string.Empty) +
            this.form.TableData(d.Type, string.Empty) +
            "</tr>";

        protected override List<string> JsonHeaders => new()
        {
            "Name", "SobrName", "Status", "ConnectionType", "GatewayServer",
            "CopyModeEnabled", "MoveModeEnabled", "MovePeriodDays",
            "EncryptionEnabled", "ImmutableEnabled", "ImmutablePeriod", "ImmutabilityMode",
            "SizeLimitEnabled", "SizeLimit", "Type"
        };

        protected override List<string> ToJsonRow(CCapacityTierExtent d) => new()
        {
            d.Name,
            d.SobrName,
            d.Status,
            d.ConnectionType ?? string.Empty,
            d.GatewayServer ?? string.Empty,
            d.CopyModeEnabled ? "True" : "False",
            d.MoveModeEnabled ? "True" : "False",
            d.MovePeriodDays.ToString(),
            d.EncryptionEnabled ? "True" : "False",
            d.ImmutableEnabled ? "True" : "False",
            d.ImmutablePeriod,
            d.ImmutabilityMode ?? string.Empty,
            d.SizeLimitEnabled ? "True" : "False",
            d.SizeLimit ?? string.Empty,
            d.Type,
        };
    }
}
