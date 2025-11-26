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
        private readonly CHtmlFormatting _form = new();
        private readonly CScrubHandler _scrubber = CGlobals.Scrubber;

        public CTapeJobInfoTable() { }

        public string TapeJobTable()
        {
            string t = string.Empty;
            try
            {
                CCsvParser c = new();
                var tapeJobInfo = c.GetTapeJobInfoFromCsv();
                if (tapeJobInfo.Count() == 0)
                    return string.Empty;  

                t += _form.Table();
                t += _form.TableHeaderLeftAligned("Job Name", string.Empty);
                t += _form.TableHeader("Media Pool - Full", string.Empty);
                t += _form.TableHeader("Incremental Enabled", string.Empty);
                t += _form.TableHeader("Media Pool - Incremental", string.Empty);
                t += _form.TableHeader("Hardware Compression", string.Empty);
                t += _form.TableHeader("Eject Medium", string.Empty);
                t += _form.TableHeader("Export Media Set", string.Empty);
                t += _form.TableHeader("Job Is Enabled", string.Empty);
                t += _form.TableHeader("Next Run", string.Empty);
                t += _form.TableHeader("Last Result", string.Empty);

                t += _form.TableBodyStart();
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
                    t += _form.TableDataLeftAligned(jobName, string.Empty);
                    t += _form.TableData(fullMediaPool, string.Empty);
                    t += _form.TableData(tj.ProcessIncrementalBackup, string.Empty);
                    t += _form.TableData(incMediaPool, string.Empty);
                    t += _form.TableData(tj.UseHardwareCompression, string.Empty);
                    t += _form.TableData(tj.EjectCurrentMedium, string.Empty);
                    t += _form.TableData(tj.ExportCurrentMediaSet, string.Empty);
                    t += _form.TableData(tj.Enabled, string.Empty);
                    t += _form.TableData(tj.NextRun, string.Empty);
                    t += _form.TableData(tj.LastResult, string.Empty);
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
