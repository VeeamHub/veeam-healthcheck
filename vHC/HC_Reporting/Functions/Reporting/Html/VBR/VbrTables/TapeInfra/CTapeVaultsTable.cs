using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.TapeInfra
{
    internal class CTapeVaultsTable
    {
        public CTapeVaultsTable() { }

        public string Render(bool scrub)
        {
            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicTapeVaults();

                var table = new CSectionTable<dynamic>("tapevaults", "Tape Vaults")
                    .WithIcon("V", "#fefce8", "#a16207")
                    .Column("Name", string.Empty, item =>
                    {
                        string name = (string)(item.name ?? "");
                        return scrub ? CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item) : name;
                    }, leftAlign: true)
                    .Column("Description", string.Empty, item => (string)(item.description ?? ""))
                    .Column("Protect", string.Empty, item => (string)(item.protect ?? ""));

                if (data == null || !data.Any())
                    return table.RenderEmpty("No tape vaults detected.");

                return table.Render(data);
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Tape Vaults table: " + e.Message);
                return string.Empty;
            }
        }
    }
}
