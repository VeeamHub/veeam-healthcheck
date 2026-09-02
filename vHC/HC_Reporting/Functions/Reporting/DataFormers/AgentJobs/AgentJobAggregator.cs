using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.AgentJobs
{
    /// <summary>
    /// Filters _Jobs.csv rows down to Veeam Agent jobs (managed and standalone)
    /// and resolves a human-readable FriendlyType for each.
    /// </summary>
    public static class AgentJobAggregator
    {
        /// <summary>
        /// Raw <c>JobType</c> enum strings that identify a Veeam Agent job in
        /// <c>_Jobs.csv</c>. Consumers (e.g. <c>CJobSummaryTable</c>) reference
        /// this set when they need to exclude agent rows from a generic
        /// per-<c>JobType</c> loop, so the agent-type list stays in one place.
        /// </summary>
        public static readonly IReadOnlySet<string> AgentJobTypes = new HashSet<string>
        {
            "EpAgentBackup",
            "EpAgentPolicy",
            "EpAgentManagement",
            "ELinuxPhysical",
            "EndpointBackup",
        };

        /// <summary>
        /// <para>
        /// Canonical human-readable labels used to seed zero-count entries in the
        /// Missing Jobs section. Consumers apply these selectively:
        /// </para>
        /// <list type="bullet">
        ///   <item><c>Agent Standalone</c> — always seeded; standalone agent jobs are an
        ///   independent category from managed jobs.</item>
        ///   <item><c>Agent Backup</c> — seeded only when no agent jobs exist at all;
        ///   skipped when modern agent jobs (EpAgentBackup etc.) are present to avoid a
        ///   false-positive missing-job alert.</item>
        /// </list>
        /// </summary>
        public static readonly IReadOnlySet<string> DefaultFriendlyTypes = new HashSet<string>
        {
            "Agent Backup",
            "Agent Standalone",
        };

        public static IReadOnlyList<AgentJobRecord> Build(IEnumerable<CJobCsvInfos> rows)
        {
            if (rows == null)
            {
                return new List<AgentJobRecord>();
            }

            return rows
                .Where(r => r != null && r.JobType != null && AgentJobTypes.Contains(r.JobType))
                .Select(MapRow)
                .ToList();
        }

        private static AgentJobRecord MapRow(CJobCsvInfos r)
        {
            bool gfsEnabled = r.GfsMonthlyEnabled || r.GfsWeeklyIsEnabled || r.GfsYearlyEnabled;
            var gfsDetailParts = new List<string>();
            if (r.GfsWeeklyIsEnabled)
            {
                gfsDetailParts.Add($"Weekly:{r.GfsWeeklyCount}");
            }
            if (r.GfsMonthlyEnabled)
            {
                gfsDetailParts.Add($"Monthly:{r.GfsMonthlyCount}");
            }
            if (r.GfsYearlyEnabled)
            {
                gfsDetailParts.Add($"Yearly:{r.GfsYearlyCount}");
            }

            string compressionLevel = r.CompressionLevel switch
            {
                "9" => "Extreme",
                "6" => "High",
                "5" => "Optimal",
                "4" => "Dedupe-Friendly",
                "0" => "None",
                _ => r.CompressionLevel,
            };

            string blockSize = r.BlockSize switch
            {
                "KbBlockSize1024" => "1 MB",
                "KbBlockSize512" => "512 KB",
                "KbBlockSize256" => "256 KB",
                "KbBlockSize4096" => "4 MB",
                "KbBlockSize8192" => "8 MB",
                _ => r.BlockSize,
            };

            bool syntheticFull = r.Algorithm == "Increment" && r.TransformFullToSyntethic;
            string backupChainType = r.Algorithm == "Syntethic" ? "Reverse Incremental" : "Forward Incremental";
            bool indexingEnabled = r.IndexingType != null && r.IndexingType != "None";
            string retentionScheme = r.RetentionType == "Cycles" ? "Points" : r.RetentionType;
            string retainDays = r.RetentionType == "Cycles" ? r.RetentionCount : r.RetainDaysToKeep;

            return new AgentJobRecord
            {
                JobName = r.Name,
                JobType = r.JobType,
                FriendlyType = ResolveFriendlyType(r),
                RepoName = r.RepoName,
                SourceSizeGB = System.Math.Round((r.OriginalSize ?? 0) / 1024.0 / 1024.0 / 1024.0, 2),
                OnDiskGB = System.Math.Round(r.OnDiskGB ?? 0, 2),
                RetentionScheme = retentionScheme,
                RetainDays = retainDays,
                Encrypted = r.StgEncryptionEnabled,
                CompressionLevel = compressionLevel,
                BlockSize = blockSize,
                GfsEnabled = gfsEnabled,
                GfsDetails = gfsEnabled ? string.Join(",", gfsDetailParts) : string.Empty,
                ActiveFullEnabled = r.EnableFullBackup.ToString(),
                SyntheticFullEnabled = syntheticFull,
                BackupChainType = backupChainType,
                IndexingEnabled = indexingEnabled,
                AAIPEnabled = r.AAIPEnabled ?? "",
                VSSEnabled = r.VSSEnabled ?? "",
                VSSIgnoreErrors = r.VSSIgnoreErrors ?? "",
                GuestFSIndexing = r.GuestFSIndexingEnabled ?? "",
                Platform = r.Platform ?? "",
            };
        }

        private static string ResolveFriendlyType(CJobCsvInfos row)
        {
            string baseLabel = !string.IsNullOrEmpty(row.TypeToString)
                ? row.TypeToString
                : CJobTypesParser.GetJobType(row.JobType);

            if (row.JobType == "EndpointBackup")
            {
                return ToStandaloneLabel(baseLabel);
            }

            return baseLabel;
        }

        private static string ToStandaloneLabel(string baseLabel)
        {
            if (string.IsNullOrEmpty(baseLabel))
            {
                return "Agent Standalone";
            }

            const string backupSuffix = " Backup";
            if (baseLabel.EndsWith(backupSuffix, System.StringComparison.Ordinal))
            {
                return baseLabel.Substring(0, baseLabel.Length - backupSuffix.Length) + " Standalone";
            }

            return baseLabel + " Standalone";
        }
    }
}
