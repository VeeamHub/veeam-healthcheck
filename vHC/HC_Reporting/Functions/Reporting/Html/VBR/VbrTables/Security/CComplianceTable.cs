using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security
{
    internal class CComplianceTable
    {
        private readonly CHtmlFormatting form = new();
        readonly CCsvParser csv = new();
        readonly IEnumerable<CComplianceCsv> csvResults;
        readonly CComplianceMetaCsv meta;
        readonly IEnumerable<CComplianceCatalogCsv> catalogResults;

        public CComplianceTable()
        {
            this.csvResults     = this.csv.ComplianceCsv() ?? new List<CComplianceCsv>();
            this.meta           = this.csv.ComplianceMetaCsv();
            this.catalogResults = this.csv.ComplianceCatalogCsv() ?? new List<CComplianceCatalogCsv>();

            CGlobals.FullReportJson.ComplianceScan = new ComplianceScanMeta
            {
                StartedAt = this.meta?.ScanStartedAt,
                CompletedAt = this.meta?.ScanCompletedAt,
                DurationSeconds = this.meta?.ScanDurationSeconds ?? 0,
                Status = this.meta?.ScanStatus ?? "Unknown",
                RuleCount = this.csvResults?.Count() ?? 0
            };
        }

        public string ComplianceSummaryTable()
        {
            string t = string.Empty;
            try
            {
                bool hasRules = this.csvResults != null && this.csvResults.Any();
                bool hasMeta  = this.meta != null;

                if (!hasRules && !hasMeta)
                {
                    CGlobals.Logger.Warning("No compliance data available - CSV file may not have been generated");
                    return t;
                }

                int totalCount    = 0;
                int passedCount   = 0;
                int warnFailCount = 0;

                if (hasRules)
                {
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
                }

                int scorePct = totalCount > 0 ? (int)((double)passedCount / totalCount * 100) : 0;

                t += this.form.SectionStartWithButtonNoTable("ComplianceSummary", "Compliance Summary", "complianceSummaryButton");

                if (hasMeta && !hasRules)
                {
                    string banner = this.meta.ScanStatus switch
                    {
                        "TimedOut" => "Scan exceeded the configured ceiling and was aborted before results were available. Increase Thresholds.CompliancePollMaxSeconds in VbrConfig.json and re-run.",
                        "Failed"   => "Scan failed before results were available. See the health check log for details.",
                        _          => "Scan returned no rule data."
                    };
                    t += $"<p style=\"color:var(--warning);margin:0 0 12px 0;\"><strong>Compliance scan {this.meta.ScanStatus}.</strong> {banner}</p>";
                }

                t += "<div class=\"compliance-stats\">";

                if (hasRules)
                {
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
                }

                if (hasMeta)
                {
                    t += "<div class=\"compliance-stat\">";
                    if (this.meta.ScanStatus == "Completed")
                    {
                        t += $"<div class=\"compliance-count\">{FormatDuration(this.meta.ScanDurationSeconds)}</div>";
                    }
                    else
                    {
                        t += $"<div class=\"compliance-count\" style=\"color:var(--warning)\">{FormatDuration(this.meta.ScanDurationSeconds)}</div>";
                    }
                    t += $"<div class=\"compliance-label\">Scan Duration ({this.meta.ScanStatus})</div>";
                    t += "</div>";
                }

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

                int driftCount = this.catalogResults != null ? this.catalogResults.Count(c => !c.IsMapped) : 0;
                if (driftCount > 0)
                {
                    t += $"<p class=\"compliance-drift-callout\"><strong>Catalog drift detected:</strong> {driftCount} compliance rule type(s) in the VBR SDK have no label mapping. Affected rows are shown with their raw enum name and a NEW badge. Add them to <code>VbrConfig.json</code> SecurityComplianceRuleNames to resolve.</p>";
                }

                t += this.form.SectionStartWithButton("ComplianceTable", "Compliance Table", "complianceButton");
                t += this.form.TableHeaderLeftAligned("Best Practice", "Name of the excluded sytem.");
                t += this.form.TableHeader("Status", "Platform of the excluded item.");
                t += this.form.TableHeaderEnd();
                t += this.form.TableBodyStart();

                // Accumulate raw values for JSON capture (no badge markup, no NEW span).
                var jsonRows = new List<List<string>>();

                foreach (var res in this.csvResults)
                {
                    string labelCell = res.BestPractice;
                    if (res.IsMapped == false)
                    {
                        labelCell += " <span class=\"badge badge-new\" title=\"Unmapped rule type: add to VbrConfig.json\">NEW</span>";
                    }
                    t += this.form.TableRowStart();
                    t += this.form.TableDataLeftAligned(labelCell, string.Empty);
                    t += this.form.TableData(ComplianceBadge(res.Status.ToString()), string.Empty);
                    t += this.form.TableRowEnd();

                    jsonRows.Add(new List<string> { res.BestPractice, res.Status.ToString() });
                }

                t += this.form.SectionEnd();

                // Per-rule rows — additive alongside the existing ComplianceScan metadata object.
                CHtmlTables.SetSectionPublic(
                    "complianceTable",
                    new List<string> { "Best Practice", "Status" },
                    jsonRows,
                    null);
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to add Compliance Table to HTML report.");
                CGlobals.Logger.Error(e.Message);
                throw;
            }

            return t;
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds < 1)  return "<1s";
            if (seconds < 60) return $"{seconds:F0}s";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.Minutes > 0 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
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
