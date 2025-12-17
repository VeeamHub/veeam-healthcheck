// <copyright file="CJobSessSummaryHelper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary
{
    /// <summary>
    /// Helper class for managing job session summary data and operations.
    /// </summary>
    internal class CJobSessSummaryHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CJobSessSummaryHelper"/> class.
        /// </summary>
        public CJobSessSummaryHelper()
        {
        }

        /// <summary>
        /// Sets wait information for a specific job by analyzing wait times and calculating statistics.
        /// </summary>
        /// <param name="jobName">The name of the job to analyze wait information for.</param>
        /// <returns>A CJobSummaryTypes object containing wait statistics including max wait, average wait, and wait count.</returns>
        public CJobSummaryTypes SetWaitInfo(string jobName)
        {
            List<TimeSpan> tList = this.GetWaitTimes(jobName);

            var summary = new CJobSummaryTypes();

            if (tList.Count != 0)
            {
                summary.MaxWait = this.GetMaxWaitAsString(tList.Max());
            }
            else
            {
                summary.MaxWait = "0";
            }


            summary.AvgWait = this.GetAverageWaitAsString(tList);
            summary.WaitCount = tList.Count();

            return summary;
        }

        private List<TimeSpan> GetWaitTimes(string jobName)
        {
            try
            {
                CCsvParser csv = new();
                IEnumerable<CWaitsCsv> waitList = null;
                var rawCsv = csv.WaitsCsvReader();
                if (rawCsv != null) { waitList = rawCsv.ToList(); }

                List<TimeSpan> tList = new();

                foreach (var w in waitList)
                {
                    string fixedName = jobName.Replace(" ", "_");

                    if (w.JobName == jobName || w.JobName == fixedName)
                    {
                        DateTime.TryParse(w.StartTime, out DateTime startTime);
                        DateTime.TryParse(w.EndTime, out DateTime endTime);
                        DateTime now = DateTime.Now;
                        double startDiff = (now - startTime).TotalDays;
                        double endDiff = (now - endTime).TotalDays;

                        if (endDiff < CGlobals.ReportDays || startDiff < CGlobals.ReportDays)
                        {
                            TimeSpan.TryParse(w.Duration, out TimeSpan duration);
                            tList.Add(duration);
                        }
                    }
                }

                return tList;
            }
            catch (Exception)
            {
                return new List<TimeSpan>();
            }
        }

        private string GetMaxWaitAsString(TimeSpan maxTime)
        {
            return maxTime.ToString(@"dd\.hh\:mm\:ss");
        }

        private string GetAverageWaitAsString(List<TimeSpan> timeList)
        {
            if (timeList.Count == 0)
                return "0";
            else
            {
                var avg = timeList.Average(x => x.Ticks);
                long longAvg = Convert.ToInt64(avg);
                TimeSpan t = new TimeSpan(longAvg);
                return t.ToString(@"dd\.hh\:mm\:ss");
            }
        }

        public List<string> JobNameList()
        {
            return this.JobSessionInfoList().Select(x => x.Name).ToList();
        }

        public List<CJobSessionInfo> JobSessionInfoList()
        {
                List<CJobSessionInfo> csv = new();
                var res = CGlobals.DtParser.JobSessions; // .Where(c => c.CreationTime >= targetDate).ToList();
                if (res == null)
                {
                    CGlobals.Logger.Debug("! No Job Sessions Found !");
                    return csv;
                }

                else
                {
                    csv = res.Where(c => c.CreationTime >= this.TargetDate()).ToList();

                    // csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    csv = csv.OrderBy(x => x.Name).ToList();

                    csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return csv;
                }
        }

        private DateTime TargetDate()
        {
            return CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);
        }

        public SessionStats SessionStats(string jobName)
        {
            SessionStats stats = new SessionStats();

            foreach (var session in this.JobSessionInfoList())
            {
                double diff = (DateTime.Now - session.CreationTime).TotalDays;
                if (jobName == session.Name && diff < CGlobals.ReportDays)
                {
                    stats.SessionCount++;
                    if (session.Status == "Failed")
                    {
                        stats.FailCounts++;
                    }


                    if (session.IsRetry == "True")
                    {
                        stats.RetryCounts++;
                    }


                    stats.JobType = session.JobType;
                    if (session.JobDuration.StartsWith("1"))
                    {
                    }

                    _ = TimeSpan.TryParse(session.JobDuration, out TimeSpan jDur);

                    stats.JobDuration.Add(jDur); // need to parse this to TimeSpan
                    stats.VmNames.Add(session.VmName);

                    stats.DataSize.Add(session.DataSize);
                    stats.BackupSize.Add(session.BackupSize);
                }
            }

            return stats;
        }

        public static List<TimeSpan> AddNonZeros(List<TimeSpan> jobDurationList)
        {
            List<TimeSpan> nonZeros = new();
            foreach (var du in jobDurationList)
            {
                if (jobDurationList.Count == 0)
                {
                    nonZeros.Add(du);
                }

                if (du.Ticks != 0)
                {
                    nonZeros.Add(du);
                }
            }

            return nonZeros;
        }

        public List<List<string>> ReturnList(List<CJobSummaryTypes> list, bool scrub, CScrubHandler scrubber)
        {
            List<List<string>> sendBack = new();
            foreach (var o in list)
            {
                List<string> row = new();
                try
                {
                    string jname = o.JobName;
                    if (scrub)
                    {
                        jname = scrubber.ScrubItem(o.JobName, ScrubItemType.Job);
                    }


                    string wait = o.WaitCount.ToString();
                    if (o.WaitCount == 0)
                    {
                        wait = string.Empty;
                    }


                    row.Add(jname);
                    row.Add(o.ItemCount.ToString());
                    row.Add(o.MinJobTime);
                    row.Add(o.MaxJobTime);
                    row.Add(o.AvgJobTime);
                    row.Add(o.SessionCount.ToString());
                    row.Add(o.Fails.ToString());
                    row.Add(o.Retries.ToString());
                    row.Add(o.SuccessRate.ToString());
                    row.Add(Math.Round(o.AvgBackupSize, 2).ToString());
                    row.Add(Math.Round(o.MaxBackupSize, 2).ToString());
                    row.Add(Math.Round(o.AvgDataSize, 2).ToString());
                    row.Add(Math.Round(o.MaxDataSize, 2).ToString());
                    row.Add(Math.Round(o.AvgChangeRate, 2).ToString());
                    row.Add(wait);
                    row.Add(o.MaxWait);
                    row.Add(o.AvgWait);
                    row.Add(o.JobType);

                    sendBack.Add(row);
                }
                catch (Exception) { }
            }

            return sendBack;
        }

        public CJobSummaryTypes SessionSummaryStats(double totalSessions, double totalFailedSessions,
            double totalRetries, int totalProtectedInstances, List<double> avgBackupSizes, List<double> avgDataSizes,
            List<double> maxBackupSize, List<double> avgRates, List<double> maxDataSizes)
        {
            CJobSummaryTypes jobSummaryTypes = new CJobSummaryTypes();

            double totalSessionSuccessPercent = (totalSessions - totalFailedSessions + totalRetries) / totalSessions * 100;
            double successPercent = Math.Round(totalSessionSuccessPercent, 2);

            avgRates.RemoveAll(x => x == 0);

            jobSummaryTypes.JobName = "Total";
            jobSummaryTypes.ItemCount = totalProtectedInstances;
            jobSummaryTypes.SessionCount = (int)totalSessions;
            jobSummaryTypes.SuccessRate = successPercent;
            jobSummaryTypes.Fails = (int)totalFailedSessions;
            jobSummaryTypes.Retries = (int)totalRetries;
            jobSummaryTypes.AvgBackupSize = Math.Round(avgBackupSizes.Sum(), 2);
            jobSummaryTypes.MaxBackupSize = Math.Round(maxBackupSize.Sum(), 2);
            jobSummaryTypes.AvgDataSize = Math.Round(avgDataSizes.Sum(), 2);
            jobSummaryTypes.MaxDataSize = Math.Round(maxDataSizes.Sum(), 2);
            var avgChangedData = avgDataSizes.Sum() / maxDataSizes.Sum() * 100;
            jobSummaryTypes.AvgChangeRate = Math.Round(avgChangedData, 2);

            return jobSummaryTypes;
        }
    }
}