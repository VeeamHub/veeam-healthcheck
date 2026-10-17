using System;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    /// <summary>
    /// Normalized view of a single (BackupId, ObjectId) row from
    /// _orphanedSupersededBackups.csv. Nested inside an
    /// OrphanedSupersededBackupRecord by OrphanedSupersededBackupAggregator.
    /// </summary>
    public class OrphanedSupersededObjectRecord
    {
        public string ObjectId { get; set; }
        public string BackupId { get; set; }
        public string ObjectName { get; set; }
        public int FullCount { get; set; }
        public int IncrementalCount { get; set; }
        public double AvgFullSizeBytes { get; set; }
        public double AvgIncrementalSizeBytes { get; set; }
        public double TotalSizeBytes { get; set; }
        public DateTime OldestRestorePoint { get; set; }
        public DateTime NewestRestorePoint { get; set; }
    }
}
