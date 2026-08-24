// <copyright file="CJobSessSummary.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    /// <summary>
    /// Handles the creation and processing of job session summaries for Veeam backup reports.
    /// </summary>
    internal class CJobSessSummary
    {
        private readonly CLogger log = CGlobals.Logger;

        // private readonly CLogger log;
        private readonly Scrubber.CScrubHandler scrubber;
        private readonly CDataTypesParser parsers;

        private readonly string logStart = "[JssBuilder] ";

        /// <summary>
        /// Initializes a new instance of the <see cref="CJobSessSummary"/> class.
        /// </summary>
        /// <param name="log">The logger instance for logging operations.</param>
        /// <param name="scrub">Indicates whether data should be scrubbed.</param>
        /// <param name="scrubber">The scrub handler for data sanitization.</param>
        /// <param name="dp">The data types parser for processing data.</param>
        public CJobSessSummary(CLogger log, bool scrub, Scrubber.CScrubHandler scrubber, CDataTypesParser dp)
        {
            // _xmlFile = xmlFile;
            this.log = log;
            this.scrubber = scrubber;
            this.parsers = dp;
        }

        public List<CJobSummaryTypes> JobSessionSummaryToXml(bool scrub)
        {
            return this.JobSessionSummaryToXml(new CJobSessSummaryHelper(), this.log, scrub, this.scrubber, this.parsers);
        }

        private List<CJobSummaryTypes> JobSessionSummaryToXml(CJobSessSummaryHelper helper, CLogger log, bool scrub, Scrubber.CScrubHandler scrubber, CDataTypesParser d)
        {
            List<List<string>> sendBack = new();
            log.Info("converting job session summary to xml");

            List<CJobSummaryTypes> outList = new();

            List<double> avgRates = new();
            List<double> avgDataSizes = new();
            List<double> avgBackupSizes = new();
            List<double> maxBackupSize = new();
            List<double> maxDataSizes = new();
            List<double> avgDedupRatios = new();
            List<double> avgCompressRatios = new();
            List<int> successRates = new();
            double totalSessions = 0;
            double totalFailedSessions = 0;
            double totalRetries = 0;
            SessionStats totalStats = new();

            // Group all sessions by their stable rollup key. Children inherit the
            // parent's PolicyTag (GUID), so grouping by CSessionGroupKey.Of merges
            // per-machine sessions under the parent without any name parsing.
            // See ADR 0019.
            var groups = helper.JobSessionInfoList()
                .GroupBy(s => CSessionGroupKey.Of(s))
                .Select(g => new
                {
                    // DisplayName: pick the first non-empty PolicyName/JobName in the group.
                    // The filter skips empty values (BC orchestrator parents leave PolicyName
                    // empty - children supply it). Multiple non-empty values in the same group
                    // shouldn't happen because Task 1's PS layer canonicalizes PolicyName via
                    // $jobIdMap; if it ever does (mid-window rename), JobSessionInfoList's
                    // CreationTime descending order means the most recent wins. See ADR 0019.
                    DisplayName = g
                        .Select(s => CSessionGroupKey.DisplayName(s))
                        .FirstOrDefault(n => !string.IsNullOrEmpty(n))
                        ?? (g.First().JobName ?? string.Empty),
                    SessionNames = new HashSet<string>(
                        g.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)),
                        StringComparer.Ordinal),
                })
                .ToList();

            int totalProtectedInstances = 0;
            foreach (var group in groups)
            {
                var j = group.DisplayName;

                // log.Debug( logStart + "Parsing Sessions for job: " + j);
                try
                {
                    CJobSummaryTypes info = helper.SetWaitInfo(j);

                    double sessionCount = 0;
                    double fails = 0;
                    double retries = 0;
                    List<TimeSpan> durations = new();
                    List<string> vmNames = new();
                    List<double> dataSize = new();
                    List<double> backupSize = new();

                    SessionStats thisSession = helper.SessionStats(group.SessionNames);
                    durations = thisSession.JobDuration;
                    vmNames = thisSession.VmNames;
                    dataSize = thisSession.DataSize;
                    backupSize = thisSession.BackupSize;
                    totalSessions += thisSession.SessionCount;
                    sessionCount = thisSession.SessionCount;
                    fails = thisSession.FailCounts;
                    retries = thisSession.RetryCounts;
                    totalFailedSessions += thisSession.FailCounts;
                    totalRetries += thisSession.RetryCounts;
                    // log.Debug(logStart + "Job Type: " + thisSession.JobType + " parsed to: " + info.JobType);
                    CJobCsvInfos jobInfo = null;
                    try
                    {
                        CCsvParser csv = new();
                        jobInfo = csv.JobCsvParser().Where(x => x.Name == j).FirstOrDefault();
                        if (jobInfo != null)
                        {
                            info.UsedVmSizeTB = (jobInfo.OriginalSize ?? 0) / 1024 / 1024 / 1024 / 1024;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error(this.logStart + "Error: ");
                        log.Error(e.ToString());
                        info.UsedVmSizeTB = 0;
                    }

                    // ResolveJobFriendlyType: TypeToString (Veeam-API label, plug-in types) →
                    // GetJobType (enum switch) → raw type. When jobInfo is null (no _Jobs.csv match
                    // or CSV parse failure), fall back to the session-reported type so rolled-up
                    // child sessions keep their own enum value rather than the parent's. ADR 0019, 0020.
                    info.JobType = jobInfo != null
                        ? CJobTypesParser.ResolveJobFriendlyType(jobInfo)
                        : CJobTypesParser.GetJobType(thisSession.JobType);

                    List<TimeSpan> nonZeros = CJobSessSummaryHelper.AddNonZeros(durations);

                    try
                    {
                        info.SessionCount = (int)thisSession.SessionCount;
                        if (sessionCount != 0)
                        {
                            double percent = (sessionCount - fails) / sessionCount * 100;
                            info.SuccessRate = (int)Math.Round(percent, 0, MidpointRounding.ToEven);
                            string sessionInfoString = string.Format(string.Empty +
                                "Total Sessions: {0}, " +
                                "Failed: {1}, " +
                                "Retries: {2}, " +
                                "PercentSuccess: {3}",
                                sessionCount.ToString(),
                                fails,
                                retries,
                                info.SuccessRate);
                            log.Info(this.logStart + "Session Calcuations:\t" + sessionInfoString);

                            if (fails != 0 || retries != 0)
                            {
                            }

                            info.Fails = (int)fails;
                            info.Retries = (int)retries;
                        }

                        successRates.Add((int)info.SuccessRate);
                        if (scrub)
                        {
                            info.JobName = scrubber.ScrubItem(j, Scrubber.ScrubItemType.Job);
                        }
                        else
                        {
                            info.JobName = j;
                        }


                        if (nonZeros.Count != 0)
                        {
                            info.MinJobTime = nonZeros.Min().ToString(@"dd\.hh\:mm\:ss");
                            info.MaxJobTime = nonZeros.Max().ToString(@"dd\.hh\:mm\:ss");
                            var s = new TimeSpan(Convert.ToInt64(nonZeros.Average(ts => ts.Ticks)));
                            info.AvgJobTime = s.ToString(@"dd\.hh\:mm\:ss");
                        }
                        else
                        {
                            info.MinJobTime = string.Empty;
                            info.MaxJobTime = string.Empty;
                            info.AvgJobTime = string.Empty;
                        }

                        info.ItemCount = vmNames.Distinct().Count();
                        totalProtectedInstances = totalProtectedInstances + vmNames.Distinct().Count();

                        info = SetBackupDataSizes(info, dataSize, backupSize, info.UsedVmSizeTB,
                            thisSession.DedupRatios, thisSession.CompressionRatios);

                        avgDataSizes.Add(info.AvgDataSize);
                        avgBackupSizes.Add(info.AvgBackupSize);
                        maxDataSizes.Add(info.MaxDataSize);
                        maxBackupSize.Add(info.MaxBackupSize);
                        avgDedupRatios.Add(info.AvgDedupRatio);
                        avgCompressRatios.Add(info.AvgCompressionRatio);

                        // Compute DCR from incremental-only sessions / source VM size
                        var incrData = thisSession.IncrementalDataSize;
                        double avgIncrDataTB = incrData.Count > 0 ? incrData.Average() / 1024 : 0;

                        if (avgIncrDataTB > 0 && info.UsedVmSizeTB > 0)
                        {
                            info.AvgChangeRate = Math.Round(avgIncrDataTB / info.UsedVmSizeTB * 100, 2);
                        }
                        else if (avgIncrDataTB > 0 && info.MaxDataSize > 0)
                        {
                            info.AvgChangeRate = Math.Round(avgIncrDataTB / info.MaxDataSize * 100, 2);
                        }
                        else
                        {
                            info.AvgChangeRate = 0;
                        }
                        avgRates.Add(info.AvgChangeRate);

                        outList.Add(info);
                    }
                    catch (Exception e)
                    {
                        log.Error(this.logStart + "Error: Session parsing failure");
                        log.Error(e.ToString());
                    }
                }
                catch (Exception e)
                {
                    log.Error(this.logStart + "Error: Failed to parse job sessions for job: " + j);
                    log.Error(e.ToString());
                }
            }

            sendBack = helper.ReturnList(outList, scrub, scrubber);

            outList.Add(helper.SessionSummaryStats(totalSessions, totalFailedSessions, totalRetries, totalProtectedInstances,
                avgBackupSizes, avgDataSizes, maxBackupSize, avgRates, maxDataSizes,
                avgDedupRatios, avgCompressRatios));

            // sendBack.Add(helper.SessionSummaryStats(totalSessions, totalFailedSessions, totalRetries, totalProtectedInstances, 
            //    avgBackupSizes, avgDataSizes, maxBackupSize, avgRates, maxDataSizes));

            log.Info("converting job session summary to xml..done!");
            return outList;
        }

        private static CJobSummaryTypes SetBackupDataSizes(CJobSummaryTypes info, List<double> dataSize, List<double> backupSize, double MaxDataSizeGB,
            List<double> dedupRatios, List<double> compressionRatios)
        {
            if (backupSize.Count != 0)
            {
                info.MinBackupSize = backupSize.Min() / 1024;
                info.MaxBackupSize = Math.Round(backupSize.Max() / 1024, 4);

                // var avg = backupSize.Average();
                // var avg2 = backupSize.Average() / 1024;
                info.AvgBackupSize = Math.Round(backupSize.Average() / 1024, 4);
            }
            else
            {
                info.MinBackupSize = 0;
                info.MaxBackupSize = 0;
                info.AvgBackupSize = 0;
            }

            if (dataSize.Count != 0)
            {
                info.MinDataSize = dataSize.Min() / 1024;
                info.MaxDataSize = Math.Round(dataSize.Max() / 1024, 4);
                info.AvgDataSize = Math.Round(dataSize.Average() / 1024, 4);
                info.UsedVmSizeTB = Math.Round(MaxDataSizeGB, 4);
            }
            else
            {
                info.MinDataSize = 0;
                info.MaxDataSize = 0;
                info.AvgDataSize = 0;
            }

            // Dedup/compression averages
            if (dedupRatios.Count > 0)
                info.AvgDedupRatio = Math.Round(dedupRatios.Average(), 2);
            if (compressionRatios.Count > 0)
                info.AvgCompressionRatio = Math.Round(compressionRatios.Average(), 2);

            return info;
        }
    }
}
