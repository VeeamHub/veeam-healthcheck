using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Html.VBR;
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

        public void ParseIndividualSessions(bool scrub)
        {
            this.scrubber = CGlobals.Scrubber;

            string folderName = "\\JobSessionReports";

            // Wipe the output folder before generating so files from previous
            // runs (e.g. for jobs that have since been renamed or deleted)
            // don't linger.
            this.CleanFolder(folderName);

            // mainDirBase/scrubDirBase are loop-invariant (same directory every group,
            // since CleanFolder only deletes *.html files, never the directory itself),
            // so create them once here instead of once per group inside SetMainDir/
            // SetScrubDir. A failure here now skips report generation entirely (logged),
            // rather than letting every group individually fail into a directory that
            // doesn't exist.
            string mainDirBase;
            string scrubDirBase;
            try
            {
                mainDirBase = BuildReportDir(CVariables.unsafeSuffix, folderName);
                scrubDirBase = BuildReportDir(CVariables.safeSuffix, folderName);
                CheckFolderExists(mainDirBase);
                CheckFolderExists(scrubDirBase);
            }
            catch (Exception e)
            {
                this.log.Error("Could not create job session report output directories: " + e.Message);
                return;
            }

            var allSessions = this.ReturnJobSessionsList();

            // Group sessions by the same rollup key the summary table uses, so
            // per-machine child sessions land in the same HTML file as their
            // parent. A second pass rolls up hierarchical ("<Parent>\<child>")
            // identities that carry no parent GUID at all. See ADR 0019, issue #219.
            var groups = CSessionGroupKey.Group(allSessions);

            double percentCounter = 0;
            int totalSessions = allSessions.Count;

            foreach (var group in groups)
            {
                var displayName = group.DisplayName;

                try
                {
                    var sessionsForJob = group.Sessions;

                    this.LogJobSessionParseProgress(percentCounter, totalSessions);

                    string mainDir = this.SetMainDir(mainDirBase, displayName);
                    string scrubDir = this.SetScrubDir(scrubDirBase, displayName);

                    File.WriteAllText(mainDir, this.BuildJobSessionReport(displayName, sessionsForJob, false));
                    File.WriteAllText(scrubDir, this.BuildJobSessionReport(displayName, sessionsForJob, true));

                    percentCounter += sessionsForJob.Count;
                }
                catch (Exception e)
                {
                    this.log.Error($"Exception generating individual session HTML for job '{displayName}':");
                    this.log.Error(e.Message);
                }
            }

            this.LogJobSessionParseProgress(100, 100);
        }

        /// <summary>
        /// Builds the complete HTML for one job's session report. When <paramref name="scrub"/>
        /// is set, the heading carries the same token as the file name, every row is scrubbed
        /// from its raw identity, and the whole document then goes through the scrubber's final
        /// pass (the one the main report runs) so no registered name survives in any column.
        /// </summary>
        internal string BuildJobSessionReport(string displayName, IEnumerable<CJobSessionInfo> sessions, bool scrub)
        {
            this.scrubber ??= CGlobals.Scrubber;

            string heading = scrub ? this.scrubber.ScrubItem(displayName, ScrubItemType.Job) : displayName;
            var sb = new System.Text.StringBuilder(this.ReturnTableHeaderString(heading));
            foreach (var cs in sessions)
            {
                try
                {
                    sb.Append(this.FormHtmlString(cs, scrub));
                }
                catch (Exception e)
                {
                    this.log.Error("Exception at individual job session parse:");
                    this.log.Error(e.Message);
                }
            }

            string html = sb.ToString();
            return scrub ? this.scrubber.FinalizeScrubbedText(html) : html;
        }

        internal IndividualJobSessionsHelper(CScrubHandler scrubber)
        {
            this.scrubber = scrubber;
        }

        private static string BuildReportDir(string suffix, string folderName)
        {
            return CCrossPlatformPath.Combine(CGlobals.desiredPath, suffix, folderName);
        }

        private void CleanFolder(string folderName)
        {
            try
            {
                var mainDir = BuildReportDir(CVariables.unsafeSuffix, folderName);
                if (Directory.Exists(mainDir))
                {
                    foreach (var f in Directory.GetFiles(mainDir, "*.html"))
                    {
                        try { File.Delete(f); }
                        catch (Exception e) { this.log.Warning("Could not delete stale session report file: " + f + " - " + e.Message); }
                    }
                }

                var scrubDir = BuildReportDir(CVariables.safeSuffix, folderName);
                if (Directory.Exists(scrubDir))
                {
                    foreach (var f in Directory.GetFiles(scrubDir, "*.html"))
                    {
                        try { File.Delete(f); }
                        catch (Exception e) { this.log.Warning("Could not delete stale session report file: " + f + " - " + e.Message); }
                    }
                }
            }
            catch (Exception e)
            {
                this.log.Warning("CleanFolder failed: " + e.Message);
            }
        }

        private string SetMainDir(string mainDirBase, string JobName)
        {
            return Path.Combine(mainDirBase, SanitizeFileName(JobName) + ".html");
        }

        private string SetScrubDir(string scrubDirBase, string JobName)
        {
            return Path.Combine(scrubDirBase, SanitizeFileName(this.scrubber.ScrubItem(JobName, ScrubItemType.Job)) + ".html");
        }

        internal static string SanitizeFileName(string name)
        {
            // ':' must go too: Path.Combine(base, second) discards `base` entirely
            // whenever `second` starts with a drive letter followed by ':'
            // (Windows' own rooted-path rule) — e.g. a job named "C: Nightly".
            return name.Replace("\\", "--").Replace("/", "-").Replace(":", "-");
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

        private string FormHtmlString(CJobSessionInfo c, bool scrub)
        {
            string s = string.Empty;
            string jname = c.JobName ?? string.Empty;
            string vmName = c.VmName;

            if (scrub)
            {
                // Scrub the raw identity. Rewriting "\" to "--" first registered a different
                // original than the rest of the report uses, so the final pass could not
                // recognise the real "<Parent>\<child>" name.
                jname = this.scrubber.ScrubItem(jname, ScrubItemType.Job);
                vmName = this.scrubber.ScrubItem(c.VmName, ScrubItemType.VM);
            }
            else
            {
                jname = jname.Replace("\\", "--");
            }

            s += "<tr>";
            s += TableData(jname, "jobName");
            s += TableData(vmName, "vmName");
            s += TableData(c.JobAlg, "alg");
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
            s += TableData(CHtmlTables.Badge(c.Status), "status");
            s += TableData(c.TaskDuration, "taskDuration");
            s += "</tr>";

            return s;
        }

        private string FixInvalidJobName(string jobName)
        {
            this.log.Debug("Caught invalid char in job name, replacing: " + jobName);
            var name = jobName.Replace("/", "-").Replace("\\", "--");
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
