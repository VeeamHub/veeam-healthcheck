using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

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
        private static readonly CLogger Log = CGlobals.Logger;
        private static readonly string LogPrefix = "[OrphanedSupersededBackupAggregator]\t";

        // Matches the invariant "o" (round-trip) format
        // Get-VhcOrphanedSupersededBackups.ps1 now explicitly pins for
        // OldestRestorePoint/NewestRestorePoint before Export-Csv, instead of
        // relying on Export-Csv's implicit current-culture ToString(). Must
        // stay in lock-step with that script's
        // CreationTimeUtc.ToString('o', [CultureInfo]::InvariantCulture) call.
        private const string DateWireFormat = "o";

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
            object rawFullCount;
            object rawIncrementalCount;
            object rawAvgFull;
            object rawAvgIncremental;
            object rawTotalSize;
            object rawOldest;
            object rawNewest;

            try
            {
                rawFullCount = row.FullCount;
                rawIncrementalCount = row.IncrementalCount;
                rawAvgFull = row.AvgFullSizeBytes;
                rawAvgIncremental = row.AvgIncrementalSizeBytes;
                rawTotalSize = row.TotalSizeBytes;
                rawOldest = row.OldestRestorePoint;
                rawNewest = row.NewestRestorePoint;
            }
            catch (Exception ex)
            {
                Log.Warning($"{LogPrefix}Dropping a row from _orphanedSupersededBackups.csv: could not read its columns: {ex.Message}");
                return null;
            }

            // TryParse/TryParseExact, not the throwing Parse overloads: a
            // malformed value in one row must drop only that row, with a
            // diagnostic saying which field and what the raw value was, not
            // silently vanish into a generic catch (the same "silent drop"
            // failure mode the PS1 script's null-ObjectId handling was
            // written to avoid - see that script's comments).
            if (!int.TryParse((string)rawFullCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fullCount))
            {
                return LogAndDrop("FullCount", rawFullCount);
            }
            if (!int.TryParse((string)rawIncrementalCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int incrementalCount))
            {
                return LogAndDrop("IncrementalCount", rawIncrementalCount);
            }
            if (!double.TryParse((string)rawAvgFull, NumberStyles.Float, CultureInfo.InvariantCulture, out double avgFullSizeBytes))
            {
                return LogAndDrop("AvgFullSizeBytes", rawAvgFull);
            }
            if (!double.TryParse((string)rawAvgIncremental, NumberStyles.Float, CultureInfo.InvariantCulture, out double avgIncrementalSizeBytes))
            {
                return LogAndDrop("AvgIncrementalSizeBytes", rawAvgIncremental);
            }
            if (!double.TryParse((string)rawTotalSize, NumberStyles.Float, CultureInfo.InvariantCulture, out double totalSizeBytes))
            {
                return LogAndDrop("TotalSizeBytes", rawTotalSize);
            }

            // ParseExact against the exact "o" wire format the PS1 producer
            // now pins explicitly (DateTime.Parse would otherwise still
            // accept - and misparse - a current-culture-formatted date if one
            // ever slipped through, e.g. day/month swapped on a dd/MM host).
            if (!DateTime.TryParseExact((string)rawOldest, DateWireFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime oldestRestorePoint))
            {
                return LogAndDrop("OldestRestorePoint", rawOldest);
            }
            if (!DateTime.TryParseExact((string)rawNewest, DateWireFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime newestRestorePoint))
            {
                return LogAndDrop("NewestRestorePoint", rawNewest);
            }

            try
            {
                var obj = new OrphanedSupersededObjectRecord
                {
                    ObjectId = row.ObjectId,
                    BackupId = row.BackupId,
                    ObjectName = row.ObjectName,
                    FullCount = fullCount,
                    IncrementalCount = incrementalCount,
                    AvgFullSizeBytes = avgFullSizeBytes,
                    AvgIncrementalSizeBytes = avgIncrementalSizeBytes,
                    TotalSizeBytes = totalSizeBytes,
                    OldestRestorePoint = oldestRestorePoint,
                    NewestRestorePoint = newestRestorePoint,
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
            catch (Exception ex)
            {
                Log.Warning($"{LogPrefix}Dropping a row from _orphanedSupersededBackups.csv: unexpected error mapping its remaining columns: {ex.Message}");
                return null;
            }
        }

        private static MappedRow LogAndDrop(string fieldName, object rawValue)
        {
            Log.Warning($"{LogPrefix}Dropping a row from _orphanedSupersededBackups.csv: could not parse column '{fieldName}' (raw value: '{rawValue}').");
            return null;
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
