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
    internal class CCapacityTierExtTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CLogger log = CGlobals.Logger;

        public CCapacityTierExtTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("capextents", "Capacity Tier Configuration", "Capacity");
            string summary = "Capacity tier extent configuration and retention policies";
            s += "<tr>" +
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
                this.form.TableHeader("Type", "Repository type (e.g., AWS S3, Azure Blob)") +
                "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            List<CCapacityTierExtent> list = new();
            try
            {
                this.log.Info("Attempting to load Capacity Tier Extent data...");
                list = this.df.CapacityTierXmlFromCsv(scrub) ?? list;

                if (list.Count == 0)
                {
                    this.log.Warning("No Capacity Tier Extent data found. No SOBRs with capacity tier may be configured.");
                }

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.SobrName, string.Empty);
                    s += this.form.TableData(CHtmlTables.Badge(d.Status), string.Empty);
                    s += this.form.TableData(d.ConnectionType ?? string.Empty, string.Empty);
                    s += this.form.TableData(d.GatewayServer ?? string.Empty, string.Empty);
                    s += this.BoolCell(d.CopyModeEnabled);
                    s += this.BoolCell(d.MoveModeEnabled);
                    s += this.form.TableData(d.MovePeriodDays.ToString(), string.Empty);
                    s += this.BoolCell(d.EncryptionEnabled);
                    s += this.BoolCell(d.ImmutableEnabled);
                    s += this.form.TableData(d.ImmutablePeriod, string.Empty);
                    s += this.form.TableData(d.ImmutabilityMode ?? string.Empty, string.Empty);
                    s += this.BoolCell(d.SizeLimitEnabled);
                    s += this.form.TableData(d.SizeLimit ?? string.Empty, string.Empty);
                    s += this.form.TableData(d.Type, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("Capacity Tier Extent Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON Capacity Tier Extents
            try
            {
                List<string> headers = new() { "Name", "SobrName", "Status", "ConnectionType", "GatewayServer", "CopyModeEnabled", "MoveModeEnabled", "MovePeriodDays", "EncryptionEnabled", "ImmutableEnabled", "ImmutablePeriod", "ImmutabilityMode", "SizeLimitEnabled", "SizeLimit", "Type" };
                List<List<string>> rows = list.Select(d => new List<string>
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
                }).ToList();
                CHtmlTables.SetSectionPublic("capextents", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture capacity tier extents JSON section: " + ex.Message);
            }

            return s;
        }

        private string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);
    }
}
