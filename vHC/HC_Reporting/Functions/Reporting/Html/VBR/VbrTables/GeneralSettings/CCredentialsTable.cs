using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
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
                var data = c.GetDynamicCredentials();

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
                    .Column("Description", string.Empty, item => (string)(item.description ?? ""))
                    .Column("Last Modified", string.Empty, item => (string)(item.lastmodified ?? ""));

                if (data == null || !data.Any())
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
