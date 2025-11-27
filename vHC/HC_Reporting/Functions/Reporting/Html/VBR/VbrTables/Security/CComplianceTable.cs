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
        private readonly CHtmlFormatting form = new();
        readonly CCsvParser csv = new();
        readonly IEnumerable<CComplianceCsv> csvResults;

        public CComplianceTable()
        {
            this.csvResults = this.csv.ComplianceCsv() ?? new List<CComplianceCsv>();
        }

        public string ComplianceSummaryTable()
        {
            string t = string.Empty;
            try
            {
                // Return early if no data
                if (this.csvResults == null || !this.csvResults.Any())
                {
                    CGlobals.Logger.Warning("No compliance data available - CSV file may not have been generated");
                    return t;
                }
                
                var passedCount = 0;
                var notImplementedCount = 0;
                var unableToDetectCount = 0;
                var suppressedCount = 0;

                foreach (var res in this.csvResults)
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

                t += this.form.SectionStartWithButton("ComplianceSummary", "ComplianceSummary", "complianceSummaryButton");
                t += this.form.TableHeaderLeftAligned("Status", "Passed, Not Implemented, Unable to Detect, Suppressed");
                t += this.form.TableHeader("Count", "Number of items");
                t += this.form.TableHeaderEnd();
                t += this.form.TableBodyStart();

                t += this.form.TableRowStart();
                t += this.form.TableDataLeftAligned("Passed", string.Empty);
                t += this.form.TableData(passedCount.ToString(), string.Empty);
                t += this.form.TableRowEnd();
                t += this.form.TableRowStart();
                t += this.form.TableDataLeftAligned("Not Implemented", string.Empty);
                t += this.form.TableData(notImplementedCount.ToString(), string.Empty);
                t += this.form.TableRowEnd();
                t += this.form.TableRowStart();
                t += this.form.TableDataLeftAligned("Unable to Detect", string.Empty);
                t += this.form.TableData(unableToDetectCount.ToString(), string.Empty);
                t += this.form.TableRowEnd();
                t += this.form.TableRowStart();
                t += this.form.TableDataLeftAligned("Suppressed", string.Empty);
                t += this.form.TableData(suppressedCount.ToString(), string.Empty);
                t += this.form.TableRowEnd();

                t += this.form.SectionEnd();
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
            string t = string.Empty;
            try
            {
                // Return early if no data
                if (this.csvResults == null || !this.csvResults.Any())
                {
                    CGlobals.Logger.Warning("No compliance data available - CSV file may not have been generated");
                    return t;
                }
                
                // var csvResults = _csv.ComplianceCsv();
                t += this.form.SectionStartWithButton("ComplianceTable", "Compliance Table", "complianceButton");
                t += this.form.TableHeaderLeftAligned("Best Practice", "Name of the excluded sytem.");
                t += this.form.TableHeader("Status", "Platform of the excluded item.");
                t += this.form.TableHeaderEnd();
                t += this.form.TableBodyStart();

                foreach (var res in this.csvResults)
                {
                    t += this.form.TableRowStart();
                    t += this.form.TableDataLeftAligned(res.BestPractice, string.Empty);
                    t += this.form.TableData(res.Status.ToString(), string.Empty);
                    t += this.form.TableRowEnd();
                }

                t += this.form.SectionEnd();
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
