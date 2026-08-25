using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups
{
    internal class COrphanedSupersededBackupsTable
    {
        // No try/catch here: a real parse failure must propagate to the
        // caller (AddOrphanedSupersededBackupsTable), which already logs
        // via this.log.Error and sets a fallback summary. A previous draft
        // swallowed exceptions here too, which meant a genuine CSV-corruption
        // error rendered as "No orphaned or superseded backups detected"
        // with nothing in the logs to explain why - defeating the caller's
        // own error handling two lines away.
        public List<OrphanedSupersededBackupRecord> LoadRecords()
        {
            CCsvParser parser = new();
            var rows = parser.GetDynamicOrphanedSupersededBackups();
            return OrphanedSupersededBackupAggregator.Build(rows);
        }

        // Reads the 1-row meta CSV Task 3's script always exports, so an
        // empty/missing data CSV can be told apart from "the global sweep
        // never ran for this environment" rather than assumed to mean
        // "evaluated, nothing found."
        //
        // FAIL-CLOSED DECISION (deviates from an earlier draft that
        // defaulted to `true` on any read failure, including a missing meta
        // file): Get-VhcOrphanedSupersededBackups.ps1 always exports exactly
        // one meta row whenever it runs (see that script's own comment
        // directly above its Export-VhciCsv call for
        // _orphanedSupersededBackupsMeta.csv). That means zero rows never
        // legitimately means "ran, evaluated, found nothing" - it means the
        // collector didn't run for this environment (e.g. a pre-feature CSV
        // folder being re-reported on) or the meta file failed to write or
        // read. Both are "we don't actually know," and defaulting to `true`
        // in either case would silently assert that Orphaned Backup coverage
        // ran when there is no evidence it did - exactly the "silent success
        // on failure" pattern Task 7's review already caught and fixed for
        // CFullReportJson.OrphanedBackupsSweepEvaluated (which now defaults
        // to false). This method is the one place that computes the value
        // which flows into that same DTO property, so it must fail closed
        // too, for consistency and because a false "evaluated, nothing
        // found" banner is strictly worse than an occasionally-too-cautious
        // "not evaluated" one. The one sub-case that already fails closed on
        // its own without extra handling: if SweepRan is present but
        // unparsable, bool.TryParse leaves sweepRan == false and the `&&`
        // short-circuits to false.
        public bool WasSweepEvaluated()
        {
            try
            {
                CCsvParser parser = new();
                var metaRows = parser.GetDynamicOrphanedSupersededBackupsMeta().ToList();
                if (metaRows.Count == 0)
                {
                    CGlobals.Logger.Warning(
                        "[COrphanedSupersededBackupsTable]\t_orphanedSupersededBackupsMeta.csv had no rows " +
                        "(missing, empty, or a pre-feature CSV folder). Treating Orphaned Backup detection as not evaluated.");
                    return false;
                }

                return bool.TryParse((string)metaRows[0].SweepRan.ToString(), out bool sweepRan) && sweepRan;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Warning(
                    "[COrphanedSupersededBackupsTable]\tFailed to read _orphanedSupersededBackupsMeta.csv: " + ex.Message +
                    ". Treating Orphaned Backup detection as not evaluated.");
                return false;
            }
        }

        public string Render(List<OrphanedSupersededBackupRecord> records, bool sweepEvaluated, bool scrub, out string summary)
        {
            // Computed once, prepended regardless of whether there's other
            // data to show - a previous draft only consulted sweepEvaluated
            // inside the records.Count == 0 branch, so a pure-safe-allowlist
            // environment with Superseded rows (the stale-ObjectId guard runs
            // unconditionally, Task 2) rendered a normal-looking table with
            // no mention that Orphaned coverage was never evaluated. See the
            // regression test Render_RecordsPresentAndSweepNotEvaluated_StillShowsNotEvaluatedNotice.
            string notEvaluatedNotice = sweepEvaluated
                ? ""
                : "<p class=\"label\">Orphaned Backup detection was not evaluated for this environment " +
                  "(no job types required the global restore-point sweep, or the sweep's status could not be " +
                  "read). Superseded Backup detection is unaffected and runs regardless.</p>";

            if (records == null || records.Count == 0)
            {
                summary = sweepEvaluated
                    ? "No orphaned or superseded backups detected."
                    : "Orphaned Backups: not evaluated for this environment. No Superseded backups detected.";
                return notEvaluatedNotice + "<p>No orphaned or superseded backups detected for this environment.</p>";
            }

            string s = notEvaluatedNotice;
            var byRepo = records.GroupBy(r => r.RepositoryId ?? "unknown");
            long grandTotalBytes = 0;
            int grandTotalCount = 0;

            foreach (var repoGroup in byRepo)
            {
                var repoRecords = repoGroup.ToList();
                double repoTotalGb = repoRecords.Sum(r => r.TotalSizeBytes) / 1073741824d;
                grandTotalBytes += (long)repoRecords.Sum(r => r.TotalSizeBytes);
                grandTotalCount += repoRecords.Count;

                // Group by RepositoryId (stable, always present) but display
                // RepositoryName - a bare Guid is meaningless as a section
                // header. Falls back to the Guid if resolution failed for
                // every record in the group (Get-VhcOrphanedSupersededBackups
                // couldn't resolve it via -RepositoryDetails).
                string repoLabel;
                if (scrub)
                {
                    repoLabel = "Repository (scrubbed)";
                }
                else
                {
                    var resolvedName = repoRecords.Find(r => !string.IsNullOrEmpty(r.RepositoryName))?.RepositoryName;
                    repoLabel = resolvedName ?? repoGroup.Key;
                }

                s += $"<div class=\"orphaned-repo-group\">";
                s += $"<div class=\"orphaned-repo-header\"><strong>{repoLabel}</strong>";
                s += $"<span class=\"label\">{repoRecords.Count} backups flagged &middot; ~{repoTotalGb:N0} GB potentially reclaimable</span></div>";
                s += "<table><thead><tr>";
                s += "<th></th><th>Job Name</th><th>Status</th><th>Original Job Type</th><th>Fulls</th><th>Incrementals</th><th>Total Size</th><th>Oldest RP</th><th>Newest RP</th>";
                s += "</tr></thead><tbody>";

                foreach (var job in repoRecords.OrderBy(r => r.OldestRestorePoint))
                {
                    string jobName = scrub ? "Job (scrubbed)" : job.JobName;
                    double totalGb = job.TotalSizeBytes / 1073741824d;
                    string badgeClass = job.Category == "Orphaned" ? "badge-orphaned" : "badge-superseded";

                    s += "<tr class=\"detail-toggle\" onclick=\"toggleDetailRow(this)\">";
                    s += "<td>&#9656;</td>";
                    s += $"<td>{jobName}</td>";
                    s += $"<td><span class=\"badge {badgeClass}\">{job.Category}</span></td>";
                    s += $"<td>{job.OriginalJobType}</td>";
                    s += $"<td>{job.FullCount}</td>";
                    s += $"<td>{job.IncrementalCount}</td>";
                    s += $"<td>{totalGb:N1} GB</td>";
                    s += $"<td>{job.OldestRestorePoint:yyyy-MM-dd}</td>";
                    s += $"<td>{job.NewestRestorePoint:yyyy-MM-dd}</td>";
                    s += "</tr>";

                    s += "<tr class=\"detail-row\"><td colspan=\"9\">";
                    s += job.Category == "Orphaned"
                        ? "<p class=\"label\">No live job - this name/type came from the backup's own retained metadata, not a current VBR job.</p>"
                        : "<p class=\"label\">Still a live job - these points belong to an object no longer part of its currently-active membership.</p>";
                    s += "<table><thead><tr><th>Object</th><th>ObjectId</th><th>Fulls</th><th>Incrementals</th><th>Avg Full Size</th><th>Avg Incremental Size</th><th>Total Size</th><th>Oldest</th><th>Newest</th></tr></thead><tbody>";
                    foreach (var obj in job.Objects.OrderBy(o => o.OldestRestorePoint))
                    {
                        string objName = scrub ? "Object (scrubbed)" : obj.ObjectName;
                        s += "<tr>";
                        s += $"<td>{objName}</td>";
                        s += $"<td>{obj.ObjectId}</td>";
                        s += $"<td>{obj.FullCount}</td>";
                        s += $"<td>{obj.IncrementalCount}</td>";
                        s += $"<td>{obj.AvgFullSizeBytes / 1073741824d:N1} GB</td>";
                        s += $"<td>{obj.AvgIncrementalSizeBytes / 1073741824d:N1} GB</td>";
                        s += $"<td>{obj.TotalSizeBytes / 1073741824d:N1} GB</td>";
                        s += $"<td>{obj.OldestRestorePoint:yyyy-MM-dd}</td>";
                        s += $"<td>{obj.NewestRestorePoint:yyyy-MM-dd}</td>";
                        s += "</tr>";
                    }
                    s += "</tbody></table></td></tr>";
                }

                s += "</tbody></table></div>";
            }

            summary = (sweepEvaluated ? "" : "Orphaned Backups: not evaluated for this environment. ")
                + $"{grandTotalCount} orphaned/superseded backups found, ~{grandTotalBytes / 1073741824d:N0} GB potentially reclaimable.";
            return s;
        }
    }
}
