using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes.Tape;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info
{
    internal class CTapeJobInfoTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CScrubHandler scrubber = CGlobals.Scrubber;

        public CTapeJobInfoTable() { }

        public string TapeJobTable()
        {
            string t = string.Empty;
            try
            {
                CCsvParser c = new();
                var tapeJobInfo = c.GetTapeJobInfoFromCsv();
                if (tapeJobInfo.Count() == 0)
                {

                    return string.Empty;
                }


                t += this.form.Table();
                t += this.form.TableHeaderLeftAligned("Job Name", string.Empty);
                t += this.form.TableHeader("Media Pool - Full", string.Empty);
                t += this.form.TableHeader("Incremental Enabled", string.Empty);
                t += this.form.TableHeader("Media Pool - Incremental", string.Empty);
                t += this.form.TableHeader("Hardware Compression", string.Empty);
                t += this.form.TableHeader("Eject Medium", string.Empty);
                t += this.form.TableHeader("Export Media Set", string.Empty);
                t += this.form.TableHeader("Job Is Enabled", string.Empty);
                t += this.form.TableHeader("Next Run", string.Empty);
                t += this.form.TableHeader("Last Result", string.Empty);

                t += this.form.TableBodyStart();
                foreach (var tj in tapeJobInfo)
                {
                    string jobName = tj.Name;
                    string fullMediaPool = tj.FullBackupMediaPool;
                    string incMediaPool = tj.IncrementalBackupMediaPool;
                    if (CGlobals.Scrub)
                    {
                        jobName = CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Job);
                        fullMediaPool = CGlobals.Scrubber.ScrubItem(fullMediaPool, ScrubItemType.MediaPool);
                        incMediaPool = CGlobals.Scrubber.ScrubItem(incMediaPool, ScrubItemType.MediaPool);
                    }

                    t += "<tr>";
                    t += this.form.TableDataLeftAligned(jobName, string.Empty);
                    t += this.form.TableData(fullMediaPool, string.Empty);
                    t += this.form.TableData(tj.ProcessIncrementalBackup, string.Empty);
                    t += this.form.TableData(incMediaPool, string.Empty);
                    t += this.form.TableData(tj.UseHardwareCompression, string.Empty);
                    t += this.form.TableData(tj.EjectCurrentMedium, string.Empty);
                    t += this.form.TableData(tj.ExportCurrentMediaSet, string.Empty);
                    t += this.form.TableData(tj.Enabled, string.Empty);
                    t += this.form.TableData(tj.NextRun, string.Empty);
                    t += this.form.TableData(tj.LastResult, string.Empty);
                    t += "</tr>";
                }

                t += "</tbody>";
                t += "</table>";
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return t;
        }
    }
}
