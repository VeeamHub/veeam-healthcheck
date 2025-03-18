// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    class CJobSessSummary
    {
        private Dictionary<string, List<TimeSpan>> _waits = new();
        private CLogger log = CGlobals.Logger;

        private CLogger _log;
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

        public List<CJobSummaryTypes> JobSessionSummaryToXml(bool scrub)
        {
            return JobSessionSummaryToXml(new CJobSessSummaryHelper(), _log, scrub, _scrubber, _parsers);
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
            List<int> successRates = new();
            double totalSessions = 0;
            double totalFailedSessions = 0;
            double totalRetries = 0;
            SessionStats totalStats = new();


            int totalProtectedInstances = 0;
            foreach (var j in helper.JobNameList().Distinct())
           {
                CJobSummaryTypes info = helper.SetWaitInfo(j);

                double sessionCount = 0;
                double fails = 0;
                double retries = 0;
                List<TimeSpan> durations = new();
                List<string> vmNames = new();
                List<double> dataSize = new();
                List<double> backupSize = new();

                SessionStats thisSession = helper.SessionStats(j);
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
                info.JobType = CJobTypeConversion.ReturnJobType(thisSession.JobType);
                try
                {
                    CCsvParser csv = new();
                    var jobInfo = csv.JobCsvParser().Where(x => x.Name == j).FirstOrDefault();
                    if(jobInfo != null)
                        info.UsedVmSizeTB = jobInfo.OriginalSize /1024 /1024 /1024 / 1024;
                }
                catch (Exception e)
                {
                    info.UsedVmSizeTB = 0;
                }

                List<TimeSpan> nonZeros = helper.AddNonZeros(durations);
                
                try
                {
                    info.sessionCount = (int)thisSession.SessionCount;
                    if (sessionCount != 0)
                    {
                        double percent = (sessionCount - fails + retries) / sessionCount * 100;
                        info.SuccessRate = (int)Math.Round(percent, 0, MidpointRounding.ToEven);
                        string sessionInfoString = string.Format("" +
                            "Total Sessions: {0}, " +
                            "Failed: {1}, " +
                            "Retries: {2}, " +
                            "PercentSuccess: {3}",
                            sessionCount.ToString(),
                            fails,
                            retries,
                            info.SuccessRate);
                        log.Info(logStart + "Session Calcuations:\t" + sessionInfoString);
                        if (percent > 100)
                        {// TODO: if percent greater than 100, set to 100
                            percent = 100;
                        }
                        if(fails != 0 || retries != 0)
                        {

                        }
                        info.Fails = (int)fails;
                        info.Retries = (int)retries;
                    }

                    successRates.Add((int)info.SuccessRate);
                    if (scrub)
                        info.JobName = scrubber.ScrubItem(j, Scrubber.ScrubItemType.Job);
                    else
                        info.JobName = j;

                    if (nonZeros.Count != 0)
                    {
                        info.MinJobTime = nonZeros.Min().ToString(@"dd\.hh\:mm\:ss");
                        info.MaxJobTime = nonZeros.Max().ToString(@"dd\.hh\:mm\:ss");
                        var s = new TimeSpan(Convert.ToInt64(nonZeros.Average(ts => ts.Ticks)));
                        info.AvgJobTime = s.ToString(@"dd\.hh\:mm\:ss");
                    }
                    else
                    {
                        info.MinJobTime = "";
                        info.MaxJobTime = "";
                        info.AvgJobTime = "";
                    }


                    info.ItemCount = vmNames.Distinct().Count();
                    totalProtectedInstances = totalProtectedInstances + vmNames.Distinct().Count();

                    info = SetBackupDataSizes(info, dataSize, backupSize, info.UsedVmSizeTB);

                    avgDataSizes.Add(info.AvgDataSize);
                    avgBackupSizes.Add(info.AvgBackupSize);
                    maxDataSizes.Add(info.MaxDataSize);
                    maxBackupSize.Add(info.MaxBackupSize);


                    if (info.AvgBackupSize != 0 && info.AvgDataSize != 0)
                    {
                        if(info.AvgDataSize > info.UsedVmSizeTB)
                        {
                            info.AvgChangeRate = Math.Round(info.AvgDataSize / info.MaxDataSize * 100, 2);
                            avgRates.Add(info.AvgChangeRate);
                        }
                        else
                        {
                            info.AvgChangeRate = Math.Round(info.AvgDataSize / info.UsedVmSizeTB * 100, 2);
                            avgRates.Add(info.AvgChangeRate);
                        }
                        
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

            

            sendBack =  helper.ReturnList(outList, scrub, scrubber);

            outList.Add(helper.SessionSummaryStats(totalSessions, totalFailedSessions, totalRetries, totalProtectedInstances,
                avgBackupSizes, avgDataSizes, maxBackupSize, avgRates, maxDataSizes));
            
            //sendBack.Add(helper.SessionSummaryStats(totalSessions, totalFailedSessions, totalRetries, totalProtectedInstances, 
            //    avgBackupSizes, avgDataSizes, maxBackupSize, avgRates, maxDataSizes));



            log.Info("converting job session summary to xml..done!");
            return outList;
        }

        private static CJobSummaryTypes SetBackupDataSizes(CJobSummaryTypes info, List<double> dataSize, List<double> backupSize, double MaxDataSizeGB)
        {
            if (backupSize.Count != 0)
            {
                info.MinBackupSize = backupSize.Min() / 1024;
                info.MaxBackupSize = Math.Round(backupSize.Max() / 1024, 4);
                //var avg = backupSize.Average();
                //var avg2 = backupSize.Average() / 1024;
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
