using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.TapeInfra
{
    internal class CTapeLibrariesTable
    {
        private readonly CHtmlFormatting form = new();

        public CTapeLibrariesTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("tapelibraries", "Tape Libraries", "Tape Libraries");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("State", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Slots Count", string.Empty);
            s += this.form.TableHeader("Drives Count", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicTapeLibraries();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='5' style='text-align: center; padding: 20px; color: #666;'><em>No tape libraries detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        if (scrub)
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.state ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.type ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.slotscount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.drivescount ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Tape Libraries table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
