using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings
{
    internal class CCredentialsTable
    {
        public CCredentialsTable() { }

        public string Render(bool scrub)
        {
            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCredentials().ToList();

                var table = new CSectionTable<dynamic>("credentials", "Credentials")
                    .WithIcon("C", "#f0fdf4", "#15803d")
                    .Column("Name", string.Empty, item =>
                    {
                        string name = (string)(item.name ?? "");
                        return scrub ? CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item) : name;
                    }, leftAlign: true)
                    .Column("User Name", string.Empty, item =>
                    {
                        string userName = (string)(item.username ?? "");
                        return scrub ? CGlobals.Scrubber.ScrubItem(userName, ScrubItemType.Item) : userName;
                    })
                    .Column("Description", string.Empty, item =>
                    {
                        string description = (string)(item.description ?? "");
                        return scrub ? CGlobals.Scrubber.ScrubItem(description, ScrubItemType.Item) : description;
                    })
                    .Column("Last Modified", string.Empty, item => (string)(item.lastmodified ?? ""));

                // Before the empty-data guard — ensures the credentials key is always present in JSON.
                CHtmlTables.SetSectionPublic("credentials", table.JsonHeaders, table.BuildJsonRows(data), null);

                if (!data.Any())
                    return table.RenderEmpty("No credentials detected.");

                return table.Render(data);
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Credentials table: " + e.Message);
                return string.Empty;
            }
        }
    }
}
