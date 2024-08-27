using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security
{
    internal class CComplianceTable
    {
        private CHtmlFormatting _form = new();
        CCsvParser _csv = new();
        IEnumerable<CComplianceCsv> _csvResults;

        public CComplianceTable()
        {
            _csvResults = _csv.ComplianceCsv();
        }
        public string ComplianceSummaryTable()
        {
            string t = "";
            try
            {
                var passedCount = 0;
                var notImplementedCount = 0;
                var unableToDetectCount = 0;
                var suppressedCount = 0;

                foreach (var res in _csvResults)
                {
                    switch (res.Status)
                    {
                        case "Passed":
                            passedCount++;
                            break;
                        case "Not Implemented":
                            notImplementedCount++;
                            break;
                        case "Unable To Detect":
                            unableToDetectCount++;
                            break;
                        case "Suppressed":
                            suppressedCount++;
                            break;
                    }
                }
                t += _form.SectionStartWithButton("ComplianceSummary", "ComplianceSummary", "complianceSummaryButton");
                t += _form.TableHeader("Status", "Passed, Not Implemented, Unable to Detect, Suppressed");
                t += _form.TableHeader("Count", "Number of items");
                t += _form.TableHeaderEnd();
                t += _form.TableBodyStart();


                t += _form.TableRowStart();
                t += _form.TableData("Passed", "");
                t += _form.TableData(passedCount.ToString(), "");
                t += _form.TableRowEnd();
                t += _form.TableRowStart();
                t += _form.TableData("Not Implemented", "");
                t += _form.TableData(notImplementedCount.ToString(), "");
                t += _form.TableRowEnd();
                t += _form.TableRowStart();
                t += _form.TableData("Unable to Detect", "");
                t += _form.TableData(unableToDetectCount.ToString(), "");
                t += _form.TableRowEnd();
                t += _form.TableRowStart();
                t += _form.TableData("Suppressed", "");
                t += _form.TableData(suppressedCount.ToString(), "");
                t += _form.TableRowEnd();


                t += _form.SectionEnd();
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to parse compliance status");
                CGlobals.Logger.Error(e.Message);
            }



            return t;
        }
        public string ComplianceTable()
        {
            string t = "";
            try
            {
                //var csvResults = _csv.ComplianceCsv();
                t += _form.SectionStartWithButton("ComplianceTable", "Compliance Table", "complianceButton");
                t += _form.TableHeader("Best Practice", "Name of the excluded sytem.");
                t += _form.TableHeader("Status", "Platform of the excluded item.");
                t += _form.TableHeaderEnd();
                t += _form.TableBodyStart();

                foreach (var res in _csvResults)
                {
                    t += _form.TableRowStart();
                    t += _form.TableData(res.BestPractice, "");
                    t += _form.TableData(res.Status.ToString(), "");
                    t += _form.TableRowEnd();
                }
                t += _form.SectionEnd();
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to add Compliance Table to HTML report.");
                CGlobals.Logger.Error(e.Message);
                throw;
            }
            return t;

        }
    }
}
