using System;
using System.Collections.Generic;
using System.IO;
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
        public void Render_NestedObjectRow_ShowsBackupIdSoReaderCanCompareChains()
        {
            // BackupId is deliberately not part of the aggregator's grouping
            // key (a per-VM-chains-enabled repository gives every object its
            // own distinct BackupId - grouping on it would explode one job's
            // roll-up into one row per object). Surfacing it per-object here
            // instead lets a reader tell whether two objects under the same
            // job share a retention chain or come from separate ones.
            var table = new COrphanedSupersededBackupsTable();
            var record = JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned");
            record.Objects[0].BackupId = "11111111-2222-3333-4444-555555555555";

            string html = table.Render(new List<OrphanedSupersededBackupRecord> { record }, sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("11111111-2222-3333-4444-555555555555", html);
        }

        [Fact]
        public void Render_NamesContainingAngleBrackets_AreHtmlEncodedNotInjected()
        {
            // JobName/ObjectName/RepositoryName are free-text VBR fields -
            // unescaped, a name containing '<'/'>' that happens to parse as
            // a tag would corrupt the surrounding table markup.
            var table = new COrphanedSupersededBackupsTable();
            var record = JobRecord("repo-1", "Repo<script>alert(1)</script>", "Job<b>Name</b>", "Orphaned");
            record.Objects[0].ObjectName = "Obj<img src=x>Name";

            string html = table.Render(new List<OrphanedSupersededBackupRecord> { record }, sweepEvaluated: true, scrub: false, out string summary);

            Assert.DoesNotContain("<script>", html);
            Assert.DoesNotContain("<b>Name</b>", html);
            Assert.DoesNotContain("<img src=x>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("&lt;b&gt;Name&lt;/b&gt;", html);
            Assert.Contains("&lt;img src=x&gt;", html);
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
        public void Render_GroupsByRepository_UsesCollapsibleSectionCardNotBareDiv()
        {
            // Job Session Summary groups by job type using
            // CHtmlFormatting's section-card pattern (icon badge, its own
            // toggleSection collapse, green accent border via css.css)
            // instead of a plain div. Per-repository groups here should
            // match that pattern rather than the old unstyled
            // "orphaned-repo-group"/"orphaned-repo-header" markup so every
            // repository group gets the same collapsible treatment.
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            // Exact id from repoGroup.Key ("repo-1"), the same "open" class
            // and toggle chevron (&#8964;) every other section-card in the
            // report uses, and the icon badge markup - not just a loose
            // "section-card" substring match, which would also pass for a
            // differently-shaped/malformed card.
            Assert.Contains("class=\"section-card open\" id=\"orphaned-repo-repo-1\"", html);
            Assert.Contains("onclick=\"toggleSection(this)\"", html);
            Assert.Contains("<span class=\"icon\"", html);
            Assert.Contains("&#8964;", html);
            Assert.DoesNotContain("orphaned-repo-group", html);
            Assert.DoesNotContain("orphaned-repo-header", html);
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

        [Fact]
        public void ScrubRecordsForExport_AnonymizesNamesButPreservesGuidsAndCounts()
        {
            // Regression test: AddOrphanedSupersededBackupsTable
            // (CHtmlTables.cs) persists these records into
            // CGlobals.FullReportJson, the scrubbed-JSON export - but
            // Render() above only ever scrubbed local HTML string
            // variables, never the record objects themselves, so that JSON
            // export leaked real RepositoryName/JobName/ObjectName even
            // when scrub=true. ScrubRecordsForExport must apply the exact
            // same scrubbing Render() applies to HTML, while leaving GUIDs
            // and numeric fields untouched (matching Render()'s own
            // convention of never scrubbing RepositoryId/ObjectId/BackupId).
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            var scrubbed = table.ScrubRecordsForExport(records);

            var record = Assert.Single(scrubbed);
            Assert.NotEqual("Repo01 (Local ReFS)", record.RepositoryName);
            Assert.NotEqual("Proxmox - Malware Lab", record.JobName);
            Assert.Equal("repo-1", record.RepositoryId);
            var obj = Assert.Single(record.Objects);
            Assert.NotEqual("pve-vm-201", obj.ObjectName);
            Assert.Equal(1, obj.FullCount);
        }

        [Fact]
        public void ScrubRecordsForExport_NullInput_ReturnsNull()
        {
            var table = new COrphanedSupersededBackupsTable();

            var scrubbed = table.ScrubRecordsForExport(null);

            Assert.Null(scrubbed);
        }

        // The tests above build OrphanedSupersededBackupRecord/dynamic rows
        // by hand, so none of them go through the real CsvHelper dynamic-CSV
        // pipeline (CCsvParser.GetDynamicOrphanedSupersededBackups[Meta]) -
        // exactly the gap that let a PascalCase/lowercase member-name
        // mismatch ship silently (every real row threw and was dropped,
        // rendering "No orphaned or superseded backups detected" against any
        // real environment). These write the exact PascalCase-header CSVs
        // Get-VhcOrphanedSupersededBackups.ps1 actually exports and drive
        // LoadRecords()/WasSweepEvaluated() end to end through
        // VbrTableScrubTestBase's isolated VbrDir, so a regression here
        // fails loudly instead of just rendering an empty table.
        private void WriteDataCsv(string rows) =>
            File.WriteAllText(
                Path.Combine(VbrDir, "_orphanedSupersededBackups.csv"),
                "RepositoryId,RepositoryName,JobName,CurrentJobId,Category,OriginalJobType,ObjectId,BackupId," +
                "ObjectName,FullCount,IncrementalCount,AvgFullSizeBytes,AvgIncrementalSizeBytes,TotalSizeBytes," +
                "OldestRestorePoint,NewestRestorePoint\n" + rows);

        private void WriteMetaCsv(string sweepRan) =>
            File.WriteAllText(
                Path.Combine(VbrDir, "_orphanedSupersededBackupsMeta.csv"),
                "SweepRan\n" + sweepRan);

        [Fact]
        public void LoadRecords_RealCsvHelperPipeline_ParsesPascalCaseExportedCsv()
        {
            WriteDataCsv(
                "repo-1,Repo01 (Local ReFS),Proxmox - Malware Lab,00000000-0000-0000-0000-000000000000,Orphaned," +
                "Proxmox Backup,obj-1,backup-1,pve-vm-201,3,42,12000000000,500000000,540000000000," +
                "2025-11-02T00:00:00.0000000Z,2026-03-15T00:00:00.0000000Z\n");

            var records = new COrphanedSupersededBackupsTable().LoadRecords();

            var record = Assert.Single(records);
            Assert.Equal("Proxmox - Malware Lab", record.JobName);
            Assert.Equal("Repo01 (Local ReFS)", record.RepositoryName);
            Assert.Equal("Orphaned", record.Category);
            var obj = Assert.Single(record.Objects);
            Assert.Equal("pve-vm-201", obj.ObjectName);
            Assert.Equal(3, obj.FullCount);
        }

        [Theory]
        [InlineData("True", true)]
        [InlineData("False", false)]
        public void WasSweepEvaluated_RealCsvHelperPipeline_ReadsPascalCaseExportedSweepRanColumn(string sweepRan, bool expected)
        {
            WriteMetaCsv(sweepRan);

            bool evaluated = new COrphanedSupersededBackupsTable().WasSweepEvaluated();

            Assert.Equal(expected, evaluated);
        }
    }
}
