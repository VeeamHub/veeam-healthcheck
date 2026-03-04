// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamHealthCheck.Functions.Reporting.Html.Shared
{
    /// <summary>
    /// Defines a single column in a CSectionTable, binding a header name,
    /// tooltip, and a data extractor function together at definition time.
    /// </summary>
    internal class ColumnDef<T>
    {
        public string Header { get; }
        public string Tooltip { get; }
        public Func<T, string> Extractor { get; }
        public bool LeftAlign { get; }

        public ColumnDef(string header, string tooltip, Func<T, string> extractor, bool leftAlign = false)
        {
            Header = header;
            Tooltip = tooltip;
            Extractor = extractor;
            LeftAlign = leftAlign;
        }
    }

    /// <summary>
    /// A typed, fluent table builder that emits well-formed HTML section cards.
    /// Binds column headers to data extractors at definition time, making it
    /// structurally impossible to have mismatched header/data column counts.
    ///
    /// Uses StringBuilder internally for efficient string building.
    /// Coexists with existing CHtmlFormatting code during incremental migration.
    /// </summary>
    internal class CSectionTable<T>
    {
        private readonly string _id;
        private readonly string _title;
        private readonly List<ColumnDef<T>> _columns = new();

        // Icon properties (optional)
        private string _iconLetter = "";
        private string _iconBgColor = "";
        private string _iconFgColor = "";
        private bool _hasIcon;

        // Section options
        private bool _defaultOpen;
        private int? _intervalDays;

        // Checkbox emoji constants (matching CHtmlFormatting)
        private const string TrueEmoji = "&#9989;";
        private const string FalseEmoji = "&#9744;";

        public CSectionTable(string id, string title)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _title = title ?? throw new ArgumentNullException(nameof(title));
        }

        /// <summary>
        /// Adds a column with a string extractor.
        /// </summary>
        public CSectionTable<T> Column(string header, string tooltip, Func<T, string> extractor, bool leftAlign = false)
        {
            _columns.Add(new ColumnDef<T>(header, tooltip, extractor, leftAlign));
            return this;
        }

        /// <summary>
        /// Adds a column with a boolean extractor that renders as checkbox emoji.
        /// </summary>
        public CSectionTable<T> Column(string header, string tooltip, Func<T, bool> boolExtractor, bool leftAlign = false)
        {
            _columns.Add(new ColumnDef<T>(header, tooltip,
                item => boolExtractor(item) ? TrueEmoji : FalseEmoji, leftAlign));
            return this;
        }

        /// <summary>
        /// Sets the icon badge for the section header.
        /// </summary>
        public CSectionTable<T> WithIcon(string letter, string bgColor, string fgColor)
        {
            _iconLetter = letter;
            _iconBgColor = bgColor;
            _iconFgColor = fgColor;
            _hasIcon = true;
            return this;
        }

        /// <summary>
        /// Makes the section open by default (adds the "open" CSS class).
        /// </summary>
        public CSectionTable<T> DefaultOpen()
        {
            _defaultOpen = true;
            return this;
        }

        /// <summary>
        /// Appends "(N Days)" to the section title.
        /// </summary>
        public CSectionTable<T> WithInterval(int days)
        {
            _intervalDays = days;
            return this;
        }

        /// <summary>
        /// Renders the complete section card with table for the given data.
        /// Emits well-formed HTML with proper thead/tbody structure.
        /// </summary>
        public string Render(IEnumerable<T> data, bool scrub = false)
        {
            var sb = new StringBuilder(1024);

            string displayTitle = _intervalDays.HasValue
                ? $"{_title} ({_intervalDays.Value} Days)"
                : _title;

            // Section card wrapper
            string openClass = _defaultOpen ? " open" : "";
            sb.Append($"<div id=\"{Encode(_id)}\" class=\"section-card{openClass}\">");

            // Section header with collapse toggle
            sb.Append("<div class=\"section-header\" onclick=\"toggleSection(this)\">");
            sb.Append("<h2>");

            // Icon badge
            if (_hasIcon)
            {
                sb.Append($"<span class=\"icon\" style=\"background:{Encode(_iconBgColor)};color:{Encode(_iconFgColor)}\">");
                sb.Append(Encode(_iconLetter));
                sb.Append("</span> ");
            }

            sb.Append(Encode(displayTitle));
            sb.Append("</h2>");
            sb.Append("<span class=\"toggle\">&#9662;</span>");
            sb.Append("</div>");

            // Section body with table
            sb.Append("<div class=\"section-body\">");
            sb.Append("<table>");

            // Thead
            sb.Append("<thead><tr>");
            foreach (var col in _columns)
            {
                string alignStyle = col.LeftAlign ? " style=\"text-align:left\"" : "";
                sb.Append($"<th title=\"{Encode(col.Tooltip)}\"{alignStyle}>{Encode(col.Header)}</th>");
            }
            sb.Append("</tr></thead>");

            // Tbody
            sb.Append("<tbody>");
            var dataList = data?.ToList();
            if (dataList == null || dataList.Count == 0)
            {
                sb.Append($"<tr><td colspan=\"{_columns.Count}\" style=\"text-align: center; padding: 20px; color: #666;\"><em>No data available</em></td></tr>");
            }
            else
            {
                foreach (var item in dataList)
                {
                    sb.Append("<tr>");
                    foreach (var col in _columns)
                    {
                        string value;
                        try
                        {
                            value = col.Extractor(item) ?? "";
                        }
                        catch
                        {
                            value = "";
                        }

                        string alignStyle = col.LeftAlign ? " style=\"text-align:left\"" : "";

                        // Check if value contains HTML entities (like checkbox emoji) - pass through raw
                        if (value.Contains("&#"))
                        {
                            sb.Append($"<td title=\"\"{alignStyle}>{value}</td>");
                        }
                        else
                        {
                            sb.Append($"<td title=\"\"{alignStyle}>{Encode(value)}</td>");
                        }
                    }
                    sb.Append("</tr>");
                }
            }
            sb.Append("</tbody>");

            // Close table and section
            sb.Append("</table>");
            sb.Append("</div>"); // section-body
            sb.Append("</div>"); // section-card

            return sb.ToString();
        }

        /// <summary>
        /// Renders an empty section card with a custom message (no table).
        /// </summary>
        public string RenderEmpty(string message = "No data available")
        {
            var sb = new StringBuilder(512);

            string displayTitle = _intervalDays.HasValue
                ? $"{_title} ({_intervalDays.Value} Days)"
                : _title;

            string openClass = _defaultOpen ? " open" : "";
            sb.Append($"<div id=\"{Encode(_id)}\" class=\"section-card{openClass}\">");
            sb.Append("<div class=\"section-header\" onclick=\"toggleSection(this)\">");
            sb.Append("<h2>");

            if (_hasIcon)
            {
                sb.Append($"<span class=\"icon\" style=\"background:{Encode(_iconBgColor)};color:{Encode(_iconFgColor)}\">");
                sb.Append(Encode(_iconLetter));
                sb.Append("</span> ");
            }

            sb.Append(Encode(displayTitle));
            sb.Append("</h2>");
            sb.Append("<span class=\"toggle\">&#9662;</span>");
            sb.Append("</div>");
            sb.Append("<div class=\"section-body\">");
            sb.Append($"<p style=\"text-align: center; padding: 20px; color: #666;\"><em>{Encode(message)}</em></p>");
            sb.Append("</div>");
            sb.Append("</div>");

            return sb.ToString();
        }

        /// <summary>
        /// Gets the number of columns defined.
        /// </summary>
        public int ColumnCount => _columns.Count;

        /// <summary>
        /// Gets the section ID.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the section title.
        /// </summary>
        public string Title => _title;

        /// <summary>
        /// Minimal HTML encoding.
        /// </summary>
        private static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? "";

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
