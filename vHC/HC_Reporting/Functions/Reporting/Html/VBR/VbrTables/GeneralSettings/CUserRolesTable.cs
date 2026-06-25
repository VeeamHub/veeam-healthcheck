using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings
{
    internal class CUserRolesTable
    {
        private readonly CHtmlFormatting form = new();

        public CUserRolesTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("userroles", "User Roles", "User Roles");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Role", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            // Declared before the try so the section is always emitted (empty rows when no data).
            var jsonRows = new List<List<string>>();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicUserRoles().ToList();

                if (!data.Any())
                {
                    s += "<tr><td colspan='3' style='text-align: center; padding: 20px; color: #666;'><em>No user role assignments detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        // CsvHelper's FastDynamicObject implements IDictionary<string, object> and
                        // PrepareHeaderForMatch lowercases keys. TryGetValue avoids RuntimeBinderException
                        // when a real-world CSV omits a column (e.g. _UserRoles.csv has no Description column).
                        var row = (IDictionary<string, object>)item;
                        string name = row.TryGetValue("name", out var n) ? (string)(n ?? "") : "";
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                        }

                        string role = row.TryGetValue("role", out var r) ? (string)(r ?? "") : ""; // Role is a Veeam-internal enum string, not PII — intentionally not scrubbed
                        string description = row.TryGetValue("description", out var d) ? (string)(d ?? "") : "";
                        if (scrub)
                        {
                            description = CGlobals.Scrubber.ScrubItem(description, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(role, string.Empty);
                        s += this.form.TableData(description, string.Empty);

                        s += "</tr>";

                        jsonRows.Add(new List<string> { name, role, description });
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render User Roles table: " + e.Message);
            }

            s += this.form.SectionEnd();

            CHtmlTables.SetSectionPublic("userRoles", new List<string> { "Name", "Role", "Description" }, jsonRows, null);

            return s;
        }
    }
}
