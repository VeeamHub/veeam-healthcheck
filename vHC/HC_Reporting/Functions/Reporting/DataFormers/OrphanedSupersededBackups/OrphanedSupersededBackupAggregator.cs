using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    /// <summary>
    /// Groups the (BackupId, ObjectId)-grain rows produced by
    /// CCsvParser.GetDynamicOrphanedSupersededBackups() into one
    /// OrphanedSupersededBackupRecord per (RepositoryId, JobName,
    /// CurrentJobId, Category), with each source row nested as an
    /// OrphanedSupersededObjectRecord. Consumes dynamic CSV rows per the
    /// design's dynamic-path decision instead of a strongly-typed CSV DTO.
    /// </summary>
    public static class OrphanedSupersededBackupAggregator
    {
        public static List<OrphanedSupersededBackupRecord> Build(IEnumerable<dynamic> rows)
        {
            var result = new List<OrphanedSupersededBackupRecord>();
            if (rows == null)
            {
                return result;
            }

            var groups = rows
                .Select(MapRow)
                .Where(r => r != null)
                .GroupBy(r => (r.RepositoryId, r.JobName, r.CurrentJobId, r.Category));

            foreach (var group in groups)
            {
                var objects = group.Select(r => r.ObjectRecord).ToList();

                var record = new OrphanedSupersededBackupRecord
                {
                    RepositoryId = group.Key.RepositoryId,
                    RepositoryName = group.First().RepositoryName,
                    JobName = group.Key.JobName,
                    CurrentJobId = group.Key.CurrentJobId,
                    Category = group.Key.Category,
                    OriginalJobType = group.First().OriginalJobType,
                    FullCount = objects.Sum(o => o.FullCount),
                    IncrementalCount = objects.Sum(o => o.IncrementalCount),
                    TotalSizeBytes = objects.Sum(o => o.TotalSizeBytes),
                    OldestRestorePoint = objects.Min(o => o.OldestRestorePoint),
                    NewestRestorePoint = objects.Max(o => o.NewestRestorePoint),
                    Objects = objects,
                };

                result.Add(record);
            }

            return result;
        }

        private static MappedRow MapRow(dynamic row)
        {
            try
            {
                var obj = new OrphanedSupersededObjectRecord
                {
                    ObjectId = row.ObjectId,
                    BackupId = row.BackupId,
                    ObjectName = row.ObjectName,
                    FullCount = int.Parse((string)row.FullCount, CultureInfo.InvariantCulture),
                    IncrementalCount = int.Parse((string)row.IncrementalCount, CultureInfo.InvariantCulture),
                    AvgFullSizeBytes = double.Parse((string)row.AvgFullSizeBytes, CultureInfo.InvariantCulture),
                    AvgIncrementalSizeBytes = double.Parse((string)row.AvgIncrementalSizeBytes, CultureInfo.InvariantCulture),
                    TotalSizeBytes = double.Parse((string)row.TotalSizeBytes, CultureInfo.InvariantCulture),
                    OldestRestorePoint = DateTime.Parse((string)row.OldestRestorePoint, CultureInfo.InvariantCulture),
                    NewestRestorePoint = DateTime.Parse((string)row.NewestRestorePoint, CultureInfo.InvariantCulture),
                };

                return new MappedRow
                {
                    RepositoryId = row.RepositoryId,
                    RepositoryName = row.RepositoryName,
                    JobName = row.JobName,
                    CurrentJobId = row.CurrentJobId,
                    Category = row.Category,
                    OriginalJobType = row.OriginalJobType,
                    ObjectRecord = obj,
                };
            }
            catch
            {
                return null;
            }
        }

        private class MappedRow
        {
            public string RepositoryId { get; set; }
            public string RepositoryName { get; set; }
            public string JobName { get; set; }
            public string CurrentJobId { get; set; }
            public string Category { get; set; }
            public string OriginalJobType { get; set; }
            public OrphanedSupersededObjectRecord ObjectRecord { get; set; }
        }
    }
}
