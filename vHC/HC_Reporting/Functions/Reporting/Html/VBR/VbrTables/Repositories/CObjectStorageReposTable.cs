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
            s += this.form.TableHeader("Endpoint", string.Empty);
            s += this.form.TableHeader("Account", string.Empty);
            s += this.form.TableHeader("Connection", string.Empty);
            s += this.form.TableHeader("Gateway", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicObjectStorageRepos()?
                    .Where(r => ((IDictionary<string, object>)r).Values.Any(v => !string.IsNullOrWhiteSpace(v as string)))
                    .ToList();

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='9' style='text-align: center; padding: 20px; color: #666;'><em>No object storage repositories detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        // Cast to IDictionary<string,object> + TryGetValue so a row missing a
                        // column (CsvHelper FastDynamicObject quirk on header-only / partial
                        // CSVs) does not throw RuntimeBinderException. Matches the safe
                        // pattern in CUserRolesTable.
                        var row = (IDictionary<string, object>)item;
                        string Get(string key) =>
                            row.TryGetValue(key, out var v) ? (string)(v ?? "") : "";

                        s += "<tr>";

                        string name = Get("Name");
                        string account = Get("Account");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Item);
                            account = CGlobals.Scrubber.ScrubItem(account, ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(Get("Type"), string.Empty);
                        s += this.form.TableData(Get("Bucket"), string.Empty);
                        s += this.form.TableData(Get("Folder"), string.Empty);
                        s += this.form.TableData(Get("Region"), string.Empty);
                        s += this.form.TableData(Get("Endpoint"), string.Empty);
                        s += this.form.TableData(account, string.Empty);
                        s += this.form.TableData(Get("ConnectionType"), string.Empty);
                        s += this.form.TableData(Get("Gateway"), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Object Storage Repositories table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }
    }
}
