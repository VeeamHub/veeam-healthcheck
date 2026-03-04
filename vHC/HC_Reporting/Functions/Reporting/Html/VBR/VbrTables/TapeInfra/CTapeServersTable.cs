using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.TapeInfra
{
    internal class CTapeServersTable
    {
        public CTapeServersTable() { }

        public string Render(bool scrub)
        {
            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicTapeServers();

                var table = new CSectionTable<dynamic>("tapeservers", "Tape Servers")
                    .WithIcon("T", "#f0f9ff", "#0369a1")
                    .Column("Name", string.Empty, item =>
                    {
                        string name = (string)(item.name ?? "");
                        return scrub ? CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Server) : name;
                    }, leftAlign: true)
                    .Column("Description", string.Empty, item => (string)(item.description ?? ""))
                    .Column("State", string.Empty, item => (string)(item.state ?? ""));

                if (data == null || !data.Any())
                    return table.RenderEmpty("No tape servers detected.");

                return table.Render(data);
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Tape Servers table: " + e.Message);
                return string.Empty;
            }
        }
    }
}
