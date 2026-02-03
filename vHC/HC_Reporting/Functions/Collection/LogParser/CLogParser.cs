// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CLogParser
    {
        private readonly CLogger log = CGlobals.Logger;
        private readonly string LogLocation;
        private Dictionary<string, List<TimeSpan>> waits = new();
        private readonly string pathToCsv = CVariables.vbrDir + @"\waits.csv";

        private readonly List<string> fixList = new();
        private readonly object fileLock = new object(); // Thread-safe file writing

        private readonly string logStart = "[LogParser] ";

        public CLogParser()
        {
            this.LogLocation = this.InitLogDir();
            this.InitWaitCsv();
            /*check each job for long wait times
             * need:
             * - job name
             * - how long it waited
             * -- last wait
             * -- times waited
             * -- avg wait?
             * - overlaps?
             * 
             */
        }

        public CLogParser(string path)
        {
        }

        public string InitLogDir()
        {
            this.log.Info(this.logStart + "Checking registry for default log location...");
            string logs = string.Empty;
            try
            {
                CRegReader reg = new();
                logs = reg.DefaultLogDir();
                this.log.Info(this.logStart + "Log Location: " + logs);
            }
            catch (Exception e)
            {
                this.log.Error(this.logStart + "Failed to return log location. Error:\t" + e.Message);
            }

            return logs;
        }

        private void InitWaitCsv()
        {
            this.log.Info(this.logStart + "Init waits.csv");
            try
            {
                using (StreamWriter sw = new StreamWriter(this.pathToCsv, append: false))
                {
                    sw.WriteLine("JobName,StartTime,EndTime,Duration");
                }
            }
            catch (Exception e)
            {
                this.log.Error(this.logStart + "Failed to init waits.csv. Error:\t" + e.Message);
            }
        }

        private void DumpWaitsToFile(string JobName, DateTime start, DateTime end, TimeSpan diff)
        {
            // Thread-safe file writing for parallel processing
            lock (this.fileLock)
            {
                using (StreamWriter sw = new StreamWriter(this.pathToCsv, append: true))
                {
                    sw.WriteLine(JobName + "," + start + "," + end + "," + diff);
                }
            }

            // String csv = String.Join(
            //    Environment.NewLine,
            //    _waits.Select(d => $"{d.Key};{d.Value};")
            //        );
            // System.IO.File.WriteAllText(pathToCsv, csv);
        }

        public Dictionary<string, List<TimeSpan>> GetWaitsFromFiles()
        {
            this.log.Info("Checking Log files for waits..", false);

            // OPTIMIZATION 1: Filter directories by last write time BEFORE scanning
            var allDirs = Directory.GetDirectories(this.LogLocation);
            var cutoffDate = DateTime.Now.AddDays(-CGlobals.ReportDays);
            var recentDirs = allDirs
                .Where(d => Directory.GetLastWriteTime(d) >= cutoffDate)
                .ToArray();

            this.log.Info(string.Format("[LogParser] Filtered to {0} of {1} directories within {2} days",
                recentDirs.Length, allDirs.Length, CGlobals.ReportDays), false);

            // Count files only in recent directories
            int jobFilesCount = 0;
            int taskFilesCount = 0;
            foreach (var dir in recentDirs)
            {
                jobFilesCount += Directory.GetFiles(dir, "Job*.log", SearchOption.AllDirectories).Length;
                taskFilesCount += Directory.GetFiles(dir, "Task*.log", SearchOption.AllDirectories).Length;
            }
            int logCount = jobFilesCount + taskFilesCount;

            this.log.Info(string.Format("[LogParser] Processing {0} Job logs + {1} Task logs = {2} total",
                jobFilesCount, taskFilesCount, logCount), false);

            // OPTIMIZATION 2: Parallel processing of directories
            ConcurrentDictionary<string, List<TimeSpan>> jobsAndWaits = new();
            int counter = 0;
            int fileCounter = 0;

            Parallel.ForEach(recentDirs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, d =>
            {
                int currentCount = Interlocked.Increment(ref counter);
                string info = string.Format("[LogParser] Parsing Directory {0} of {1}", currentCount, recentDirs.Length);
                this.log.Info(info, false);

                string jobname = Path.GetFileName(d);
                List<TimeSpan> waits = new();

                string[] jobList = Directory.GetFiles(d, "Job*.log", SearchOption.AllDirectories);
                string[] taskList = Directory.GetFiles(d, "Task*.log", SearchOption.AllDirectories);
                List<string> fileList = new();
                fileList.AddRange(jobList);
                fileList.AddRange(taskList);

                foreach (var f in fileList)
                {
                    int currentFileCount = Interlocked.Increment(ref fileCounter);
                    string fileInfoLog = string.Format("[LogParser] Parsing Log {0} of {1}", currentFileCount, logCount);
                    this.log.Info(fileInfoLog, false);
                    try
                    {
                        DateTime lastWriteTime = File.GetLastWriteTime(f);
                        DateTime currentTime = DateTime.Now;
                        TimeSpan diff = currentTime - lastWriteTime;
                        if (diff.Days <= CGlobals.ReportDays)
                        {
                            waits.AddRange(this.CheckFileWait(f, jobname));
                        }
                    }
                    catch (Exception) { }
                }

                jobsAndWaits.TryAdd(jobname, waits);
            });

            this.waits = jobsAndWaits.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            this.log.Info("Checking Log files for waits..Done!", false);
            return this.waits;
        }

        private void ParseFixLines(string line)
        {
            try
            {
                // log.Debug(line, false);
                string fixLine = line.Remove(0, line.IndexOf("Private Fix"));
                if (fixLine.EndsWith(']'))
                {
                    fixLine = fixLine.Replace("]", string.Empty);
                }


                if (!this.fixList.Contains(fixLine))
                {
                    this.fixList.Add(fixLine);

                    // log.Debug(fixLine, false);
                }
            }
            catch (Exception e)
            {
                if (CGlobals.DEBUG)
                {
                    this.log.Debug(e.Message);
                }
            }
        }

        private List<TimeSpan> CheckFileWait(string file, string jobName)
        {
            List<TimeSpan> diffListMin = new();

            using (StreamReader sr = new(file))
            {
                string line;
                string waitLine = "Waiting for backup infrastructure resources availability";
                string startTime = null;
                string endTime = null;

                bool countNextLine = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(waitLine))
                    {
                    }

                    if (!string.IsNullOrEmpty(line))
                    {
                        if (line.Contains("Private Fix"))
                        {
                            this.ParseFixLines(line);
                        }


                        try
                        {
                            if (countNextLine)
                            {
                                endTime = line.Remove(21);

                                countNextLine = false;
                            }

                            string trimline = string.Empty;
                            if (line.Length > 40)
                            {
                                trimline = trimline = line.Substring(40).Trim();
                            }


                            if (trimline == waitLine || trimline.Contains(waitLine))
                            {
                                startTime = line.Remove(21);
                                countNextLine = true;
                            }

                            if (!string.IsNullOrEmpty(startTime) && !string.IsNullOrEmpty(endTime))
                            {
                                diffListMin.Add(this.CalcTime(jobName, startTime, endTime));
                                endTime = string.Empty;
                                startTime = string.Empty;
                            }
                        }
                        catch (System.ArgumentOutOfRangeException) { }
                        catch (Exception) { }
                    }
                }
            }

            return diffListMin;
        }

        private TimeSpan CalcTime(string jobName, string start, string end)
        {
            start = start.Trim('[');
            start = start.Trim(']');
            start = start.Trim('.');
            end = end.Trim('[');
            end = end.Trim(']');
            end = end.Trim('.');

            // DateTime.TryParse(start, out DateTime tStart);
            // DateTime.TryParse(end, out DateTime tEnd);
            DateTime.TryParseExact(start, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime tStart);
            DateTime.TryParseExact(end, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime tEnd);

            var diffTime = tEnd - tStart;

            // string t = diffTime.ToString("dd:HH:mm:ss");
            this.DumpWaitsToFile(jobName, tStart, tEnd, diffTime);
            return diffTime;
        }
    }
}
