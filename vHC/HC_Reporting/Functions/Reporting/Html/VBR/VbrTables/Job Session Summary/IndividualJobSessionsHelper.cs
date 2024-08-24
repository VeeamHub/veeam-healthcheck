﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary
{
    internal class IndividualJobSessionsHelper
    {
        private readonly CHtmlFormatting _form = new();
        private readonly CLogger log = CGlobals.Logger;
        private CScrubHandler _scrubber;
        private string logStart = "[DataFormer]\t";
        public IndividualJobSessionsHelper()
        {

        }
        private List<CJobSessionInfo> ReturnJobSessionsList()
        {
            var targetDate = CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);

            using (CDataTypesParser dt = new())
            {
                List<CJobSessionInfo> csv = new();
                var res = dt.JobSessions; //.Where(c => c.CreationTime >= targetDate).ToList();
                if (res == null)
                    return csv;
                else
                {
                    csv = res.Where(c => c.CreationTime >= targetDate).ToList();
                    //csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    csv = csv.OrderBy(x => x.Name).ToList();

                    csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return csv;
                }
            }
        }
        private List<CJobSessionInfo> ReturnJobSessionsList(string jobName)
        {
            var targetDate = CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);

            using (CDataTypesParser dt = new())
            {
                List<CJobSessionInfo> csv = new();
                var res = dt.JobSessions; //.Where(c => c.CreationTime >= targetDate).ToList();
                if (res == null)
                    return csv;
                else
                {
                    csv = res.Where(c => c.CreationTime >= targetDate).ToList();
                    //csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    csv = csv.Where(x => x.Name == jobName).ToList();

                    csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return csv;
                }
            }
        }
        private List<string> ReturnJobSessionsNamesList()
        {
            var targetDate = CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);

            using (CDataTypesParser dt = new())
            {
                List<string> csv = new();
                var res = dt.JobSessions; //.Where(c => c.CreationTime >= targetDate).ToList();
                if (res == null)
                    return null;
                else
                {
                    var p = res.Select(c => c.Name).ToList();
                    //csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    //csv = csv.Where(x => x.Name == jobName).ToList();

                    //csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return p.Distinct().ToList();
                }
            }
        }

        public void ParseIndividualSessions(bool scrub)
        {
            //if (scrub) { _scrubber = new(); }
            _scrubber = new();

            List<string> processedJobs = new();
            double percentCounter = 0;
            //var csv = ReturnJobSessionsList();

            var namesList = ReturnJobSessionsNamesList();
            var totalSessions = ReturnJobSessionsList().Count();


            foreach (var name in namesList)
            {
                var jName = name;
                var jobSessions = ReturnJobSessionsList(name);
                LogJobSessionParseProgress(percentCounter, totalSessions);

                // string outDir = "";// CVariables.desiredDir + "\\Original";
                string folderName = "\\JobSessionReports";

                string mainDir = SetMainDir(folderName, name);
                string scrubDir = SetScrubDir(folderName, name);

                if (name.Contains("/"))
                {
                    jName = FixInvalidJobName(name);
                }

                //                    string docName = outDir + "\\";


                string mainString = ReturnTableHeaderString(jName);
                File.WriteAllText(mainDir, mainString);

                string scrubString = ReturnTableHeaderString(jName);
                File.WriteAllText(scrubDir, scrubString);

                int counter = 1;

                //test
                foreach (var cs in jobSessions)
                {
                    string info = string.Format("Parsing {0} of {1} Job Sessions to HTML", counter, totalSessions);
                    counter++;
                    //log.Info(logStart + info, false);
                    try
                    {
                        int matches = 0;
                        if (name == cs.JobName)
                        {
                            matches++;
                            //mainString += FormHtmlString(cs, mainString, false);

                            File.AppendAllText(mainDir, FormHtmlString(cs, mainString, false));
                            File.AppendAllText(scrubDir, FormHtmlString(cs, scrubString, true));

                        }
                    }
                    catch (Exception e)
                    {
                        log.Error("Exception at individual job session parse:");
                        log.Error(e.Message);
                    }

                    percentCounter++;
                }
                //counter = 1;
                ////File.AppendAllText(mainDir, mainString);
                //mainString = null;


                //foreach (var cs in jobSessions)
                //{
                //    //string info = string.Format("Parsing {0} of {1} Job Sessions to HTML", counter, totalSessions);
                //    counter++;
                //    //log.Info(logStart + info, false);
                //    try
                //    {
                //        //int matches = 0;
                //        if (name == cs.JobName)
                //        {
                //           // matches++;
                //            //mainString += FormHtmlString(cs, mainString, false);

                //        }
                //    }
                //    catch (Exception e) { log.Error(e.Message); }

                //    percentCounter++;
                //}


                scrubString = null;


                // percentCounter++;

            }


            LogJobSessionParseProgress(100, 100);


        }
        private string SetMainDir(string folderName, CJobSessionInfo cs)
        {
            var mainDir = CGlobals._desiredPath + CVariables._unsafeSuffix + folderName;
            CheckFolderExists(mainDir);
            mainDir += "\\" + cs.JobName + ".html";
            return mainDir;
        }
        private string SetScrubDir(string folderName, CJobSessionInfo cs)
        {
            var scrubDir = CGlobals._desiredPath + CVariables._safeSuffix + folderName;
            //log.Warning("SAFE outdir = " + outDir, false);
            CheckFolderExists(scrubDir);
            scrubDir += "\\" + _scrubber.ScrubItem(cs.JobName) + ".html";
            return scrubDir;
        }
        private string SetMainDir(string folderName, string JobName)
        {
            var mainDir = CGlobals._desiredPath + CVariables._unsafeSuffix + folderName;
            CheckFolderExists(mainDir);
            mainDir += "\\" + JobName + ".html";
            return mainDir;
        }
        private string SetScrubDir(string folderName, string JobName)
        {
            var scrubDir = CGlobals._desiredPath + CVariables._safeSuffix + folderName;
            //log.Warning("SAFE outdir = " + outDir, false);
            CheckFolderExists(scrubDir);
            scrubDir += "\\" + _scrubber.ScrubItem(JobName) + ".html";
            return scrubDir;
        }
        private string ReturnTableHeaderString(CJobSessionInfo cs)
        {
            string s = _form.Header();
            s += "<h2>" + cs.JobName + "</h2>";

            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Job Name", "Name of job");
            s += _form.TableHeader("VM Name", "Name of VM/Server within the job");
            s += _form.TableHeader("Alg", "Job Algorithm");
            s += _form.TableHeader("Primary Bottleneck", "Primary detected bottleneck");
            s += _form.TableHeader("BottleNeck", "Detected bottleneck breakdown");
            s += _form.TableHeader("CompressionRatio", "Calculated compression ratio");
            s += _form.TableHeader("Start Time", "Start time of the backup job");
            s += _form.TableHeader("BackupSize", "Detected size of backup file");
            s += _form.TableHeader("DataSize", "Detected size of original VM/server (provisioned, not actual)");
            s += _form.TableHeader("DedupRatio", "Calculated deduplication ratio");
            s += _form.TableHeader("Is Retry", "Is this a retry run?");
            s += _form.TableHeader("Job Duration", "Duration of job in minutes");
            s += _form.TableHeader("Min Time", "Shorted detected job duration in minutes");
            s += _form.TableHeader("Max Time", "Longest detected job duration in minutes");
            s += _form.TableHeader("Avg Time", "Average job duration in minutes");
            s += _form.TableHeader("Processing Mode", "Processing mode used in the job (blank = SAN)");
            s += _form.TableHeader("Status", "Final status of the job");
            s += _form.TableHeader("Task Duration", "Duration of the VM/server within the job in minutes");
            s += "</tr>";
            return s;
        }
        private string ReturnTableHeaderString(string jobname)
        {
            string s = _form.Header();
            s += "<h2>" + jobname + "</h2>";

            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Job Name", "Name of job");
            s += _form.TableHeader("VM Name", "Name of VM/Server within the job");
            s += _form.TableHeader("Alg", "Job Algorithm");
            s += _form.TableHeader("Primary Bottleneck", "Primary detected bottleneck");
            s += _form.TableHeader("BottleNeck", "Detected bottleneck breakdown");
            s += _form.TableHeader("CompressionRatio", "Calculated compression ratio");
            s += _form.TableHeader("Start Time", "Start time of the backup job");
            s += _form.TableHeader("BackupSize", "Detected size of backup file");
            s += _form.TableHeader("DataSize", "Detected size of original VM/server (provisioned, not actual)");
            s += _form.TableHeader("DedupRatio", "Calculated deduplication ratio");
            s += _form.TableHeader("Is Retry", "Is this a retry run?");
            s += _form.TableHeader("Job Duration", "Duration of job in minutes");
            s += _form.TableHeader("Min Time", "Shorted detected job duration in minutes");
            s += _form.TableHeader("Max Time", "Longest detected job duration in minutes");
            s += _form.TableHeader("Avg Time", "Average job duration in minutes");
            s += _form.TableHeader("Processing Mode", "Processing mode used in the job (blank = SAN)");
            s += _form.TableHeader("Status", "Final status of the job");
            s += _form.TableHeader("Task Duration", "Duration of the VM/server within the job in minutes");
            s += "</tr>";
            return s;
        }
        private string FormHtmlString(CJobSessionInfo c, string htmlString, bool scrub)
        {
            string s = ""; // htmlString;
            string jname = c.JobName;
            if (jname.Contains("\\"))
            {
                jname = jname.Replace("\\", "--");
            }
            string vmName = c.VmName;
            //string repo = _scrubber.ScrubItem(c.)
            if (scrub)
            {
                jname = _scrubber.ScrubItem(jname, "job");
                vmName = _scrubber.ScrubItem(c.VmName, "vm");
            }

            s += "<tr>";
            s += TableData(jname, "jobName");
            s += TableData(vmName, "vmName");
            s += TableData(c.Alg, "alg");
            s += TableData(c.PrimaryBottleneck, "primBottleneck");
            s += TableData(c.Bottleneck, "bottleneck");
            s += TableData(c.CompressionRatio, "compression");
            s += TableData(c.CreationTime.ToString(), "creationtime");
            s += TableData(c.BackupSize.ToString(), "backupsize");
            s += TableData(c.DataSize.ToString(), "datasize");
            s += TableData(c.DedupRatio, "dedupratio");
            s += TableData(c.IsRetry, "isretry");
            s += TableData(c.JobDuration, "jobDuration");
            s += TableData(c.minTime.ToString(), "minTime");
            s += TableData(c.maxTime.ToString(), "maxtime");
            s += TableData(c.avgTime.ToString(), "avgTime");
            s += TableData(c.ProcessingMode, "processingmode");
            s += TableData(c.Status, "status");
            s += TableData(c.TaskDuration, "taskDuration");
            s += "</tr>";

            return s;
        }
        private string FixInvalidJobName(string jobName)
        {
            log.Debug("Caught invalid char: \"/\", replacing with \"-\": " + jobName);
            var name = jobName.Replace("/", "-");
            log.Debug("New Name = " + name);
            return name;
        }
        private void LogJobSessionParseProgress(double counter, int total)
        {
            double percentComplete = counter / total * 100;
            string msg = string.Format(logStart + "{0}%...", Math.Round(percentComplete, 2));
            log.Info(msg, false);
        }
        private string TableData(string data, string toolTip)
        {
            return string.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
        }
        private void CheckFolderExists(string folder)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }
}
