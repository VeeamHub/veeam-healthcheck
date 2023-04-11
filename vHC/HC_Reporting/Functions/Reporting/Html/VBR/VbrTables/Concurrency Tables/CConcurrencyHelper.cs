using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    internal class CConcurrencyHelper
    {
        private CDataTypesParser _dTypeParser;
        private readonly CLogger log = CGlobals.Logger;
        public CConcurrencyHelper()
        {
            _dTypeParser= new CDataTypesParser();
        }

        public List<ConcurentTracker> TaskCounter(List<CJobSessionInfo> trimmedSessionInfo)
        {
            List<ConcurentTracker> ctList = new();
            foreach (var session in trimmedSessionInfo)
            {
                DateTime now = DateTime.Now;
                double diff = (now - session.CreationTime).TotalDays;
                if (diff < CGlobals.ReportDays)
                {
                    ctList.Add(ParseConcurrency(session, 7));

                }

            }

            return ctList;
        }
        public List<ConcurentTracker> JobCounter(List<CJobSessionInfo> trimmedSessionInfo)
        {
            List<CJobTypeInfos> jobInfo = _dTypeParser.JobInfos;
            List<ConcurentTracker> ctList = new();
            List<string> mirrorJobNamesList = new();
            List<string> nameDatesList = new();
            List<string> mirrorJobBjobList = new();
            List<string> backupSyncNameList = new();
            List<string> epAgentBackupList = new();
            foreach (var backup in jobInfo)
            {
                if (backup.JobType == "SimpleBackupCopyPolicy")
                    mirrorJobBjobList.Add(backup.Name);
                if (backup.JobType == "BackupSync")
                    backupSyncNameList.Add(backup.Name);
                if (backup.JobType == "EpAgentBackup")
                    epAgentBackupList.Add(backup.Name);
            }


            foreach (var m in mirrorJobBjobList)
            {
                var mirrorSessions = trimmedSessionInfo.Where(y => y.JobName.StartsWith(m));

                foreach (var sess in mirrorSessions)
                {
                    //int i = mirrorSessions.Count();
                    DateTime now = DateTime.Now;
                    double diff = (now - sess.CreationTime).TotalDays;
                    if (diff < CGlobals.ReportDays)
                    {
                        mirrorJobNamesList.Add(sess.JobName);
                        string nameDate = sess.JobName + sess.CreationTime.ToString();
                        if (!nameDatesList.Contains(nameDate))
                        {
                            nameDatesList.Add(nameDate);
                            ctList.Add(ParseConcurrency(sess, 7));
                        }
                    }
                }
            }

            var backupSyncJobs = jobInfo.Where(x => x.JobType == "BackupSync");
            foreach (var b in backupSyncJobs)
            {
                var v = trimmedSessionInfo.Where(x => x.JobName == b.Name);

                foreach (var s in v)
                {
                    try
                    {
                        string[] n = s.JobName.Split("\\");
                        string bcjName = b.Name;
                        if (!nameDatesList.Contains(bcjName))
                        {
                            nameDatesList.Add(bcjName);

                            ctList.Add(ParseConcurrency(s, 7));
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error("Failed to parse BackupSync job. Error:");
                        log.Error(e.Message);
                    }
                }
            }

            var epAgentBackupJobs = jobInfo.Where(x => x.JobType == "EpAgentBackup");
            foreach (var e in epAgentBackupJobs)
            {
                var epBcj = trimmedSessionInfo.Where(x => x.JobType == "EEndPoint");

                foreach (var epB in epBcj)
                {
                    if (!mirrorJobNamesList.Contains(epB.JobName))
                    {
                        string[] n = epB.JobName.Split(" - ");
                        string n1 = n[0] + epB.CreationTime.ToString();
                        if (!nameDatesList.Contains(n1))
                        {
                            nameDatesList.Add(n1);
                            ctList.Add(ParseConcurrency(epB, 7));
                        }
                    }

                }
            }
            foreach (var b in jobInfo)
            {

                var remainingSessions = trimmedSessionInfo.Where(x => x.JobName.Equals(b.Name));
                foreach (var sess in remainingSessions)
                {
                    string nameDate = sess.JobName + sess.CreationTime.ToString();
                    if (!nameDatesList.Contains(nameDate))
                    {
                        nameDatesList.Add(nameDate);
                        ctList.Add(ParseConcurrency(sess, 7));
                    }
                }

            }
            return ctList;
        }
        private Dictionary<DayOfWeek, Dictionary<int,int>> ConcurrencyDictionary(List<ConcurentTracker> cTrackerList)
        {
            Dictionary<DayOfWeek, Dictionary<int, int>> concurrencyDictionary = new();
            foreach (var c in cTrackerList)
            {

                if (!concurrencyDictionary.ContainsKey(c.DayofTheWeeek))
                {
                    Dictionary<int, int> minuteMapper = new();
                    foreach (var c2 in cTrackerList)
                    {

                        if (c2.Date.DayOfWeek == c.Date.DayOfWeek)
                        {
                            var ticks = c2.Duration.TotalMinutes;
                            int hMinute = c2.hourMinute;

                            for (int i = 0; i < ticks; i++)
                            {
                                int current2;

                                minuteMapper.TryGetValue(hMinute, out current2);
                                minuteMapper[hMinute] = current2 + 1;
                                hMinute++;
                            }
                        }
                    }
                    Dictionary<int, int> hoursAndCount = new();

                    for (int hour = 0; hour < 24; hour++)
                    {
                        int highestCount = 0;
                        foreach (var h in minuteMapper.Keys)
                        {
                            var p = Math.Round((decimal)h / 60, 0, MidpointRounding.ToZero);
                            if (hour == p)
                            {
                                int minutesSubtract = hour * 60;
                                int minutes = h - minutesSubtract;

                                minuteMapper.TryGetValue(h, out int counter);
                                if (counter > highestCount || highestCount == 0)
                                {
                                    highestCount = counter;

                                }
                            }
                        }
                        hoursAndCount.Add(hour, highestCount);
                    }

                    concurrencyDictionary.Add(c.DayofTheWeeek, hoursAndCount);

                }

            }
            return concurrencyDictionary;
        }
        public Dictionary<int, string[]> FinalConcurrency(List<ConcurentTracker> cTrackerList)
        {
            var sendBack = new Dictionary<int, string[]>();
            var concurrencyDictionary = ConcurrencyDictionary(cTrackerList);
            foreach (var hour in DailyHours().Distinct()) // o is every hour starting with 0
            {
                //string[] weekdays = new string[7];
                string[] daysOfTheWeek = new string[7];
                foreach (var c in concurrencyDictionary)
                {
                    foreach (var d in c.Value)
                    {
                        if (d.Key == hour)
                        {
                            string count;
                            if (d.Value == 0)
                                count = "";
                            else
                                count = d.Value.ToString();
                            //string count = d.Value.ToString();

                            if (c.Key == DayOfWeek.Sunday)
                                daysOfTheWeek[0] = count;
                            if (c.Key == DayOfWeek.Monday)
                                daysOfTheWeek[1] = count;
                            if (c.Key == DayOfWeek.Tuesday)
                                daysOfTheWeek[2] = count;
                            if (c.Key == DayOfWeek.Wednesday)
                                daysOfTheWeek[3] = count;
                            if (c.Key == DayOfWeek.Thursday)
                                daysOfTheWeek[4] = count;
                            if (c.Key == DayOfWeek.Friday)
                                daysOfTheWeek[5] = count;
                            if (c.Key == DayOfWeek.Saturday)
                                daysOfTheWeek[6] = count;
                        }
                    }

                }
                sendBack.Add(hour, daysOfTheWeek);

            }
            return sendBack;
        }
        private List<int> DailyHours()
        {
            List<int> dailyHours = new();
            for (int i = 0; i < 24; i++)
            {
                dailyHours.Add(i);
            }
            return dailyHours;
        }
        private ConcurentTracker ParseConcurrency(CJobSessionInfo session, int days)
        {
            ConcurentTracker ct = new();

            DateTime now = DateTime.Now;
            double diff = (now - session.CreationTime).TotalDays;
            //if (session.CreationTime.Day == now.Day)
            //{

            //}
            if (diff < days)
            {
                DayOfWeek dayOfWeek = session.CreationTime.DayOfWeek;
                var startTime = session.CreationTime;

                TimeSpan.TryParse(session.JobDuration, out TimeSpan duration);
                DateTime endTime = startTime.AddMinutes(duration.Minutes);

                var startDay = session.CreationTime.Date;
                int startHour = startTime.Hour;
                int startMinute = startTime.Minute;
                int endHour = endTime.Hour;
                int endMinute = endTime.Minute;

                ct.Date = startTime.Date;
                ct.DayofTheWeeek = dayOfWeek;
                ct.Hour = startHour;
                ct.hourMinute = startHour * 60 + startMinute;
                ct.Minutes = startMinute;
                ct.Duration = duration;

                return ct;
            }
            return ct;
        }
    }
}
