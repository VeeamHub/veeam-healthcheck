using System;
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
        private readonly CHtmlFormatting form = new();
        private readonly CLogger log = CGlobals.Logger;
        private CScrubHandler scrubber;
        private readonly string logStart = "[DataFormer]\t";

        public IndividualJobSessionsHelper()
        {
        }

        private List<CJobSessionInfo> ReturnJobSessionsList()
        {
            var targetDate = CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);

            List<CJobSessionInfo> csv = new();
            var res = CGlobals.DtParser.JobSessions; // .Where(c => c.CreationTime >= targetDate).ToList();
            if (res == null)
                    return csv;
                else
                {
                    csv = res.Where(c => c.CreationTime >= targetDate).ToList();

                    // csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    csv = csv.OrderBy(x => x.Name).ToList();

                    csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return csv;
                }
        }

        private List<CJobSessionInfo> ReturnJobSessionsList(string jobName)
        {
            var targetDate = CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);

            List<CJobSessionInfo> csv = new();
            var res = CGlobals.DtParser.JobSessions; // .Where(c => c.CreationTime >= targetDate).ToList();
            if (res == null)
                    return csv;
                else
                {
                    csv = res.Where(c => c.CreationTime >= targetDate).ToList();

                    // csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    csv = csv.Where(x => x.Name == jobName).ToList();

                    csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return csv;
                }
        }

        private List<string> ReturnJobSessionsNamesList()
        {
            var targetDate = CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays);

            List<string> csv = new();
            var res = CGlobals.DtParser.JobSessions; // .Where(c => c.CreationTime >= targetDate).ToList();
            if (res == null)
                    return null;
                else
                {
                    var p = res.Select(c => c.Name).ToList();

                    // csv = csv.Where(c => c.CreationTime >= targetDate).ToList();
                    // csv = csv.Where(x => x.Name == jobName).ToList();

                    // csv = csv.OrderBy(y => y.CreationTime).ToList();
                    csv.Reverse();
                    return p.Distinct().ToList();
                }
        }

        public void ParseIndividualSessions(bool scrub)
        {
            // if (scrub) { _scrubber = new(); }
            this.scrubber = CGlobals.Scrubber;

            List<string> processedJobs = new();
            double percentCounter = 0;

            // var csv = ReturnJobSessionsList();
            var namesList = this.ReturnJobSessionsNamesList();
            var totalSessions = this.ReturnJobSessionsList().Count();

            foreach (var name in namesList)
            {
                var jName = name;
                var jobSessions = this.ReturnJobSessionsList(name);
                this.LogJobSessionParseProgress(percentCounter, totalSessions);

                // string outDir = "";// CVariables.desiredDir + "\\Original";
                string folderName = "\\JobSessionReports";

                string mainDir = this.SetMainDir(folderName, name);
                string scrubDir = this.SetScrubDir(folderName, name);

                if (name.Contains("/"))
                {
                    jName = this.FixInvalidJobName(name);
                }

                // string docName = outDir + "\\";

                string mainString = this.ReturnTableHeaderString(jName);
                File.WriteAllText(mainDir, mainString);

                string scrubString = this.ReturnTableHeaderString(jName);
                File.WriteAllText(scrubDir, scrubString);

                int counter = 1;

                // test
                foreach (var cs in jobSessions)
                {
                    string info = string.Format("Parsing {0} of {1} Job Sessions to HTML", counter, totalSessions);
                    counter++;

                    // log.Info(logStart + info, false);
                    try
                    {
                        int matches = 0;
                        if (name == cs.JobName)
                        {
                            matches++;

                            // mainString += FormHtmlString(cs, mainString, false);
                            File.AppendAllText(mainDir, this.FormHtmlString(cs, mainString, false));
                            File.AppendAllText(scrubDir, this.FormHtmlString(cs, scrubString, true));
                        }
                    }
                    catch (Exception e)
                    {
                        this.log.Error("Exception at individual job session parse:");
                        this.log.Error(e.Message);
                    }

                    percentCounter++;
                }

                // counter = 1;
                ////File.AppendAllText(mainDir, mainString);
                // mainString = null;

                // foreach (var cs in jobSessions)
                // {
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

                // }
                //    }
                //    catch (Exception e) { log.Error(e.Message); }

                // percentCounter++;
                // }

                scrubString = null;

                // percentCounter++;
            }

            this.LogJobSessionParseProgress(100, 100);
        }

        private string SetMainDir(string folderName, CJobSessionInfo cs)
        {
            var mainDir = CGlobals.desiredPath + CVariables.unsafeSuffix + folderName;
            CheckFolderExists(mainDir);
            mainDir += "\\" + cs.JobName + ".html";
            return mainDir;
        }

        private string SetScrubDir(string folderName, CJobSessionInfo cs)
        {
            var scrubDir = CGlobals.desiredPath + CVariables.safeSuffix + folderName;

            // log.Warning("SAFE outdir = " + outDir, false);
            CheckFolderExists(scrubDir);
            scrubDir += "\\" + this.scrubber.ScrubItem(cs.JobName, ScrubItemType.Job) + ".html";
            return scrubDir;
        }

        private string SetMainDir(string folderName, string JobName)
        {
            var mainDir = CGlobals.desiredPath + CVariables.unsafeSuffix + folderName;
            CheckFolderExists(mainDir);
            mainDir += "\\" + JobName + ".html";
            return mainDir;
        }

        private string SetScrubDir(string folderName, string JobName)
        {
            var scrubDir = CGlobals.desiredPath + CVariables.safeSuffix + folderName;

            // log.Warning("SAFE outdir = " + outDir, false);
            CheckFolderExists(scrubDir);
            scrubDir += "\\" + this.scrubber.ScrubItem(JobName, ScrubItemType.Job) + ".html";
            return scrubDir;
        }

        private string ReturnTableHeaderString(string jobname)
        {
            string s = this.form.Header();
            s += "<h2>" + jobname + "</h2>";

            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Job Name", "Name of job");
            s += this.form.TableHeader("VM Name", "Name of VM/Server within the job");
            s += this.form.TableHeader("Alg", "Job Algorithm");
            s += this.form.TableHeader("Primary Bottleneck", "Primary detected bottleneck");
            s += this.form.TableHeader("BottleNeck", "Detected bottleneck breakdown");
            s += this.form.TableHeader("CompressionRatio", "Calculated compression ratio");
            s += this.form.TableHeader("Start Time", "Start time of the backup job");
            s += this.form.TableHeader("BackupSizeGB", "Detected size of backup file");
            s += this.form.TableHeader("DataSizeGB", "Detected size of original VM/server (provisioned, not actual)");
            s += this.form.TableHeader("DedupRatio", "Calculated deduplication ratio");
            s += this.form.TableHeader("Is Retry", "Is this a retry run?");
            s += this.form.TableHeader("Job Duration", "Duration of job in minutes");
            s += this.form.TableHeader("Min Time", "Shorted detected job duration in minutes");
            s += this.form.TableHeader("Max Time", "Longest detected job duration in minutes");
            s += this.form.TableHeader("Avg Time", "Average job duration in minutes");
            s += this.form.TableHeader("Processing Mode", "Processing mode used in the job (blank = SAN)");
            s += this.form.TableHeader("Status", "Final status of the job");
            s += this.form.TableHeader("Task Duration", "Duration of the VM/server within the job in minutes");
            s += "</tr>";
            return s;
        }

        private string FormHtmlString(CJobSessionInfo c, string htmlString, bool scrub)
        {
            string s = string.Empty; // htmlString;
            string jname = c.JobName;
            if (jname.Contains("\\"))
            {
                jname = jname.Replace("\\", "--");
            }

            string vmName = c.VmName;

            // string repo = _scrubber.ScrubItem(c.)
            if (scrub)
            {
                jname = this.scrubber.ScrubItem(jname, ScrubItemType.Job);
                vmName = this.scrubber.ScrubItem(c.VmName, ScrubItemType.VM);
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
            this.log.Debug("Caught invalid char: \"/\", replacing with \"-\": " + jobName);
            var name = jobName.Replace("/", "-");
            this.log.Debug("New Name = " + name);
            return name;
        }

        private void LogJobSessionParseProgress(double counter, int total)
        {
            double percentComplete = counter / total * 100;
            string msg = string.Format(this.logStart + "{0}%...", Math.Round(percentComplete, 2));
            this.log.Info(msg, false);
        }

        private static string TableData(string data, string toolTip)
        {
            return string.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
        }

        private static void CheckFolderExists(string folder)
        {
            if (!Directory.Exists(folder))
            {

                Directory.CreateDirectory(folder);
            }
        }
    }
}
