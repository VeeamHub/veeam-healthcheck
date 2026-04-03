using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Repositories
{
    internal class CObjectStorageReposTable
    {
        private readonly CHtmlFormatting form = new();

        public CObjectStorageReposTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("objstorage", "Object Storage Repositories", "Object Storage Repositories");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Bucket/Container", string.Empty);
            s += this.form.TableHeader("Folder", string.Empty);
            s += this.form.TableHeader("Region", string.Empty);
            s += this.form.TableHeader("Account", string.Empty);
            s += this.form.TableHeader("Gateway", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicObjectStorageRepos();

                if (data == null || !data.Any())
                {
                    s += "<tr><td colspan='7' style='text-align: center; padding: 20px; color: #666;'><em>No object storage repositories detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string account = (string)(item.account ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            account = CGlobals.Scrubber.ScrubItem(account, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData((string)(item.type ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.bucket ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.folder ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.region ?? ""), string.Empty);
                        s += this.form.TableData(account, string.Empty);
                        s += this.form.TableData((string)(item.gateway ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Object Storage Repositories table: " + e.Message);
            }

            s += this.form.SectionEnd();

            return s;
        }
    }
}
