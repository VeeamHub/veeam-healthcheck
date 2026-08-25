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
        private static dynamic Row(
            string repositoryId, string repositoryName, string jobName, string currentJobId, string category,
            string originalJobType, string objectId, string backupId, string objectName,
            int fullCount, int incrementalCount, double avgFull, double avgIncremental,
            double totalSize, DateTime oldest, DateTime newest)
        {
            dynamic row = new ExpandoObject();
            row.RepositoryId = repositoryId;
            row.RepositoryName = repositoryName;
            row.JobName = jobName;
            row.CurrentJobId = currentJobId;
            row.Category = category;
            row.OriginalJobType = originalJobType;
            row.ObjectId = objectId;
            row.BackupId = backupId;
            row.ObjectName = objectName;
            row.FullCount = fullCount.ToString();
            row.IncrementalCount = incrementalCount.ToString();
            row.AvgFullSizeBytes = avgFull.ToString();
            row.AvgIncrementalSizeBytes = avgIncremental.ToString();
            row.TotalSizeBytes = totalSize.ToString();
            row.OldestRestorePoint = oldest.ToString("O");
            row.NewestRestorePoint = newest.ToString("O");
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
        public void Build_EmptyInput_ReturnsEmptyList()
        {
            var result = OrphanedSupersededBackupAggregator.Build(new List<dynamic>());

            Assert.Empty(result);
        }
    }
}
