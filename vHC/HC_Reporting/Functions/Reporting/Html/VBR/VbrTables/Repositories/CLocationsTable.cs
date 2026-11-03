using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Repositories
{
    /// <summary>
    /// VBR geographic Locations (Get-VBRLocation) and the repositories, servers and
    /// proxies assigned to each, from _Locations.csv. A location with nothing assigned
    /// renders one row with a blank object. When VBR defines no locations the collector
    /// writes no file and the table shows an empty-state row.
    /// </summary>
    internal class CLocationsTable
    {
        private readonly CHtmlFormatting form = new();

        public CLocationsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("locations", "Locations", "Locations");

            s += this.form.TableHeaderLeftAligned("Location", string.Empty);
            s += this.form.TableHeader("Object Type", string.Empty);
            s += this.form.TableHeader("Object", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicLocations()?
                    .Where(r => ((IDictionary<string, object>)r).Values.Any(v => !string.IsNullOrWhiteSpace(v as string)))
                    .ToList();

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='3' style='text-align: center; padding: 20px; color: #666;'><em>No locations defined in Veeam Backup &amp; Replication.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        // Keys are lowercased by the CSV reader (PrepareHeaderForMatch). IDictionary + TryGetValue: a row missing a column must not throw
                        // RuntimeBinderException (same pattern as CUserRolesTable).
                        var row = (IDictionary<string, object>)item;
                        string Get(string key) =>
                            row.TryGetValue(key, out var v) ? (string)(v ?? "") : "";

                        string location = Get("location");
                        string objectName = Get("objectname");
                        if (scrub)
                        {
                            location = CGlobals.Scrubber.ScrubItem(location, ScrubItemType.Item);
                            objectName = CGlobals.Scrubber.ScrubItem(objectName, ScrubItemType.Item);
                        }

                        s += "<tr>";
                        s += this.form.TableDataLeftAligned(location, string.Empty);
                        s += this.form.TableData(Get("objecttype"), string.Empty);
                        s += this.form.TableData(objectName, string.Empty);
                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Locations table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
