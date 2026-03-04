using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info
{
    internal class CJobInfoTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        // Job types that support Application Aware Image Processing (guest OS quiescence)
        private static readonly HashSet<string> AaipSupportedJobTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Backup",           // VM backups (VMware/Hyper-V)
            "EpAgentBackup",    // Physical/Endpoint backups
            "Replica"           // VM replication
        };

        private static bool JobTypeSupportsAaip(string jobType) => AaipSupportedJobTypes.Contains(jobType);

        public CJobInfoTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("jobs", VbrLocalizationHelper.JobInfoTitle, VbrLocalizationHelper.JobInfoBtn);
            s += "</table>";

            string summary = this.sum.JobInfo();

            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                source.OrderBy(x => x.Name);
                var jobTypes = source.Select(x => x.JobType).Distinct().ToList();

                try
                {
                    foreach (var jType in jobTypes)
                    {
                        double tSizeGB = 0;
                        double onDiskTotalGB = 0;

                        bool useSourceSize = !(jType == "NasBackupCopy" || jType == "Copy");

                        var realType = CJobTypesParser.GetJobType(jType);
                        string jobTable = this.form.SectionStartWithButton("jobTable", realType + " Jobs", string.Empty);
                        s += jobTable;
                        s += this.SetGenericJobTablHeader(useSourceSize, jType);
                        var res = source.Where(x => x.JobType == jType).ToList();
                        foreach (var job in res)
                        {
                            double onDiskGB = 0;
                            double sourceSizeGB = 0;

                            if (job.JobType != jType)
                            {
                                continue;
                            }

                            string row = string.Empty;
                            if (jType == "NasBackup")
                            {
                                var x = csvparser.GetDynamicNasBackup().ToList();

                                var diskGb = x.Where(x => x.name == job.Name)
                                    .Select(x => x.ondiskgb)
                                    .FirstOrDefault();
                                double.TryParse(diskGb, out onDiskGB);
                                onDiskGB = Math.Round(onDiskGB, 2);
                                onDiskTotalGB += onDiskGB;

                                var sourceGb = x.Where(x => x.name == job.Name)
                                    .Select(x => x.sourcegb)
                                    .FirstOrDefault();
                                double.TryParse(sourceGb, out sourceSizeGB);
                                sourceSizeGB = Math.Round(sourceSizeGB, 2);
                            }
                            else
                            {
                                onDiskGB = Math.Round(job.OnDiskGB ?? 0, 2);
                            }

                            string jobName = job.Name;
                            string repoName = job.RepoName;
                            if (scrub)
                            {
                                jobName = CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Job);
                                repoName = CGlobals.Scrubber.ScrubItem(repoName, ScrubItemType.Repository);
                            }

                            row += "<tr>";
                            row += this.form.TableDataLeftAligned(jobName, string.Empty);
                            row += this.form.TableData(repoName, string.Empty);

                            if (useSourceSize)
                            {
                                if (jType == "NasBackup")
                                {
                                    row += this.form.TableData(sourceSizeGB.ToString(), string.Empty);
                                    tSizeGB += sourceSizeGB;
                                }
                                else
                                {
                                    double trueSizeGB = Math.Round(job.OriginalSize / 1024 / 1024 / 1024, 2);
                                    double trueSizeTB = Math.Round(job.OriginalSize / 1024 / 1024 / 1024 / 1024, 2);
                                    double trueSizeMB = Math.Round(job.OriginalSize / 1024 / 1024, 2);
                                    tSizeGB += trueSizeGB;
                                    if (trueSizeGB > 999)
                                    {
                                        row += this.form.TableData(trueSizeTB.ToString() + " TB", string.Empty);
                                    }
                                    else if (trueSizeGB < 1)
                                    {
                                        row += this.form.TableData(trueSizeMB.ToString() + " MB", string.Empty);
                                    }
                                    else
                                    {
                                        row += this.form.TableData(trueSizeGB.ToString() + " GB", string.Empty);
                                    }
                                }
                            }
                            else
                            {
                            }

                            row += this.form.TableData(onDiskGB.ToString(), string.Empty);

                            row += job.RetentionType == "Cycles" ? this.form.TableData("Points", string.Empty) : this.form.TableData(job.RetentionType, string.Empty);
                            string retentionValue = job.RetentionType == "Cycles"
                                ? job.RetentionCount
                                : job.RetainDaysToKeep;
                            row += this.form.TableData(retentionValue, string.Empty);

                            row += job.StgEncryptionEnabled == "True" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                            var jobType = CJobTypesParser.GetJobType(job.JobType);
                            row += this.form.TableData(jobType, string.Empty);

                            string compressionLevel = string.Empty;
                            if (job.CompressionLevel == "9")
                            {
                                compressionLevel = "Extreme";
                            }
                            else if (job.CompressionLevel == "6")
                            {
                                compressionLevel = "High";
                            }
                            else if (job.CompressionLevel == "5")
                            {
                                compressionLevel = "Optimal";
                            }
                            else if (job.CompressionLevel == "4")
                            {
                                compressionLevel = "Dedupe-Friendly";
                            }
                            else if (job.CompressionLevel == "0")
                            {
                                compressionLevel = "None";
                            }

                            row += this.form.TableData(compressionLevel, string.Empty);

                            string blockSize = string.Empty;
                            if (job.BlockSize == "KbBlockSize1024")
                            {
                                blockSize = "1 MB";
                            }
                            else if (job.BlockSize == "KbBlockSize512")
                            {
                                blockSize = "512 KB";
                            }
                            else if (job.BlockSize == "KbBlockSize256")
                            {
                                blockSize = "256 KB";
                            }
                            else if (job.BlockSize == "KbBlockSize4096")
                            {
                                blockSize = "4 MB";
                            }
                            else if (job.BlockSize == "KbBlockSize8192")
                            {
                                blockSize = "8 MB";
                            }

                            row += this.form.TableData(blockSize, string.Empty);
                            if (job.GfsMonthlyEnabled || job.GfsWeeklyIsEnabled || job.GfsYearlyEnabled)
                            {
                                row += this.form.TableData(this.form.True, string.Empty);
                                var gfsParts = new List<string>();
                                if (job.GfsWeeklyIsEnabled)
                                    gfsParts.Add("Weekly: " + job.GfsWeeklyCount);
                                if (job.GfsMonthlyEnabled)
                                    gfsParts.Add("Monthly: " + job.GfsMonthlyCount);
                                if (job.GfsYearlyEnabled)
                                    gfsParts.Add("Yearly: " + job.GfsYearlyCount);
                                string GfsString = string.Join("<br> ", gfsParts);
                                row += this.form.TableData(GfsString, string.Empty);
                            }
                            else
                            {
                                row += this.form.TableData(this.form.False, string.Empty);
                                row += this.form.TableData(string.Empty, string.Empty);
                            }

                            row += job.EnableFullBackup ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                            try
                            {
                                if (job.Algorithm == "Increment" && job.TransformFullToSyntethic == true)
                                {
                                    row += this.form.TableData(this.form.True, string.Empty);
                                }
                                else
                                {
                                    row += this.form.TableData(this.form.False, string.Empty);
                                }
                            }
                            catch
                            {
                                row += this.form.TableData(this.form.False, string.Empty);
                            }

                            row += job.Algorithm == "Syntethic" ? this.form.TableData("Reverse Incremental", string.Empty) : this.form.TableData("Forward Incremental", string.Empty);

                            row += job.IndexingType != "None" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);

                            // AAIP/Guest Processing columns - only for job types that support it
                            if (JobTypeSupportsAaip(jType))
                            {
                                row += job.AAIPEnabled == "True" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                                row += job.VSSIgnoreErrors == "True" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                                row += job.GuestFSIndexingEnabled == "True" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                            }

                            row += "</tr>";

                            s += row;
                        }

                        // table summary/totals
                        if (useSourceSize)
                        {
                            s += "<tr>";
                            s += this.form.TableDataLeftAligned("Totals", string.Empty);
                            s += this.form.TableData(string.Empty, string.Empty);

                            double totalSizeTB = Math.Round(tSizeGB / 1024, 2);
                            double totalSizeMB = Math.Round(tSizeGB * 1024, 2);

                            double diskTotalTB = Math.Round(onDiskTotalGB / 1024, 2);
                            double diskTotalMB = Math.Round(onDiskTotalGB * 1024, 2);
                            if (tSizeGB > 999)
                            {
                                s += this.form.TableData(totalSizeTB.ToString() + " TB", string.Empty);
                            }
                            else if (tSizeGB < 1)
                            {
                                s += this.form.TableData(totalSizeMB.ToString() + " MB", string.Empty);
                            }
                            else
                            {
                                s += this.form.TableData(tSizeGB.ToString() + " GB", string.Empty);
                            }

                            if (diskTotalTB > 1)
                            {
                                s += this.form.TableData(onDiskTotalGB.ToString(), "TB");
                            }
                            else if (onDiskTotalGB > 1)
                            {
                                s += this.form.TableData(onDiskTotalGB.ToString(), "GB");
                            }
                            else
                            {
                                s += this.form.TableData(onDiskTotalGB.ToString(), "MB");
                            }
                        }

                        // end each table/section
                        s += this.form.SectionEnd(summary);
                    }

                    // add tape table
                    try
                    {
                        CTapeJobInfoTable tapeTable = new();
                        string tt = tapeTable.TapeJobTable();
                        if (tt != string.Empty)
                        {
                            string tableButton = this.form.SectionStartWithButton("jobTable", "Tape Jobs", string.Empty);
                            s += tableButton;
                            s += tt;
                        }
                    }
                    catch (Exception e)
                    {
                        this.log.Error("Tape Job Data import failed. ERROR:");
                        this.log.Error("\t" + e.Message);
                    }

                    // Add Entra Table
                    try
                    {
                        CEntraJobsTable entraTable = new();
                        string et = entraTable.Table();
                        if (et != null && et.Length > 0)
                        {
                            this.log.Debug("Entra jobs table length: " + et.Length);
                            string tableButton = this.form.SectionStartWithButton("jobTable", "Entra Jobs", string.Empty);
                            s += tableButton;
                            s += et;
                            s += this.form.SectionEnd(summary);
                        }
                        else
                        {
                            this.log.Info("No Entra backup jobs detected - skipping Entra jobs table", false);
                        }
                    }
                    catch (Exception e)
                    {
                        this.log.Error("Entra Job Data import failed. ERROR:");
                        this.log.Error("\t" + e.Message);
                    }

                    s += this.form.SectionEnd(summary);
                }
                catch (Exception e)
                {
                    this.log.Error("Job Info Data import failed. ERROR:");
                    this.log.Error("\t" + e.Message);
                }
            }
            catch (Exception e)
            {
                this.log.Error("Jobs Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON job info capture
            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                List<string> headers = new() { "JobName", "RepoName", "SourceSizeGB", "OnDiskGB", "RetentionScheme", "RetainDays", "Encrypted", "JobType", "CompressionLevel", "BlockSize", "GfsEnabled", "GfsDetails", "ActiveFullEnabled", "SyntheticFullEnabled", "BackupChainType", "IndexingEnabled", "AAIPEnabled", "VSSEnabled", "VSSIgnoreErrors", "GuestFSIndexing" };
                List<List<string>> rows = new();

                foreach (var job in source)
                {
                    string jobName = scrub ? CGlobals.Scrubber.ScrubItem(job.Name, ScrubItemType.Job) : job.Name;
                    string repoName = scrub ? CGlobals.Scrubber.ScrubItem(job.RepoName, ScrubItemType.Repository) : job.RepoName;

                    double sourceSizeGB = Math.Round(job.OriginalSize / 1024.0 / 1024.0 / 1024.0, 2);
                    string compressionLevel = job.CompressionLevel switch
                    {
                        "9" => "Extreme",
                        "6" => "High",
                        "5" => "Optimal",
                        "4" => "Dedupe-Friendly",
                        "0" => "None",
                        _ => job.CompressionLevel,
                    };

                    string blockSize = job.BlockSize switch
                    {
                        "KbBlockSize1024" => "1 MB",
                        "KbBlockSize512" => "512 KB",
                        "KbBlockSize256" => "256 KB",
                        "KbBlockSize4096" => "4 MB",
                        "KbBlockSize8192" => "8 MB",
                        _ => job.BlockSize,
                    };

                    bool gfsEnabled = job.GfsMonthlyEnabled || job.GfsWeeklyIsEnabled || job.GfsYearlyEnabled;
                    var gfsDetailParts = new List<string>();
                    if (job.GfsWeeklyIsEnabled)
                        gfsDetailParts.Add($"Weekly:{job.GfsWeeklyCount}");
                    if (job.GfsMonthlyEnabled)
                        gfsDetailParts.Add($"Monthly:{job.GfsMonthlyCount}");
                    if (job.GfsYearlyEnabled)
                        gfsDetailParts.Add($"Yearly:{job.GfsYearlyCount}");
                    string gfsDetails = gfsEnabled ? string.Join(",", gfsDetailParts) : string.Empty;
                    bool syntheticFull = job.Algorithm == "Increment" && job.TransformFullToSyntethic;
                    string backupChainType = job.Algorithm == "Syntethic" ? "Reverse Incremental" : "Forward Incremental";
                    bool indexingEnabled = job.IndexingType != "None";

                    this.log.Debug($"Job: {jobName}, OnDiskGB: {job.OnDiskGB}");

                    string retentionValue = job.RetentionType == "Cycles" ? job.RetentionCount : job.RetainDaysToKeep;
                    rows.Add(new List<string>
                    {
                        jobName,
                        repoName,
                        sourceSizeGB.ToString(),
                        (job.OnDiskGB ?? 0).ToString(),
                        job.RetentionType == "Cycles" ? "Points" : job.RetentionType,
                        retentionValue,
                        job.StgEncryptionEnabled,
                        CJobTypesParser.GetJobType(job.JobType),
                        compressionLevel,
                        blockSize,
                        gfsEnabled.ToString(),
                        gfsDetails,
                        job.EnableFullBackup.ToString(),
                        syntheticFull.ToString(),
                        backupChainType,
                        indexingEnabled.ToString(),
                        job.AAIPEnabled?.ToString() ?? "",
                        job.VSSEnabled?.ToString() ?? "",
                        job.VSSIgnoreErrors?.ToString() ?? "",
                        job.GuestFSIndexingEnabled?.ToString() ?? "",
                    });
                }

                CHtmlTables.SetSectionPublic("jobInfo", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobInfo JSON section: " + ex.Message);
            }

            return s;
        }

        private string SetGenericJobTablHeader(bool useSourceSize, string jobType = null)
        {
            bool showAaipColumns = jobType != null && JobTypeSupportsAaip(jobType);

            string s = string.Empty;
            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.JobInfo0, VbrLocalizationHelper.JobInfo0TT);
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo1, VbrLocalizationHelper.JobInfo1TT);
            if (useSourceSize)
            {
                s += this.form.TableHeader(VbrLocalizationHelper.JobInfo2, VbrLocalizationHelper.JobInfo2TT);
            }

            s += this.form.TableHeader("Est. On Disk GB", "Estimated size of the backup data on-disk.");
            s += this.form.TableHeader("Retention Scheme", "Is the job set to keep backups for X number of Days or Points");
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo3, VbrLocalizationHelper.JobInfo3TT);
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo4, VbrLocalizationHelper.JobInfo4TT);
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo5, VbrLocalizationHelper.JobInfo5TT);

            s += this.form.TableHeader("Compression Level", "Level of compression used in the job");
            s += this.form.TableHeader("Block Size", "Block Size set for the job");
            s += this.form.TableHeader("GFS Enabled", "True if any GFS Periods are enabled");
            s += this.form.TableHeader("GFS Retention", "Details about the GFS Retention period");
            s += this.form.TableHeader("Active Full Enabled", string.Empty);
            s += this.form.TableHeader("Synthetic Full Enabled", string.Empty);
            s += this.form.TableHeader("Backup Chain Type", "Type of backup chain used in the job");
            s += this.form.TableHeader("Indexing Enabled", string.Empty);

            if (showAaipColumns)
            {
                s += this.form.TableHeader("AAIP Enabled", "Enable application-aware processing checkbox in job settings");
                s += this.form.TableHeader("VSS Ignore Errors", "Continue backup if VSS errors occur");
                s += this.form.TableHeader("Guest FS Indexing", "Guest file system indexing enabled for file-level restore");
            }

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            return s;
        }
    }
}
