using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups
{
    /// <summary>
    /// Render(records, sweepEvaluated, scrub, out summary) takes already-loaded
    /// data directly (no CSV reading here), so most fixtures are built in memory
    /// rather than following VbrTableScrubTestBase's CSV-writing pattern. It DOES
    /// touch CGlobals when scrub=true though: it delegates to the codebase's
    /// universal per-value scrub convention, CGlobals.Scrubber.ScrubItem, the
    /// same as every other VBR table renderer (CUserRolesTable, CCredentialsTable,
    /// CReplicasTable, ...). That handler writes a real de-anonymization key file
    /// under CVariables.unsafeDir (CGlobals.desiredPath + "\Original"), so this
    /// class still inherits VbrTableScrubTestBase - purely for its temp
    /// desiredPath/"Original" directory setup and teardown, not for its
    /// CSV-writing helpers - and is tagged [Collection("GlobalState")] because
    /// CGlobals.Scrubber is a single process-lifetime static instance (see
    /// GlobalStateCollection's own doc comment, which names Scrubber explicitly).
    /// </summary>
    [Trait("Category", "OrphanedSupersededBackups")]
    [Collection("GlobalState")]
    public class COrphanedSupersededBackupsTableTests : VbrTableScrubTestBase
    {
        public COrphanedSupersededBackupsTableTests() : base("VhcOrphanedSupersededScrubTests_")
        {
        }

        private static OrphanedSupersededBackupRecord JobRecord(
            string repositoryId, string repositoryName, string jobName, string category)
        {
            return new OrphanedSupersededBackupRecord
            {
                RepositoryId = repositoryId,
                RepositoryName = repositoryName,
                JobName = jobName,
                CurrentJobId = category == "Orphaned" ? Guid.Empty.ToString() : Guid.NewGuid().ToString(),
                Category = category,
                OriginalJobType = "VMware Backup",
                FullCount = 1,
                IncrementalCount = 1,
                TotalSizeBytes = 1_000_000_000,
                OldestRestorePoint = new DateTime(2026, 1, 1),
                NewestRestorePoint = new DateTime(2026, 2, 1),
                Objects = new List<OrphanedSupersededObjectRecord>
                {
                    new OrphanedSupersededObjectRecord
                    {
                        ObjectId = Guid.NewGuid().ToString(),
                        BackupId = Guid.NewGuid().ToString(),
                        ObjectName = "pve-vm-201",
                        FullCount = 1,
                        IncrementalCount = 1,
                        AvgFullSizeBytes = 500_000_000,
                        AvgIncrementalSizeBytes = 500_000_000,
                        TotalSizeBytes = 1_000_000_000,
                        OldestRestorePoint = new DateTime(2026, 1, 1),
                        NewestRestorePoint = new DateTime(2026, 2, 1),
                    }
                }
            };
        }

        [Fact]
        public void Render_NoRecordsAndSweepEvaluated_ShowsNoDataMessage()
        {
            var table = new COrphanedSupersededBackupsTable();

            string html = table.Render(new List<OrphanedSupersededBackupRecord>(), sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("No orphaned or superseded backups detected", html);
            Assert.DoesNotContain("not evaluated", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Render_NoRecordsAndSweepNotEvaluated_ShowsNotEvaluatedMessage()
        {
            var table = new COrphanedSupersededBackupsTable();

            string html = table.Render(new List<OrphanedSupersededBackupRecord>(), sweepEvaluated: false, scrub: false, out string summary);

            Assert.Contains("not evaluated", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Render_RecordsPresentAndSweepNotEvaluated_StillShowsNotEvaluatedNotice()
        {
            // Regression test for the exact gap review caught: a pure
            // safe-allowlist environment with a rebuilt machine has
            // Superseded rows (the stale-ObjectId guard runs unconditionally,
            // Task 2) even though SweepRan is false, so Orphaned coverage was
            // never evaluated. The notice must not get skipped just because
            // there happens to be other data to show.
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "Superseded")
            };

            string html = table.Render(records, sweepEvaluated: false, scrub: false, out string summary);

            Assert.Contains("not evaluated", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("VBR Managed Agents - Windows", html);
        }

        [Fact]
        public void Render_RecordsPresentAndSweepEvaluated_DoesNotShowNotEvaluatedNotice()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            Assert.DoesNotContain("not evaluated", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Proxmox - Malware Lab", html);
        }

        [Fact]
        public void Render_GroupsByRepository_DisplaysRepositoryNameNotRawGuid()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("11111111-1111-1111-1111-111111111111", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("Repo01 (Local ReFS)", html);
            Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", html);
        }

        [Fact]
        public void Render_RepositoryNameMissing_FallsBackToRepositoryId()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("11111111-1111-1111-1111-111111111111", null, "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("11111111-1111-1111-1111-111111111111", html);
        }

        [Fact]
        public void Render_ScrubTrue_AnonymizesJobAndObjectNames()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: true, out string summary);

            Assert.DoesNotContain("Proxmox - Malware Lab", html);
            Assert.DoesNotContain("pve-vm-201", html);
        }
    }
}
