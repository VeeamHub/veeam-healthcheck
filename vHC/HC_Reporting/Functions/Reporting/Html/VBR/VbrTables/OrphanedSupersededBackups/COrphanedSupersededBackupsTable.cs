using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups
{
    internal class COrphanedSupersededBackupsTable
    {
        private readonly CHtmlFormatting form = new();

        // Get-VhcOrphanedSupersededBackups.ps1 is the sole producer of
        // Category and only ever writes one of these two literals - named
        // here rather than repeating raw strings so a future producer-side
        // typo/rename fails a compile instead of silently mislabeling rows.
        private const string CategoryOrphaned = "Orphaned";
        private const string CategorySuperseded = "Superseded";


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

                // Lowercase member name: dynamic CSV rows go through
                // CCsvReader.GetCsvConfig's PrepareHeaderForMatch, which
                // lowercases every header regardless of the PascalCase
                // "SweepRan" column Get-VhcOrphanedSupersededBackups.ps1
                // actually exports (see OrphanedSupersededBackupAggregator.
                // MapRow for the same fix on the sibling data CSV).
                return bool.TryParse((string)metaRows[0].sweepran.ToString(), out bool sweepRan) && sweepRan;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Warning(
                    "[COrphanedSupersededBackupsTable]\tFailed to read _orphanedSupersededBackupsMeta.csv: " + ex.Message +
                    ". Treating Orphaned Backup detection as not evaluated.");
                return false;
            }
        }

        // AddOrphanedSupersededBackupsTable (CHtmlTables.cs) also persists
        // records into CGlobals.FullReportJson, the DTO backing the
        // "safe to share" scrubbed JSON export. Render() below only ever
        // scrubs local HTML string variables - it never touches the record
        // objects - so a caller that fed the raw `records` list straight
        // into FullReportJson would leak real RepositoryName/JobName/
        // ObjectName into that JSON export even when scrub=true. This
        // produces a cloned, scrubbed copy using the exact same
        // CGlobals.Scrubber.ScrubItem calls (and therefore the exact same
        // per-value aliases) Render() uses for HTML, so the two exports
        // stay consistent. RepositoryId/CurrentJobId/ObjectId/BackupId are
        // left as-is, matching Render(): this codebase's scrub convention
        // treats opaque GUIDs as non-identifying, same as the HTML table.
        public List<OrphanedSupersededBackupRecord> ScrubRecordsForExport(List<OrphanedSupersededBackupRecord> records)
        {
            if (records == null)
            {
                return records;
            }

            return records.Select(r => new OrphanedSupersededBackupRecord
            {
                RepositoryId = r.RepositoryId,
                RepositoryName = CGlobals.Scrubber.ScrubItem(r.RepositoryName, ScrubItemType.Repository),
                JobName = CGlobals.Scrubber.ScrubItem(r.JobName, ScrubItemType.Job),
                CurrentJobId = r.CurrentJobId,
                Category = r.Category,
                OriginalJobType = r.OriginalJobType,
                FullCount = r.FullCount,
                IncrementalCount = r.IncrementalCount,
                TotalSizeBytes = r.TotalSizeBytes,
                OldestRestorePoint = r.OldestRestorePoint,
                NewestRestorePoint = r.NewestRestorePoint,
                Objects = r.Objects.Select(o => new OrphanedSupersededObjectRecord
                {
                    ObjectId = o.ObjectId,
                    BackupId = o.BackupId,
                    ObjectName = CGlobals.Scrubber.ScrubItem(o.ObjectName, ScrubItemType.VM),
                    FullCount = o.FullCount,
                    IncrementalCount = o.IncrementalCount,
                    AvgFullSizeBytes = o.AvgFullSizeBytes,
                    AvgIncrementalSizeBytes = o.AvgIncrementalSizeBytes,
                    TotalSizeBytes = o.TotalSizeBytes,
                    OldestRestorePoint = o.OldestRestorePoint,
                    NewestRestorePoint = o.NewestRestorePoint,
                }).ToList(),
            }).ToList();
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
                var resolvedName = repoRecords.Find(r => !string.IsNullOrEmpty(r.RepositoryName))?.RepositoryName;
                string repoLabel = resolvedName ?? repoGroup.Key;

                // Scrub via the codebase's universal per-value scrub convention
                // (CGlobals.Scrubber.ScrubItem, used by every other VBR table -
                // e.g. CUserRolesTable, CCredentialsTable, CReplicasTable) rather
                // than a flat "Repository (scrubbed)" literal for every group. A
                // stable per-value alias (Repository_0, Repository_1, ...)
                // preserves which-is-which across a scrubbed report - a Veeam
                // support engineer looking at 3 repositories and 12 flagged jobs
                // in a scrubbed report can still tell them apart, which a single
                // flat placeholder for every value cannot.
                if (scrub)
                {
                    repoLabel = CGlobals.Scrubber.ScrubItem(repoLabel, ScrubItemType.Repository);
                }

                // Nested section-card per repository, via the exact same
                // form.SectionStartWithButton/SectionEnd pair
                // CJobSessionSummaryTable.RenderByJob uses for its
                // per-job-type groups - not a new UI pattern, just reused
                // here so repository groups get the same icon badge and
                // independent collapse toggle instead of a bare, unstyled
                // div ("orphaned-repo-group" had no CSS rule at all).
                string cardTitle = $"{WebUtility.HtmlEncode(repoLabel)} " +
                    $"<span class=\"label\">{repoRecords.Count} backups flagged &middot; " +
                    $"~{repoTotalGb.ToString("N0", CultureInfo.InvariantCulture)} GB potentially reclaimable</span>";
                // repoGroup.Key is either a RepositoryId Guid or the literal
                // "unknown" fallback - both are already valid, unique HTML id
                // characters, so no further sanitizing is needed here.
                s += this.form.SectionStartWithButton("orphaned-repo-" + repoGroup.Key, cardTitle, string.Empty);
                s += "<th></th><th>Job Name</th><th>Status</th><th>Original Job Type</th><th>Fulls</th><th>Incrementals</th><th>Total Size</th><th>Oldest RP</th><th>Newest RP</th>";
                s += "</tr></thead><tbody>";

                foreach (var job in repoRecords.OrderBy(r => r.OldestRestorePoint))
                {
                    string jobName = scrub ? CGlobals.Scrubber.ScrubItem(job.JobName, ScrubItemType.Job) : job.JobName;
                    double totalGb = job.TotalSizeBytes / 1073741824d;
                    // Explicit three-way check, not a binary ?: on the
                    // Orphaned case alone: an unexpected Category value
                    // (typo, future third category) must not silently
                    // render as - and pair with the explanatory sentence
                    // for - "Superseded", which would be factually wrong.
                    string badgeClass = job.Category switch
                    {
                        CategoryOrphaned => "badge-orphaned",
                        CategorySuperseded => "badge-superseded",
                        _ => "badge-unknown",
                    };

                    s += "<tr class=\"detail-toggle\" onclick=\"toggleDetailRow(this)\">";
                    s += "<td>&#9656;</td>";
                    s += $"<td>{WebUtility.HtmlEncode(jobName)}</td>";
                    s += $"<td><span class=\"badge {badgeClass}\">{job.Category}</span></td>";
                    s += $"<td>{job.OriginalJobType}</td>";
                    s += $"<td>{job.FullCount}</td>";
                    s += $"<td>{job.IncrementalCount}</td>";
                    s += $"<td>{totalGb.ToString("N1", CultureInfo.InvariantCulture)} GB</td>";
                    s += $"<td>{job.OldestRestorePoint:yyyy-MM-dd}</td>";
                    s += $"<td>{job.NewestRestorePoint:yyyy-MM-dd}</td>";
                    s += "</tr>";

                    s += "<tr class=\"detail-row\"><td colspan=\"9\">";
                    s += job.Category switch
                    {
                        CategoryOrphaned => "<p class=\"label\">No live job - this name/type came from the backup's own retained metadata, not a current VBR job.</p>",
                        CategorySuperseded => "<p class=\"label\">Still a live job - these points belong to an object no longer part of its currently-active membership.</p>",
                        _ => "<p class=\"label\">Unrecognized category - showing raw data without further interpretation.</p>",
                    };
                    // BackupId shown per-object rather than used to group
                    // jobs (see Build()'s comment): a per-VM-chains-enabled
                    // repository gives every object its own distinct
                    // BackupId, so a reader comparing two objects' BackupId
                    // values here can tell whether they share a retention
                    // chain or come from entirely separate ones, without the
                    // table exploding into one row per object.
                    s += "<table><thead><tr><th>Object</th><th>ObjectId</th><th>BackupId</th><th>Fulls</th><th>Incrementals</th><th>Avg Full Size</th><th>Avg Incremental Size</th><th>Total Size</th><th>Oldest</th><th>Newest</th></tr></thead><tbody>";
                    foreach (var obj in job.Objects.OrderBy(o => o.OldestRestorePoint))
                    {
                        // ScrubItemType.VM, not .Item: ObjectName is "source
                        // VM/machine name" (see
                        // _orphanedSupersededBackups.schema.json), and while this
                        // feature's objects can come from any protected-workload
                        // type (VMware, Hyper-V, Proxmox, Agent, physical, ...),
                        // this codebase already uses ScrubItemType.VM as the
                        // general "protected object display name" bucket
                        // regardless of the underlying platform - e.g.
                        // CReplicasTable.cs and IndividualJobSessionsHelper.cs use
                        // it for VmName, and CM365Tables.cs uses it for M365
                        // mailbox/site names, none of which are VMware VMs either.
                        // There is no more specific ScrubItemType for a generic
                        // protected object, so VM is the established fit, not Item
                        // (which this codebase reserves for free-text/username
                        // fields like CUserRolesTable's Description).
                        string objName = scrub ? CGlobals.Scrubber.ScrubItem(obj.ObjectName, ScrubItemType.VM) : obj.ObjectName;
                        s += "<tr>";
                        s += $"<td>{WebUtility.HtmlEncode(objName)}</td>";
                        s += $"<td>{obj.ObjectId}</td>";
                        s += $"<td>{obj.BackupId}</td>";
                        s += $"<td>{obj.FullCount}</td>";
                        s += $"<td>{obj.IncrementalCount}</td>";
                        s += $"<td>{(obj.AvgFullSizeBytes / 1073741824d).ToString("N1", CultureInfo.InvariantCulture)} GB</td>";
                        s += $"<td>{(obj.AvgIncrementalSizeBytes / 1073741824d).ToString("N1", CultureInfo.InvariantCulture)} GB</td>";
                        s += $"<td>{(obj.TotalSizeBytes / 1073741824d).ToString("N1", CultureInfo.InvariantCulture)} GB</td>";
                        s += $"<td>{obj.OldestRestorePoint:yyyy-MM-dd}</td>";
                        s += $"<td>{obj.NewestRestorePoint:yyyy-MM-dd}</td>";
                        s += "</tr>";
                    }
                    s += "</tbody></table></td></tr>";
                }

                s += this.form.SectionEnd(string.Empty);
            }

            summary = (sweepEvaluated ? "" : "Orphaned Backups: not evaluated for this environment. ")
                + $"{grandTotalCount} orphaned/superseded backups found, ~{(grandTotalBytes / 1073741824d).ToString("N0", CultureInfo.InvariantCulture)} GB potentially reclaimable.";
            return s;
        }
    }
}
