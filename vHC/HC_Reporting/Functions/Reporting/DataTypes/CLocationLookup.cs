using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    /// <summary>
    /// VBR Location assignments from _Locations.csv, parsed once per report by
    /// <see cref="CDataTypesParser"/> so the infrastructure tables (managed servers,
    /// proxies, SOBRs, extents, repositories) can show each object's location the same
    /// way the managed-server table shows roles: one pre-loaded source, one lookup per row.
    ///
    /// Tolerant by design: a missing file, missing columns, blank rows or an unreadable
    /// CSV all give an empty lookup, and every Find on an empty lookup returns "".
    /// </summary>
    public class CLocationLookup
    {
        private readonly Dictionary<string, string> byId = new(StringComparer.OrdinalIgnoreCase);

        // Name fallback, used only when an object's Id is not available in the table's
        // source data. Keyed by "objecttype|name"; a name that maps to more than one
        // location within one type is ambiguous and resolves to "".
        private readonly Dictionary<string, string> byTypeAndName = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> ambiguous = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDictionary<string, object>> rows = new();

        public CLocationLookup()
            : this(Enumerable.Empty<IDictionary<string, object>>())
        {
        }

        /// <param name="rows">Rows of _Locations.csv. Keys are matched case-insensitively
        /// (the CSV reader lowercases them): location, objecttype, objectname, objectid.</param>
        public CLocationLookup(IEnumerable<IDictionary<string, object>> rows)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in rows ?? Enumerable.Empty<IDictionary<string, object>>())
            {
                if (raw == null)
                {
                    continue;
                }

                var row = new Dictionary<string, object>(raw, StringComparer.OrdinalIgnoreCase);
                string location = Get(row, "location");
                if (location.Length == 0)
                {
                    continue;
                }

                names.Add(location);
                this.rows.Add(row);

                string id = Get(row, "objectid");
                if (id.Length > 0 && !this.byId.ContainsKey(id))
                {
                    this.byId[id] = location;
                }

                string type = Get(row, "objecttype");
                string name = Get(row, "objectname");
                if (name.Length > 0)
                {
                    string key = type + "|" + name;
                    if (this.byTypeAndName.TryGetValue(key, out var existing) &&
                        !string.Equals(existing, location, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ambiguous.Add(key);
                    }
                    else
                    {
                        this.byTypeAndName[key] = location;
                    }
                }
            }

            this.LocationCount = names.Count;
        }

        /// <summary>Rows with a location, keys case-insensitive, in CSV order (feeds the Locations table).</summary>
        public IReadOnlyList<IDictionary<string, object>> Rows => this.rows;

        /// <summary>Number of distinct locations defined in VBR (0 when none or not collected).</summary>
        public int LocationCount { get; }

        /// <summary>True when VBR has at least one location; tables add their Location column only then.</summary>
        public bool HasLocations => this.LocationCount > 0;

        /// <summary>
        /// Location assigned to an object, or "" when unknown. Matches by object Id first;
        /// when no Id is given (or it is unknown), by name within the given object types.
        /// </summary>
        public string Find(string id, string name, params string[] objectTypes)
        {
            if (!string.IsNullOrWhiteSpace(id) && this.byId.TryGetValue(id.Trim(), out var byIdHit))
            {
                return byIdHit;
            }

            if (string.IsNullOrWhiteSpace(name) || objectTypes == null)
            {
                return string.Empty;
            }

            foreach (var type in objectTypes)
            {
                string key = type + "|" + name.Trim();
                if (!this.ambiguous.Contains(key) && this.byTypeAndName.TryGetValue(key, out var hit))
                {
                    return hit;
                }
            }

            return string.Empty;
        }

        /// <summary>Reads _Locations.csv. Never throws: any failure yields an empty lookup.</summary>
        public static CLocationLookup FromCsv()
        {
            try
            {
                var rows = new CCsvParser().GetDynamicLocations();
                if (rows == null)
                {
                    return new CLocationLookup();
                }

                return new CLocationLookup(rows.OfType<IDictionary<string, object>>().ToList());
            }
            catch (Exception ex)
            {
                CGlobals.Logger?.Warning("[CLocationLookup] Could not read _Locations.csv; location columns stay blank: " + ex.Message);
                return new CLocationLookup();
            }
        }

        private static string Get(IDictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out var v) && v != null ? v.ToString().Trim() : string.Empty;
        }
    }
}
