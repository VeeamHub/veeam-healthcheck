using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Repositories
{
    /// <summary>
    /// VBR geographic Locations (Get-VBRLocation) and the repositories, servers and
    /// proxies assigned to each, from _Locations.csv. A location with nothing assigned
    /// renders one row with a blank object. When VBR defines no locations the collector
    /// writes no file and the table shows an empty-state row. Reads the rows parsed once
    /// by CDataTypesParser (the same source as the Location columns on the infrastructure tables).
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
                var data = CLocationColumn.Lookup.Rows.ToList();

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='3' style='text-align: center; padding: 20px; color: #666;'><em>No locations defined in Veeam Backup &amp; Replication.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        // Rows are case-insensitive dictionaries; TryGetValue so a missing column reads as "".
                        var row = item;
                        string Get(string key) =>
                            row.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

                        string location = Get("location");
                        string objectType = Get("objecttype");
                        string objectName = Get("objectname");
                        if (scrub)
                        {
                            // Same scrub kind the object's own table uses, so a scrubbed name here
                            // matches the scrubbed name in the managed server / proxy / repository tables.
                            location = CGlobals.Scrubber.ScrubItem(location, ScrubItemType.Item);
                            objectName = objectName.Length > 0 ? CGlobals.Scrubber.ScrubItem(objectName, ScrubKindFor(objectType)) : objectName;
                        }

                        s += "<tr>";
                        s += this.form.TableDataLeftAligned(WebUtility.HtmlEncode(location), string.Empty);
                        s += this.form.TableData(WebUtility.HtmlEncode(objectType), string.Empty);
                        s += this.form.TableData(WebUtility.HtmlEncode(objectName), string.Empty);
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

        private static ScrubItemType ScrubKindFor(string objectType) => objectType switch
        {
            "Server" or "ViProxy" or "HvProxy" => ScrubItemType.Server,
            "Repository" or "Extent" or "ObjectStore" => ScrubItemType.Repository,
            "SOBR" => ScrubItemType.SOBR,
            _ => ScrubItemType.Item,
        };
    }
}
