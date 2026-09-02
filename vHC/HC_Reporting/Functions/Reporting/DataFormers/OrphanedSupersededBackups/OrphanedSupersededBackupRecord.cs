using System;
using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    /// <summary>
    /// One job-level roll-up of Orphaned/Superseded backups, grouped from
    /// _orphanedSupersededBackups.csv rows by
    /// (RepositoryId, JobName, CurrentJobId, Category). Produced by
    /// OrphanedSupersededBackupAggregator.
    /// </summary>
    public class OrphanedSupersededBackupRecord
    {
        public string RepositoryId { get; set; }
        public string RepositoryName { get; set; }
        public string JobName { get; set; }
        public string CurrentJobId { get; set; }
        public string Category { get; set; }
        public string OriginalJobType { get; set; }
        public int FullCount { get; set; }
        public int IncrementalCount { get; set; }
        public double TotalSizeBytes { get; set; }
        public DateTime OldestRestorePoint { get; set; }
        public DateTime NewestRestorePoint { get; set; }
        public List<OrphanedSupersededObjectRecord> Objects { get; set; } = new();
    }
}
