using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// The "Location" column the infrastructure tables (managed servers, proxies, SOBRs,
    /// extents, repositories) add when at least one of their rows has a VBR Location. Values
    /// come from <see cref="CDataTypesParser.Locations"/>, parsed once per report. VBR accepts
    /// locations only on some object kinds (repositories yes; Windows and Hyper-V servers and
    /// VI proxies are rejected on VBR 13.1), so a table whose rows have none leaves the column
    /// out rather than show an always-empty one. Missing or unreadable location data gives
    /// "" for every lookup, so no table can fail on location data.
    /// </summary>
    internal static class CLocationColumn
    {
        public const string HeaderText = "Location";

        public const string Tooltip = "VBR Location this object is assigned to (Get-VBRLocation). Blank: no location assigned.";

        private static readonly CLocationLookup Empty = new();

        public static CLocationLookup Lookup => CGlobals.DtParser?.Locations ?? Empty;

        /// <summary>True when VBR defines at least one location.</summary>
        public static bool Show => Lookup.HasLocations;

        /// <summary>
        /// True when a table should carry the column: VBR has locations and at least one of the
        /// table's rows resolved to one. Never throws; a failure reading the rows means no column.
        /// </summary>
        public static bool ShowFor(Func<IEnumerable<string>> rowLocations)
        {
            if (!Show || rowLocations == null)
            {
                return false;
            }

            try
            {
                return rowLocations()?.Any(l => !string.IsNullOrEmpty(l)) == true;
            }
            catch (Exception ex)
            {
                CGlobals.Logger?.Warning("[CLocationColumn] Could not read rows for the Location column: " + ex.Message);
                return false;
            }
        }

        /// <summary>Location for one object, scrubbed when the report is scrubbed. Never null.</summary>
        public static string For(string id, string name, bool scrub, params string[] objectTypes)
        {
            string location = Lookup.Find(id, name, objectTypes);
            if (scrub && location.Length > 0 && CGlobals.Scrubber != null)
            {
                location = CGlobals.Scrubber.ScrubItem(location, ScrubItemType.Location) ?? string.Empty;
            }

            return location;
        }

        public static string Header(CHtmlFormatting form) => form.TableHeader(HeaderText, Tooltip);

        public static string Header(CHtmlFormatting form, int sortIndex) => form.TableHeader(HeaderText, Tooltip, sortIndex);

        public static string Cell(CHtmlFormatting form, string location) => form.TableData(WebUtility.HtmlEncode(location ?? string.Empty), string.Empty);
    }
}
