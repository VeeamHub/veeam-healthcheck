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
    internal class CJobSessSummaryHelper
    {
        public CJobSessSummaryHelper()
        {

        }
        public void ReturnOriginalSize(string jobName)
        {
            var bjobsCsvInfo = new CCsvParser().GetDynamicBjobs();

            //TODO: adjust this to pull VM Original Size, calculate compress & dedupe as well..
            if (bjobsCsvInfo != null)
            {
                var originalSize = bjobsCsvInfo.Where(x => x.name == jobName).Select(x => x.originalsize);
                //return int.TryParse(originalSize.ToString(), out int i);

            }
        }
        public CJobSummaryTypes SetWaitInfo(string jobName)
        {
            List<TimeSpan> tList = GetWaitTimes(jobName);

            var summary = new CJobSummaryTypes();

            if (tList.Count != 0)
                summary.maxWait = GetMaxWaitAsString(tList.Max());
            else
                summary.maxWait = "0";
            summary.avgwait = GetAverageWaitAsString(tList);
            summary.waitCount = tList.Count();

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
            catch (Exception ex)
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
            return JobSessionInfoList().Select(x => x.Name).ToList();
        }
        public List<CJobSessionInfo> JobSessionInfoList()
        {
            using (CDataTypesParser dt = new())
            {
                List<CJobSessionInfo> csv = new();
                var res = dt.JobSessions; //.Where(c => c.CreationTime >= targetDate).ToList();
                if (res == null)
                    return csv;
                else
                {
                    csv = res.Where(c => c.CreationTime >= TargetDate()).ToList();
                    //csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    csv = csv.OrderBy(x => x.Name).ToList();

                    csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return csv;
                }
            }
        }
        private DateTime TargetDate()
        {
            return CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);
        }
        public SessionStats SessionStats(string jobName)
        {
            SessionStats stats = new SessionStats();

            foreach (var session in JobSessionInfoList())
            {
                double diff = (DateTime.Now - session.CreationTime).TotalDays;
                if (jobName == session.Name && diff < CGlobals.ReportDays)
                {

                    stats.SessionCount++;
                    if (session.Status == "Failed")
                        stats.FailCounts++;
                    if (session.IsRetry == "True")
                        stats.RetryCounts++;

                    stats.JobType = session.JobType;
                    TimeSpan.TryParse(session.JobDuration, out TimeSpan jDur);

                    stats.JobDuration.Add(jDur); //need to parse this to TimeSpan
                    stats.VmNames.Add(session.VmName);

                    stats.DataSize.Add(session.DataSize);
                    stats.BackupSize.Add(session.BackupSize);
                }

            }



            return stats;
        }
        public List<TimeSpan> AddNonZeros(List<TimeSpan> jobDurationList)
        {
            List<TimeSpan> nonZeros = new();
            foreach (var du in jobDurationList)
            {
                if (jobDurationList.Count == 0)
                    nonZeros.Add(du);
                if (du.Ticks != 0)
                    nonZeros.Add(du);
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
                        jname = scrubber.ScrubItem(o.JobName, "job");

                    string wait = o.waitCount.ToString();
                    if (o.waitCount == 0)
                        wait = "";

                    row.Add(jname);
                    row.Add(o.ItemCount.ToString());
                    row.Add(o.MinJobTime);
                    row.Add(o.MaxJobTime);
                    row.Add(o.AvgJobTime);
                    row.Add(o.sessionCount.ToString());
                    row.Add(o.SuccessRate.ToString());
                    row.Add(Math.Round(o.AvgBackupSize, 2).ToString());
                    row.Add(Math.Round(o.MaxBackupSize, 2).ToString());
                    row.Add(Math.Round(o.AvgDataSize, 2).ToString());
                    row.Add(Math.Round(o.MaxDataSize, 2).ToString());
                    row.Add(Math.Round(o.AvgChangeRate, 2).ToString());
                    row.Add(wait);
                    row.Add(o.maxWait);
                    row.Add(o.avgwait);
                    row.Add(o.JobType);

                    sendBack.Add(row);
                }
                catch (Exception e) { }
            }
            return sendBack;
        }
        public List<string> SessionSummaryStats(double totalSessions, double totalFailedSessions,
            double totalRetries, int totalProtectedInstances, List<double> avgBackupSizes, List<double> avgDataSizes,
            List<double> maxBackupSize, List<double> avgRates, List<double> maxDataSizes)
        {
            double totalSessionSuccessPercent = (totalSessions - totalFailedSessions + totalRetries) / totalSessions * 100;
            List<string> summary = new()
            {
                "Total",
                totalProtectedInstances.ToString(),
                "",
                "",
                "",
                totalSessions.ToString() , //total sessions
                Math.Round(totalSessionSuccessPercent, 2).ToString() //TODO: Make this total sessions - total failed sessions / total session;
            };
            if (avgBackupSizes.Count > 0)
                summary.Add(Math.Round(avgBackupSizes.Average(), 0).ToString());
            else
                summary.Add("0");
            summary.Add(Math.Round(maxBackupSize.Sum(), 0).ToString());

            if (maxBackupSize.Count > 0)
                summary.Add(Math.Round(maxBackupSize.Max(), 0).ToString());
            else summary.Add("0");

            if (avgDataSizes.Count > 0)
                summary.Add(Math.Round(avgDataSizes.Average(), 0).ToString());
            else
                summary.Add("0");
            if (maxDataSizes.Count > 0)
                summary.Add(Math.Round(maxDataSizes.Sum(), 2).ToString());
            else
                summary.Add("0");

            if (avgRates.Count > 0)
                summary.Add(Math.Round(avgRates.Average(), 2).ToString());
            else
                summary.Add("0");



            return summary;
        }
    }
}