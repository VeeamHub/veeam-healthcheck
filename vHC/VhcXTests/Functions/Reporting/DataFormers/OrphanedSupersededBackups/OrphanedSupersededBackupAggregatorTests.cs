using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using Xunit;

namespace VhcXTests.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    [Trait("Category", "OrphanedSupersededBackups")]
    public class OrphanedSupersededBackupAggregatorTests
    {
        // Builds rows with lowercase member names - CCsvParser's real
        // dynamic-CSV pipeline (CCsvReader.GetCsvConfig's
        // PrepareHeaderForMatch) lowercases every header before it becomes a
        // dynamic member, regardless of the PascalCase columns
        // Get-VhcOrphanedSupersededBackups.ps1 actually exports. A prior
        // version of this fixture used PascalCase keys, which let
        // OrphanedSupersededBackupAggregator.MapRow's PascalCase property
        // access compile and "work" against these hand-built ExpandoObjects
        // while throwing (and silently dropping every row) against every
        // real CSV - masking the bug from this entire test class.
        private static dynamic Row(
            string repositoryId, string repositoryName, string jobName, string currentJobId, string category,
            string originalJobType, string objectId, string backupId, string objectName,
            int fullCount, int incrementalCount, double avgFull, double avgIncremental,
            double totalSize, DateTime oldest, DateTime newest)
        {
            dynamic row = new ExpandoObject();
            row.repositoryid = repositoryId;
            row.repositoryname = repositoryName;
            row.jobname = jobName;
            row.currentjobid = currentJobId;
            row.category = category;
            row.originaljobtype = originalJobType;
            row.objectid = objectId;
            row.backupid = backupId;
            row.objectname = objectName;
            row.fullcount = fullCount.ToString();
            row.incrementalcount = incrementalCount.ToString();
            row.avgfullsizebytes = avgFull.ToString();
            row.avgincrementalsizebytes = avgIncremental.ToString();
            row.totalsizebytes = totalSize.ToString();
            row.oldestrestorepoint = oldest.ToString("O");
            row.newestrestorepoint = newest.ToString("O");
            return row;
        }

        /// <summary>
        /// Builds a row from literal wire-format strings instead of the
        /// <c>Row</c> helper's <c>.ToString()</c>/<c>.ToString("O")</c>
        /// shortcuts. Those shortcuts are already culture-invariant by
        /// construction (a bare <c>double.ToString()</c> on a whole-GB
        /// fixture never shows a decimal separator, and "O" is already
        /// invariant ISO 8601) - they can't catch the bug class this class
        /// exists to guard against. <c>RawRow</c> lets a test hand-pick the
        /// exact strings the producer (Get-VhcOrphanedSupersededBackups.ps1)
        /// now writes, or a deliberately malformed one.
        /// </summary>
        private static dynamic RawRow(
            string repositoryId, string repositoryName, string jobName, string currentJobId, string category,
            string originalJobType, string objectId, string backupId, string objectName,
            string fullCount, string incrementalCount, string avgFull, string avgIncremental,
            string totalSize, string oldest, string newest)
        {
            dynamic row = new ExpandoObject();
            row.repositoryid = repositoryId;
            row.repositoryname = repositoryName;
            row.jobname = jobName;
            row.currentjobid = currentJobId;
            row.category = category;
            row.originaljobtype = originalJobType;
            row.objectid = objectId;
            row.backupid = backupId;
            row.objectname = objectName;
            row.fullcount = fullCount;
            row.incrementalcount = incrementalCount;
            row.avgfullsizebytes = avgFull;
            row.avgincrementalsizebytes = avgIncremental;
            row.totalsizebytes = totalSize;
            row.oldestrestorepoint = oldest;
            row.newestrestorepoint = newest;
            return row;
        }

        [Fact]
        public void Build_SingleObjectRow_ProducesOneJobRecordWithOneObject()
        {
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", Guid.Empty.ToString(), "Orphaned",
                    "Proxmox Backup", "obj-1", "backup-1", "pve-vm-201",
                    3, 42, 12_000_000_000, 500_000_000, 540_000_000_000,
                    new DateTime(2025, 11, 2), new DateTime(2026, 3, 15))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Single(result);
            Assert.Equal("Proxmox - Malware Lab", result[0].JobName);
            Assert.Equal("Repo01 (Local ReFS)", result[0].RepositoryName);
            Assert.Equal("Orphaned", result[0].Category);
            Assert.Single(result[0].Objects);
            Assert.Equal("pve-vm-201", result[0].Objects[0].ObjectName);
        }

        [Fact]
        public void Build_TwoObjectsSharingOneJob_RollsUpToOneJobRecordWithTwoObjects()
        {
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "job-1", "Superseded",
                    "Windows Agent Policy", "obj-7", "backup-7", "WindowsAgent07",
                    1, 5, 8_000_000_000, 100_000_000, 48_000_000_000,
                    new DateTime(2026, 3, 1), new DateTime(2026, 3, 6)),
                Row("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "job-1", "Superseded",
                    "Windows Agent Policy", "obj-8", "backup-7", "WindowsAgent08",
                    1, 2, 9_000_000_000, 90_000_000, 12_000_000_000,
                    new DateTime(2026, 1, 1), new DateTime(2026, 1, 10))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Single(result);
            var job = result[0];
            Assert.Equal(2, job.FullCount);
            Assert.Equal(7, job.IncrementalCount);
            Assert.Equal(60_000_000_000, job.TotalSizeBytes);
            Assert.Equal(new DateTime(2026, 1, 1), job.OldestRestorePoint);
            Assert.Equal(new DateTime(2026, 3, 6), job.NewestRestorePoint);
            Assert.Equal(2, job.Objects.Count);
        }

        [Fact]
        public void Build_DifferentCategoriesForSameJobName_ProducesSeparateRecords()
        {
            // Same BackupId group could in principle produce an Orphaned row
            // (no current job) and a different group could separately name-match
            // a real job of the same display name after a rebuild - Category is
            // part of the grouping key so these never silently merge.
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "Windows01", Guid.Empty.ToString(), "Orphaned",
                    "VMware Backup", "obj-old", "backup-old", "Windows01",
                    1, 0, 50_000_000_000, 0, 50_000_000_000,
                    new DateTime(2026, 3, 1), new DateTime(2026, 3, 1)),
                Row("repo-1", "Repo01 (Local ReFS)", "Windows01", "job-new", "Superseded",
                    "VMware Backup", "obj-new", "backup-new", "Windows01",
                    1, 0, 55_000_000_000, 0, 55_000_000_000,
                    new DateTime(2026, 7, 1), new DateTime(2026, 7, 1))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Category == "Orphaned");
            Assert.Contains(result, r => r.Category == "Superseded");
        }

        [Fact]
        public void Build_TwoObjectsWithDifferentBackupIds_StillRollUpButRetainDistinctBackupIds()
        {
            // Companion to Build_TwoObjectsSharingOneJob_RollsUpToOneJobRecordWithTwoObjects:
            // BackupId is deliberately NOT part of the grouping key (see
            // OrphanedSupersededBackupAggregator.Build's comment - on a
            // per-VM-chains-enabled repository, ADR 0027 means every object
            // in a job already has its own distinct BackupId, so grouping on
            // it would explode one job's roll-up into one row per object).
            // Two objects under the same job with DIFFERENT BackupIds must
            // still roll up into one JobRecord, with each object's own
            // BackupId preserved so a reader can tell chains apart in the
            // nested detail view.
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "job-1", "Superseded",
                    "Windows Agent Policy", "obj-7", "backup-aaa", "WindowsAgent07",
                    1, 5, 8_000_000_000, 100_000_000, 48_000_000_000,
                    new DateTime(2026, 3, 1), new DateTime(2026, 3, 6)),
                Row("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "job-1", "Superseded",
                    "Windows Agent Policy", "obj-8", "backup-bbb", "WindowsAgent08",
                    1, 2, 9_000_000_000, 90_000_000, 12_000_000_000,
                    new DateTime(2026, 1, 1), new DateTime(2026, 1, 10))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            var job = Assert.Single(result);
            Assert.Equal(2, job.Objects.Count);
            Assert.Contains(job.Objects, o => o.BackupId == "backup-aaa");
            Assert.Contains(job.Objects, o => o.BackupId == "backup-bbb");
        }

        [Fact]
        public void Build_EmptyInput_ReturnsEmptyList()
        {
            var result = OrphanedSupersededBackupAggregator.Build(new List<dynamic>());

            Assert.Empty(result);
        }

        [Fact]
        public void Build_NullInput_ReturnsEmptyList()
        {
            var result = OrphanedSupersededBackupAggregator.Build(null);

            Assert.Empty(result);
        }

        [Fact]
        public void Build_RealProducerWireFormat_RoundTripsCorrectly()
        {
            // Regression test for a real bug (empirically verified):
            // Get-VhcOrphanedSupersededBackups.ps1 used to let Export-Csv
            // stringify AvgFullSizeBytes/AvgIncrementalSizeBytes/
            // TotalSizeBytes/OldestRestorePoint/NewestRestorePoint using the
            // COLLECTING HOST'S CURRENT CULTURE, while this aggregator always
            // parsed them with CultureInfo.InvariantCulture. On a
            // comma-decimal host, double.Parse("8500000000,5",
            // InvariantCulture) silently returned 85000000005 (~10x
            // inflated, no exception, because the default NumberStyles for
            // that overload allows a stray "," as a thousands separator).
            // On a dd/MM host, DateTime.Parse("07.03.2026 10:30:00",
            // InvariantCulture) silently swapped day and month. The fix
            // pins an explicit invariant wire format at the producer (period
            // decimal via .ToString([CultureInfo]::InvariantCulture); ISO
            // 8601 round-trip via .ToString('o', ...)) and tightens the
            // consumer to NumberStyles.Float (no AllowThousands, so a stray
            // "," is now rejected rather than silently absorbed) and
            // DateTime.TryParseExact("o", ...). This test uses literal
            // strings in exactly that wire format - including a genuinely
            // fractional size (not a whole-GB multiple, so a comma-vs-period
            // mistake would actually show) and a day/month-ambiguous-looking
            // date - to prove the round trip is correct end to end.
            var rows = new List<dynamic>
            {
                RawRow("repo-1", "Repo01 (Local ReFS)", "VMware - Culture Check", Guid.Empty.ToString(), "Orphaned",
                    "VMware Backup", "obj-1", "backup-1", "CultureCheckVM",
                    "1", "0", "8500000000.5", "0", "8500000000.5",
                    "2026-03-07T10:30:00.0000000Z", "2026-03-07T10:30:00.0000000Z")
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Single(result);
            var obj = Assert.Single(result[0].Objects);
            Assert.Equal(8500000000.5, obj.AvgFullSizeBytes);
            Assert.Equal(8500000000.5, obj.TotalSizeBytes);
            // Not just DateTime equality (which ignores Kind and could in
            // principle be satisfied by a coincidentally-equal tick count) -
            // Month/Day are asserted explicitly since a day/month swap is
            // exactly the failure mode this test guards against.
            Assert.Equal(3, obj.OldestRestorePoint.Month);
            Assert.Equal(7, obj.OldestRestorePoint.Day);
            Assert.Equal(2026, obj.OldestRestorePoint.Year);
        }

        [Fact]
        public void Build_RowWithUnparseableNumericField_DropsOnlyThatRowWithoutThrowing()
        {
            // MapRow logs a warning (via CGlobals.Logger, matching
            // CImportPathResolver's static-logger convention) naming the
            // failed column and its raw value before dropping the row - not
            // asserted here (this codebase's existing logger-touching tests,
            // e.g. CImportPathResolverTests, don't intercept CGlobals.Logger
            // output either), but the behavioral contract - one bad row
            // never takes down the whole Build() call or any sibling row -
            // is what this test proves.
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "Good Job", "job-1", "Superseded",
                    "VMware Backup", "obj-good", "backup-good", "GoodVM",
                    1, 0, 10_000_000_000, 0, 10_000_000_000,
                    new DateTime(2026, 1, 1), new DateTime(2026, 1, 1)),
                RawRow("repo-1", "Repo01 (Local ReFS)", "Bad Job", "job-2", "Superseded",
                    "VMware Backup", "obj-bad", "backup-bad", "BadVM",
                    "not-a-number", "0", "10000000000", "0", "10000000000",
                    "2026-01-01T00:00:00.0000000", "2026-01-01T00:00:00.0000000"),
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Single(result);
            Assert.Equal("Good Job", result[0].JobName);
        }
    }
}
