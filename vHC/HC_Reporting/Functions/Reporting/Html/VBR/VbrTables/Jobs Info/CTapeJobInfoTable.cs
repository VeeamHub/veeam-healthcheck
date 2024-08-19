﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes.Tape;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info
{
    internal class CTapeJobInfoTable
    {
        private readonly CHtmlFormatting _form = new();
        public CTapeJobInfoTable() { }

        public string TapeJobTable()
        {
            string t = "";
            try
            {
                CCsvParser c = new();
                var tapeJobInfo = c.GetTapeJobInfoFromCsv();
                if (tapeJobInfo.Count() == 0)
                    return "";  

                t += _form.Table();
                t += _form.TableHeader("Job Name", "");
                t += _form.TableHeader("Media Pool - Full", "");
                t += _form.TableHeader("Incremental Enabled", "");
                t += _form.TableHeader("Media Pool - Incremental", "");
                t += _form.TableHeader("Hardware Compression", "");
                t += _form.TableHeader("Eject Medium", "");
                t += _form.TableHeader("Export Media Set", "");
                t += _form.TableHeader("Job Is Enabled", "");
                t += _form.TableHeader("Next Run", "");
                t += _form.TableHeader("Last Result", "");

                t += _form.TableBodyStart();
                foreach (var tj in tapeJobInfo)
                {
                    t += "<tr>";
                    t += _form.TableData(tj.Name, "");
                    t += _form.TableData(tj.FullBackupMediaPool, "");
                    t += _form.TableData(tj.ProcessIncrementalBackup, "");
                    t += _form.TableData(tj.IncrementalBackupMediaPool, "");
                    t += _form.TableData(tj.UseHardwareCompression, "");
                    t += _form.TableData(tj.EjectCurrentMedium, "");
                    t += _form.TableData(tj.ExportCurrentMediaSet, "");
                    t += _form.TableData(tj.Enabled, "");
                    t += _form.TableData(tj.NextRun, "");
                    t += _form.TableData(tj.LastResult, "");
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
