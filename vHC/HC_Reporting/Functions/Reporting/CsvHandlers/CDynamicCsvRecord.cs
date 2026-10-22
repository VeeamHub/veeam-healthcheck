// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Globalization;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    /// <summary>
    /// Null-safe reads from the dynamic records <see cref="CCsvParser"/> returns for CSVs
    /// without a typed model. A dynamic record throws <c>RuntimeBinderException</c> when a
    /// column is not in the file, and a CSV from an older or partial collector can lack
    /// any optional column; the report must show "unknown" instead of crashing.
    /// </summary>
    internal static class CDynamicCsvRecord
    {
        /// <summary>
        /// Returns the text of <paramref name="column"/> (case-insensitive), or null when the
        /// record does not have that column or its value is empty.
        /// </summary>
        /// <param name="record">A record from a dynamic CSV read (an IDictionary of column to value).</param>
        /// <param name="column">The column name.</param>
        /// <returns>The column's text, or null.</returns>
        internal static string GetString(object record, string column)
        {
            if (record is not IDictionary<string, object> fields)
            {
                return null;
            }

            foreach (var field in fields)
            {
                if (string.Equals(field.Key, column, StringComparison.OrdinalIgnoreCase))
                {
                    string text = Convert.ToString(field.Value, CultureInfo.InvariantCulture);
                    return string.IsNullOrEmpty(text) ? null : text;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns <paramref name="column"/> parsed as a number, or 0 when the column is
        /// missing, empty or not a number (unknown is shown as 0, as a blank cell always was).
        /// Parsed with the invariant culture, as the collector writes it: under a culture such as
        /// de-DE, CurrentCulture would read "256.75" as 25675.
        /// </summary>
        /// <param name="record">A record from a dynamic CSV read.</param>
        /// <param name="column">The column name.</param>
        /// <returns>The parsed value, or 0.</returns>
        internal static double GetDouble(object record, string column)
        {
            string text = GetString(record, column);
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double value) ? value : 0;
        }
    }
}
