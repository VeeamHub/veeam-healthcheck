// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Renders the Infrastructure Types chip grid (formerly AddSrvSummaryTable in CHtmlTables).
    /// </summary>
    internal class CInfraChipSection
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CInfraChipSection() { }

        public string Render(bool scrub)
        {
            string summary = this.sum.SrvSum();

            string s = this.form.SectionStartWithButtonNoTable("serversummary", "Infrastructure Types", "srvSummaryButton");

            try
            {
                Dictionary<string, int> list = this.df.ServerSummaryToXml();

                s += "<div class=\"infra-grid\">";
                foreach (var d in list)
                {
                    string infraName = MapInfraTypeName(d.Key);
                    s += $"<span class=\"infra-chip\"><span class=\"infra-chip-count\">{d.Value}</span>{infraName}</span>";
                }
                s += "</div>";
            }
            catch (Exception e)
            {
                this.log.Error("Server Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEndNoTable(summary);

            // JSON capture server summary
            try
            {
                var list = this.df.ServerSummaryToXml();
                List<string> headers = new() { "ServerType", "Count" };
                List<List<string>> rows = list.Select(d => new List<string> { d.Key, d.Value.ToString() }).ToList();
                CHtmlTables.SetSectionPublic("serverSummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture serverSummary JSON section: " + ex.Message);
            }

            return s;
        }

        private static string MapInfraTypeName(string type) => type switch
        {
            "WinServer" or "Windows" => "Windows Server",
            "Linux" => "Linux",
            "HyperV" => "Hyper-V",
            "vSphere" or "ESXi" => "VMware vSphere",
            "AHV" => "Nutanix AHV",
            "Cloud" => "Cloud",
            _ => type
        };
    }
}
