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
    internal class CTapeVaultsTable
    {
        private readonly CHtmlFormatting form = new();

        public CTapeVaultsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("tapevaults", "Tape Vaults", "Tape Vaults");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Protect", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicTapeVaults();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='3' style='text-align: center; padding: 20px; color: #666;'><em>No tape vaults detected.</em></td></tr>";
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
                        s += this.form.TableData((string)(item.description ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.protect ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Tape Vaults table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
