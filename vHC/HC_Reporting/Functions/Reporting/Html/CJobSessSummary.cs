// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    class CJobSessSummary
    {
        private Dictionary<string, List<TimeSpan>> _waits = new();
        private CLogger log = CGlobals.Logger;
        private bool _checkLogs;

        private string _xmlFile;
        private CLogger _log;
        private bool _scrub;
        private Scrubber.CScrubHandler _scrubber;
        private CDataTypesParser _parsers;

        private string logStart = "[JssBuilder] ";

        public CJobSessSummary(CLogger log, bool scrub, Scrubber.CScrubHandler scrubber, CDataTypesParser dp)
        {
            //_xmlFile = xmlFile;
            _log = log;
            _scrubber = scrubber;
            _parsers = dp;

        }

        public List<List<string>> JobSessionSummaryToXml(bool scrub)
        {
            return JobSessionSummaryToXml(_log, scrub, _scrubber, _parsers);
        }
        public List<List<string>> JobSessionSummaryToXml(CLogger log, bool scrub, Scrubber.CScrubHandler scrubber, CDataTypesParser d)
        {
            List<List<string>> sendBack = new();
            log.Info("converting job session summary to xml");

            var targetDate = CGlobals.TOOLSTART.AddDays(-CGlobals.ReportDays);
            List<CJobSessionInfo> trimmedSessionInfo = new();
            using (CDataTypesParser dt = new())
            {
                trimmedSessionInfo = dt.JobSessions.Where(c => c.CreationTime >= targetDate).ToList();
            }

            var bjobsCsvInfo = new CCsvParser().GetDynamicBjobs();


            XElement extElement = new XElement("jobSessionsSummary");

            List<string> jobNameList = trimmedSessionInfo.Select(x => x.Name).ToList();
            List<CJobSummaryTypes> outList = new();

            CCsvParser csv = new();
            IEnumerable<CWaitsCsv> waitList = null;
            if (_checkLogs)
            {
                try
                {
                    waitList = csv.WaitsCsvReader().ToList();
                }
                catch (Exception)
                {
                    log.Error(logStart + "Failed to populate waits.csv. Skipping wait times.");
                    _checkLogs = false;
                }
            }


            List<double> avgRates = new();
            List<double> avgDataSizes = new();
            List<double> avgBackupSizes = new();
            List<double> maxBackupSize = new();
            List<double> maxDataSizes = new();
            List<int> successRates = new();
            double totalSessions = 0;
            double totalFailedSessions = 0;
            double totalRetries = 0;

            int totatlProtectedInstances = 0;
            foreach (var j in jobNameList.Distinct())
            {
                CJobSummaryTypes info = new();

                try
                {
                    List<TimeSpan> tList = new();


                    if (_checkLogs)
                    {
                        foreach (var w in waitList)
                        {
                            string fixedName = j.Replace(" ", "_");

                            if (w.JobName == j || w.JobName == fixedName)
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
                        string mWait = tList.Max().ToString(@"dd\.hh\:mm\:ss");
                        info.maxWait = mWait;

                        var avg = tList.Average(x => x.Ticks);
                        long longAvg = Convert.ToInt64(avg);
                        TimeSpan t = new TimeSpan(longAvg);
                        info.avgwait = t.ToString(@"dd\.hh\:mm\:ss");
                        info.waitCount = tList.Count();
                    }





                }
                catch (Exception e) { }
                double sessionCount = 0;
                double fails = 0;
                double retries = 0;
                List<TimeSpan> durations = new();
                List<string> vmNames = new();
                List<double> dataSize = new();
                List<double> backupSize = new();
                string type = "";

                foreach (var c in trimmedSessionInfo)
                {
                    try
                    {
                        DateTime now = DateTime.Now;
                        double diff = (now - c.CreationTime).TotalDays;
                        ////if (session.CreationTime.Day == now.Day)
                        ////{

                        ////}
                        //if (diff < days)
                        if (j == c.Name && diff < CGlobals.ReportDays)
                        {
                            sessionCount++;
                            totalSessions++;
                            if (c.Status == "Failed")
                            {
                                fails++;
                                totalFailedSessions++;
                            }
                            if (c.IsRetry == "True")
                            {
                                retries++;
                                totalRetries++;
                            }

                            TimeSpan.TryParse(c.JobDuration, out TimeSpan jDur);

                            durations.Add(jDur); //need to parse this to TimeSpan
                            vmNames.Add(c.VmName);

                            dataSize.Add(c.DataSize);
                            backupSize.Add(c.BackupSize);
                            type = c.JobType;
                        }
                    }
                    catch (Exception e) { }

                }
                List<TimeSpan> nonZeros = new();
                foreach (var du in durations)
                {
                    if (durations.Count == 0)
                        nonZeros.Add(du);
                    if (du.Ticks != 0)
                        nonZeros.Add(du);
                }
                //return:
                // , avg duration, min/max, 
                try
                {
                    info.sessionCount = (int)sessionCount;
                    if (sessionCount != 0)
                    {
                        double percent = (sessionCount - fails + retries) / sessionCount * 100;
                        info.SuccessRate = (int)Math.Round(percent, 0, MidpointRounding.ToEven);
                        string sessionInfoString = string.Format("" +
                            "Total Sessions: {0} " +
                            "Failed: {1} " +
                            "Retries: {2} " +
                            "PercentSuccess: {3}",
                            sessionCount.ToString(),
                            fails,
                            retries,
                            info.SuccessRate);
                        log.Info(logStart + "Session Calcuations:\t" + sessionInfoString);
                        if (percent > 100)
                        {// TODO

                        }
                    }

                    successRates.Add(info.SuccessRate);
                    info.JobName = j;

                    if (nonZeros.Count != 0)
                    {
                        info.MinJobTime = nonZeros.Min().ToString(@"hh\:mm\:ss");
                        info.MaxJobTime = nonZeros.Max().ToString(@"hh\:mm\:ss");
                        var s = new TimeSpan(Convert.ToInt64(nonZeros.Average(ts => ts.Ticks)));
                        info.AvgJobTime = s.ToString(@"hh\:mm\:ss");
                    }
                    else
                    {
                        info.MinJobTime = "";
                        info.MaxJobTime = "";
                        info.AvgJobTime = "";
                    }


                    info.ItemCount = vmNames.Distinct().Count();
                    totatlProtectedInstances = totatlProtectedInstances + vmNames.Distinct().Count();

                    info = SetBackupDataSizes(info, dataSize, backupSize);

                    avgDataSizes.Add(info.AvgDataSize);
                    avgBackupSizes.Add(info.AvgBackupSize);
                    maxDataSizes.Add(info.MaxDataSize);
                    maxBackupSize.Add(info.MaxBackupSize);

                    info.JobType = type;

                    //TODO: adjust this to pull VM Original Size, calculate compress & dedupe as well..
                    var originalSize = bjobsCsvInfo.Where(x => x.name == j).Select(x => x.originalsize);

                    if (info.AvgBackupSize != 0 && info.AvgDataSize != 0)
                    {
                        info.AvgChangeRate = Math.Round(info.AvgBackupSize * 2 / info.MaxDataSize * 100, 0);
                        avgRates.Add(info.AvgChangeRate);
                    }
                    else
                    {
                        info.AvgBackupSize = 0;
                        avgRates.Add(0);

                    }


                    outList.Add(info);
                }
                catch (Exception e)
                {

                }
            }

            foreach (var o in outList)
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
                    row.Add(o.AvgJobTime);
                    row.Add(o.MaxJobTime);
                    row.Add(o.MinJobTime);
                    row.Add(o.sessionCount.ToString());
                    row.Add(o.SuccessRate.ToString());
                    row.Add(Math.Round(o.AvgBackupSize, 2).ToString());
                    row.Add(Math.Round(o.MaxBackupSize, 2).ToString());
                    row.Add(Math.Round(o.AvgDataSize, 2).ToString());
                    row.Add(Math.Round(o.AvgChangeRate, 2).ToString());
                    row.Add(Math.Round(o.MaxDataSize, 2).ToString());
                    row.Add(wait);
                    row.Add(o.avgwait);
                    row.Add(o.maxWait);
                    row.Add(o.JobType);

                    sendBack.Add(row);
                }
                catch (Exception e) { }
            }

            double totalSessionSuccessPercent = (totalSessions - totalFailedSessions + totalRetries) / totalSessions * 100;
            List<string> summary = new();
            summary.Add("Total");
            summary.Add(totatlProtectedInstances.ToString());
            summary.Add("");
            summary.Add("");
            summary.Add("");
            summary.Add("");
            summary.Add(Math.Round(totalSessionSuccessPercent, 2).ToString()); //TODO: Make this total sessions - total failed sessions / total session;
            if (avgBackupSizes.Count > 0)
                summary.Add(Math.Round(avgBackupSizes.Average(), 0).ToString());
            else
                summary.Add("0");
            summary.Add(Math.Round(maxBackupSize.Sum(), 0).ToString());

            if (avgDataSizes.Count > 0)
                summary.Add(Math.Round(avgDataSizes.Average(), 0).ToString());
            else
                summary.Add("0");

            if (avgRates.Count > 0)
                summary.Add(Math.Round(avgRates.Average(), 2).ToString());
            else
                summary.Add("0");

            if (maxDataSizes.Count > 0)
                summary.Add(Math.Round(maxDataSizes.Sum(), 2).ToString());
            else
                summary.Add("0");

            sendBack.Add(summary);
            //extElement.Add(ReccomendationText());

            //doc.Save(xmlFile);
            log.Info("converting job session summary to xml..done!");
            return sendBack;
        }

        private static CJobSummaryTypes SetBackupDataSizes(CJobSummaryTypes info, List<double> dataSize, List<double> backupSize)
        {
            if (backupSize.Count != 0)
            {
                info.MinBackupSize = backupSize.Min() / 1024;
                info.MaxBackupSize = backupSize.Max() / 1024;
                info.AvgBackupSize = Math.Round(backupSize.Average() / 1024, 2);
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
                info.MaxDataSize = dataSize.Max() / 1024;
                info.AvgDataSize = Math.Round(dataSize.Average() / 1024, 2);
            }
            else
            {
                info.MinDataSize = 0;
                info.MaxDataSize = 0;
                info.AvgDataSize = 0;
            }
            return info;
        }

        private int SplitDurationToMinutes(string duration)
        {
            try
            {
                //log.Info("splitting duration..");
                int i = 0;
                //00:01:59.4060000
                string[] split = duration.Split(':');
                int hours = 0;
                int minutes = 0;
                int seconds = 0;

                if (split[0] != "00")
                {
                    int.TryParse(split[0], out int h);
                    hours = h;
                }
                if (split[1] != "00")
                {
                    int.TryParse(split[1], out int m);
                    minutes = m;
                }
                minutes = minutes + hours * 60;

                //log.Info("splitting duration..done!");
                return minutes;
            }
            catch (Exception e) { return 0; }
        }
    }
}
