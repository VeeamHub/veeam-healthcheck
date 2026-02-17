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
    internal class CTapeMediaPoolsTable
    {
        private readonly CHtmlFormatting form = new();

        public CTapeMediaPoolsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("tapemediapools", "Tape Media Pools", "Tape Media Pools");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Media Count", string.Empty);
            s += this.form.TableHeader("Retention Policy", string.Empty);
            s += this.form.TableHeader("Encryption", string.Empty);
            s += this.form.TableHeader("Is WORM", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicTapeMediaPools();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='7' style='text-align: center; padding: 20px; color: #666;'><em>No tape media pools detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        if (scrub)
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.MediaPool);

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.type ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.description ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.mediacount ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.retentionpolicy ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.encryption ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.isworm ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Tape Media Pools table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
