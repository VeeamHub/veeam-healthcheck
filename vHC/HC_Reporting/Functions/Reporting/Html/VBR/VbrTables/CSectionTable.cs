using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Base class for typed HTML section tables. Subclasses define headers, data loading,
    /// row rendering, and JSON column mapping. The base handles boilerplate HTML structure
    /// and JSON capture.
    /// </summary>
    /// <typeparam name="T">The data type for each row in the table.</typeparam>
    internal abstract class CSectionTable<T>
    {
        protected readonly CHtmlFormatting form = new();
        protected readonly CLogger log = CGlobals.Logger;

        /// <summary>Section HTML id attribute (e.g., "sobr", "extents").</summary>
        protected abstract string SectionId { get; }

        /// <summary>Section title displayed in the report.</summary>
        protected abstract string Title { get; }

        /// <summary>Button text for the collapsible section.</summary>
        protected abstract string ButtonText { get; }

        /// <summary>Summary text rendered at the bottom of the section.</summary>
        protected abstract string GetSummary();

        /// <summary>Emit all table header cells (no wrapping tr needed).</summary>
        protected abstract string RenderHeaders();

        /// <summary>Load typed data from CSV/data former.</summary>
        protected abstract List<T> LoadData(bool scrub);

        /// <summary>Render a single data row (including opening and closing tr tags).</summary>
        protected abstract string RenderRow(T item);

        /// <summary>JSON column headers for the SetSection capture.</summary>
        protected abstract List<string> JsonHeaders { get; }

        /// <summary>Convert a data item to a list of string values for JSON capture.</summary>
        protected abstract List<string> ToJsonRow(T item);

        /// <summary>
        /// Renders the complete section HTML and captures JSON data.
        /// </summary>
        public string Render(bool scrub)
        {
            string summary = this.GetSummary();
            string s = this.form.SectionStartWithButton(this.SectionId, this.Title, this.ButtonText);
            s += "<tr>" + this.RenderHeaders() + "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            List<T> list = new();
            try
            {
                list = this.LoadData(scrub) ?? new List<T>();

                if (list.Count == 0)
                {
                    this.log.Warning($"No {this.Title} data found.");
                }

                foreach (var item in list)
                {
                    s += this.RenderRow(item);
                }
            }
            catch (Exception e)
            {
                this.log.Error($"{this.Title} Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON capture
            try
            {
                List<List<string>> rows = list.Select(item => this.ToJsonRow(item)).ToList();
                CHtmlTables.SetSectionPublic(this.SectionId, this.JsonHeaders, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error($"Failed to capture {this.SectionId} JSON section: " + ex.Message);
            }

            return s;
        }

        /// <summary>Helper to render a boolean as a checkmark or empty box.</summary>
        protected string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);
    }
}
