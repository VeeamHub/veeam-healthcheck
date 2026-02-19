using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SureBackup
{
    internal class CSureBackupVirtualLabsTable
    {
        private readonly CHtmlFormatting form = new();

        public CSureBackupVirtualLabsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("surebackupvirtuallabs", "SureBackup Virtual Labs", "SureBackup Virtual Labs");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Platform", string.Empty);
            s += this.form.TableHeader("Server", string.Empty);
            s += this.form.TableHeader("Proxy Appliance", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicSureBackupVirtualLabs();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='5' style='text-align: center; padding: 20px; color: #666;'><em>No SureBackup virtual labs detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string server = (string)(item.server ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            server = CGlobals.Scrubber.ScrubItem(server, ScrubItemType.Server);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.description ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.platform ?? ""), string.Empty);
                        s += this.form.TableData(server, string.Empty);
                        s += this.form.TableData((string)(item.proxyappliance ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render SureBackup Virtual Labs table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
