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
                var warnFailCount = 0;
                var totalCount = 0;

                foreach (var res in this.csvResults)
                {
                    totalCount++;
                    switch (res.Status)
                    {
                        case "Passed":
                            passedCount++;
                            break;
                        default:
                            warnFailCount++;
                            break;
                    }
                }

                int scorePct = totalCount > 0 ? (int)((double)passedCount / totalCount * 100) : 0;

                t += this.form.SectionStartWithButtonNoTable("ComplianceSummary", "Compliance Summary", "complianceSummaryButton");

                t += "<div class=\"compliance-stats\">";

                t += "<div class=\"compliance-stat\">";
                t += $"<div class=\"compliance-count\">{totalCount}</div>";
                t += "<div class=\"compliance-label\">Total Rules</div>";
                t += "</div>";

                t += "<div class=\"compliance-stat\">";
                t += $"<div class=\"compliance-count\" style=\"color:var(--green)\">{passedCount}</div>";
                t += "<div class=\"compliance-label\">Passed</div>";
                t += "</div>";

                t += "<div class=\"compliance-stat\">";
                t += $"<div class=\"compliance-count\" style=\"color:var(--danger)\">{warnFailCount}</div>";
                t += "<div class=\"compliance-label\">Warnings / Failed</div>";
                t += "</div>";

                t += "<div class=\"compliance-stat\">";
                string scoreColor = scorePct >= 80 ? "var(--green)" : scorePct >= 50 ? "var(--warning)" : "var(--danger)";
                t += $"<div class=\"compliance-count\" style=\"color:{scoreColor}\">{scorePct}%</div>";
                t += "<div class=\"compliance-label\">Compliance Score</div>";
                t += "</div>";

                t += "</div>"; // end compliance-stats

                t += this.form.SectionEndNoTable();
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
                    t += this.form.TableData(ComplianceBadge(res.Status.ToString()), string.Empty);
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

        private static string ComplianceBadge(string status)
        {
            if (string.IsNullOrEmpty(status)) return status;
            string cls = status.ToLower() switch
            {
                "pass" or "passed" => "badge badge-success",
                "warn" or "warning" => "badge badge-warning",
                "fail" or "failed" or "not implemented" => "badge badge-danger",
                "unable to detect" => "badge badge-warning",
                "suppressed" => "badge badge-neutral",
                _ => "badge"
            };
            return $"<span class=\"{cls}\">{status}</span>";
        }
    }
}
