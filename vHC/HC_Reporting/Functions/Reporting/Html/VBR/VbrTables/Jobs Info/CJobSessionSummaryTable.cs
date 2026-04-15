using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info
{
    internal class CJobSessionSummaryTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CJobSessionSummaryTable() { }

        public string RenderFlat(bool scrub)
        {
            this.log.Info("Adding Job Session Summary Table");
            string s = this.form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            string summary = this.sum.JobSessSummary();

            s += this.SetJobSessionsHeaders();
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);

                foreach (var stu in stuff)
                {
                    try
                    {
                        string t = string.Empty;
                        t += "<tr>";

                        t += this.form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                        t += this.form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                        t += this.form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                        t += this.form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                        t += this.form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                        t += this.form.TableData(stu.SessionCount.ToString(), VbrLocalizationHelper.Jss5);
                        t += this.form.TableData(stu.Fails.ToString(), "Fails");
                        t += this.form.TableData(stu.Retries.ToString(), "Retries");
                        t += this.form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                        t += this.form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                        t += this.form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                        t += this.form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                        t += this.form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                        t += this.form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                        t += this.form.TableData(stu.WaitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += this.form.TableData(stu.MaxWait, VbrLocalizationHelper.Jss13);
                        t += this.form.TableData(stu.AvgWait, VbrLocalizationHelper.Jss14);
                        if (CGlobals.DEBUG)
                        {
                            this.log.Debug("Job Name = " + stu.JobName);
                            this.log.Debug("Job Type = " + stu.JobType);
                        }

                        string jobType = CJobTypesParser.GetJobType(stu.JobType);
                        t += this.form.TableData(jobType, VbrLocalizationHelper.Jss15);

                        t += "</tr>";

                        s += t;
                    }
                    catch (Exception ex)
                    {
                        this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        this.log.Error("\t" + ex.Message);
                    }
                }
            }
            catch (Exception e)
            {
                this.log.Error("Job Session Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);
            this.log.Info("Job Session Summary Table added");

            // JSON job session summary
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                List<string> headers = new() { "JobName", "ItemCount", "MinJobTime", "MaxJobTime", "AvgJobTime", "SessionCount", "Fails", "Retries", "SuccessRate", "AvgBackupSize", "MaxBackupSize", "AvgDataSize", "MaxDataSize", "AvgChangeRate", "WaitCount", "MaxWait", "AvgWait", "JobTypes" };
                List<List<string>> rows = stuff.Select(stu => new List<string>
                {
                    stu.JobName,
                    stu.ItemCount.ToString(),
                    stu.MinJobTime,
                    stu.MaxJobTime,
                    stu.AvgJobTime,
                    stu.SessionCount.ToString(),
                    stu.Fails.ToString(),
                    stu.Retries.ToString(),
                    stu.SuccessRate.ToString(),
                    stu.AvgBackupSize.ToString(),
                    stu.MaxBackupSize.ToString(),
                    stu.AvgDataSize.ToString(),
                    stu.MaxDataSize.ToString(),
                    stu.AvgChangeRate.ToString(),
                    stu.WaitCount.ToString(),
                    stu.MaxWait,
                    stu.AvgWait,
                    CJobTypesParser.GetJobType(stu.JobType),
                }).ToList();
                CHtmlTables.SetSectionPublic("jobSessionSummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobSessionSummary JSON section: " + ex.Message);
            }

            return s;
        }

        public string RenderByJob(bool scrub)
        {
            string s = this.form.SectionStartWithButtonNoTable("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);

            string summary = this.sum.JobInfo();

            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                source.OrderBy(x => x.Name);
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var jobTypes = stuff.Select(x => x.JobType).Distinct().ToList();

                List<CJobSummaryTypes> OffloadJobs = new();

                try
                {
                    foreach (var jType in jobTypes)
                    {
                        if (CGlobals.DEBUG)
                        {
                            this.log.Debug("Job Type = " + jType);
                        }

                        bool skipTotals = false;
                        var jobType = jType;

                        if (jType == string.Empty || jType == null)
                        {
                        }

                        var realType = CJobTypesParser.GetJobType(jobType);

                        string sectionHeader = realType;
                        if (jType == null)
                        {
                            sectionHeader = "Summary of All";
                        }

                        string jobTable = this.form.SectionStartWithButton("jobTable-" + realType.ToLower().Replace(" ", "-"), sectionHeader + " Jobs", string.Empty);
                        s += jobTable;
                        s += this.SetJobSessionsHeaders();
                        var res = stuff.Where(x => x.JobType == jobType).ToList();

                        int totalItemsCount = 0;
                        double totalSessionCount = 0;
                        int totalFails = 0;
                        int totalRetries = 0;
                        double totalSuccessRate = 0;
                        double totalAvgBackupSize = 0;
                        double totalMaxBackupSize = 0;
                        double totalAvgDataSize = 0;
                        double totalMaxDataSize = 0;
                        double totalAvgChangeRate = 0;
                        int totalWaitCount = 0;

                        foreach (var stu in res)
                        {
                            if (stu.JobName.Contains("Offload"))
                            {
                                OffloadJobs.Add(stu);
                                continue;
                            }

                            if (stu.JobName == "Total")
                            {
                                skipTotals = true;
                            }

                            if (stu.JobType != jobType && stu.JobName != "Totals")
                            {
                                continue;
                            }

                            try
                            {
                                string t = string.Empty;
                                t += "<tr>";

                                t += this.form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                                t += this.form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                                t += this.form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                                t += this.form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                                t += this.form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                                t += this.form.TableData(stu.SessionCount.ToString(), VbrLocalizationHelper.Jss5);
                                t += this.form.TableData(stu.Fails.ToString(), "Fails");
                                t += this.form.TableData(stu.Retries.ToString(), "Retries");
                                t += this.form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                                t += this.form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                                t += this.form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                                t += this.form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                                t += this.form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                                t += this.form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                                t += this.form.TableData(stu.WaitCount.ToString(), VbrLocalizationHelper.Jss12);
                                t += this.form.TableData(stu.MaxWait, VbrLocalizationHelper.Jss13);
                                t += this.form.TableData(stu.AvgWait, VbrLocalizationHelper.Jss14);
                                string jt = CJobTypesParser.GetJobType(stu.JobType);
                                t += this.form.TableData(jt, VbrLocalizationHelper.Jss15);

                                t += "</tr>";

                                s += t;

                                totalItemsCount += stu.ItemCount;
                                totalSessionCount += stu.SessionCount;
                                totalFails += stu.Fails;
                                totalRetries += stu.Retries;

                                totalAvgBackupSize += stu.AvgBackupSize;
                                totalMaxBackupSize += stu.MaxBackupSize;
                                totalAvgDataSize += stu.AvgDataSize;
                                totalMaxDataSize += stu.MaxDataSize;

                                totalWaitCount += stu.WaitCount;
                            }

                            catch (Exception ex)
                            {
                                this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                                this.log.Error("\t" + ex.Message);
                            }
                        }

                        // clean up totals:
                        double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                        totalSuccessRate = (double)Math.Round(successPercent, 2);
                        if (totalAvgDataSize == 0 && totalMaxDataSize == 0)
                        {
                            totalAvgChangeRate = 0;
                        }
                        else
                        {
                            totalAvgChangeRate = Math.Round(totalAvgDataSize / totalMaxDataSize * 100, 2);
                        }

                        // add totals line:
                        if (!skipTotals)
                        {
                            string totalRow = string.Empty;
                            totalRow += "<tr>";
                            totalRow += this.form.TableDataLeftAligned("TOTALS", string.Empty);
                            totalRow += this.form.TableData(totalItemsCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(totalSessionCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalFails.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalRetries.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalSuccessRate.ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalAvgBackupSize, 2).ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalMaxBackupSize, 2).ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalAvgDataSize, 2).ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalMaxDataSize, 2).ToString(), string.Empty);
                            if (totalAvgChangeRate == double.NaN)
                            {
                                totalAvgChangeRate = 0;
                            }

                            totalRow += this.form.TableData(totalAvgChangeRate.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalWaitCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += "</tr>";
                            s += totalRow;
                        }

                        s += this.form.SectionEnd(summary);
                    }

                    s += this.AddOffloadsTable(OffloadJobs);
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

            s += this.form.SectionEndNoTable(summary);

            // JSON job session summary by job
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var ordered = stuff.OrderBy(stu => stu.JobName).ToList();
                List<string> headers = new() { "JobName", "ItemCount", "MinJobTime", "MaxJobTime", "AvgJobTime", "SessionCount", "Fails", "Retries", "SuccessRate", "AvgBackupSize", "MaxBackupSize", "AvgDataSize", "MaxDataSize", "AvgChangeRate", "WaitCount", "MaxWait", "AvgWait", "JobTypes" };
                List<List<string>> rows = ordered.Select(stu => new List<string>
                {
                    stu.JobName,
                    stu.ItemCount.ToString(),
                    stu.MinJobTime,
                    stu.MaxJobTime,
                    stu.AvgJobTime,
                    stu.SessionCount.ToString(),
                    stu.Fails.ToString(),
                    stu.Retries.ToString(),
                    stu.SuccessRate.ToString(),
                    stu.AvgBackupSize.ToString(),
                    stu.MaxBackupSize.ToString(),
                    stu.AvgDataSize.ToString(),
                    stu.MaxDataSize.ToString(),
                    stu.AvgChangeRate.ToString(),
                    stu.WaitCount.ToString(),
                    stu.MaxWait,
                    stu.AvgWait,
                    CJobTypesParser.GetJobType(stu.JobType),
                }).ToList();
                CHtmlTables.SetSectionPublic("jobSessionSummaryByJob", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobSessionSummaryByJob JSON section: " + ex.Message);
            }

            return s;
        }

        private string AddOffloadsTable(List<CJobSummaryTypes> offloadJobs)
        {
            string s = string.Empty;
            try
            {
                var realType = "Offload";
                string jobTable = this.form.SectionStartWithButton("jobTable-offload", realType + " Jobs", string.Empty);
                s += jobTable;
                s += this.SetJobSessionsHeaders();

                int totalItemsCount = 0;
                double totalSessionCount = 0;
                int totalFails = 0;
                int totalRetries = 0;
                double totalSuccessRate = 0;
                double totalAvgBackupSize = 0;
                double totalMaxBackupSize = 0;
                double totalAvgDataSize = 0;
                double totalMaxDataSize = 0;
                double totalAvgChangeRate = 0;
                int totalWaitCount = 0;

                foreach (var stu in offloadJobs)
                {
                    try
                    {
                        string t = string.Empty;
                        t += "<tr>";

                        t += this.form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                        t += this.form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                        t += this.form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                        t += this.form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                        t += this.form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                        t += this.form.TableData(stu.SessionCount.ToString(), VbrLocalizationHelper.Jss5);
                        t += this.form.TableData(stu.Fails.ToString(), "Fails");
                        t += this.form.TableData(stu.Retries.ToString(), "Retries");
                        t += this.form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                        t += this.form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                        t += this.form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                        t += this.form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                        t += this.form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                        t += this.form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                        t += this.form.TableData(stu.WaitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += this.form.TableData(stu.MaxWait, VbrLocalizationHelper.Jss13);
                        t += this.form.TableData(stu.AvgWait, VbrLocalizationHelper.Jss14);
                        string jobType = CJobTypesParser.GetJobType(stu.JobType);
                        t += this.form.TableData(jobType, VbrLocalizationHelper.Jss15);

                        t += "</tr>";

                        s += t;

                        totalItemsCount += stu.ItemCount;
                        totalSessionCount += stu.SessionCount;
                        totalFails += stu.Fails;
                        totalRetries += stu.Retries;

                        totalAvgBackupSize += stu.AvgBackupSize;
                        totalMaxBackupSize += stu.MaxBackupSize;
                        totalAvgDataSize += stu.AvgDataSize;
                        totalMaxDataSize += stu.MaxDataSize;

                        totalWaitCount += stu.WaitCount;
                    }

                    catch (Exception ex)
                    {
                        this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        this.log.Error("\t" + ex.Message);
                    }
                }

                // clean up totals:
                double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                totalSuccessRate = (double)Math.Round(successPercent, 2);

                totalAvgChangeRate = Math.Round(totalAvgDataSize / totalMaxDataSize * 100, 2);

                // add totals line:
                string totalRow = string.Empty;
                totalRow += "<tr>";
                totalRow += this.form.TableDataLeftAligned("TOTALS", string.Empty);
                totalRow += this.form.TableData(totalItemsCount.ToString(), string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(totalSessionCount.ToString(), string.Empty);
                totalRow += this.form.TableData(totalFails.ToString(), string.Empty);
                totalRow += this.form.TableData(totalRetries.ToString(), string.Empty);
                totalRow += this.form.TableData(totalSuccessRate.ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalAvgBackupSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalMaxBackupSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalAvgDataSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalMaxDataSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(totalAvgChangeRate.ToString(), string.Empty);
                totalRow += this.form.TableData(totalWaitCount.ToString(), string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += "</tr>";
                s += totalRow;

                s += this.form.SectionEnd(string.Empty);
            }
            catch (Exception e)
            {
                this.log.Error("Job Info Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            return s;
        }

        private string SetJobSessionsHeaders()
        {
            string s = string.Empty;
            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.Jss0, VbrLocalizationHelper.Jss0TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss1, VbrLocalizationHelper.Jss1TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss2, VbrLocalizationHelper.Jss2TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss3, VbrLocalizationHelper.Jss3TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss4, VbrLocalizationHelper.Jss4TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss5, VbrLocalizationHelper.Jss5TT);
            s += this.form.TableHeader("Fails", "Total times job failed");
            s += this.form.TableHeader("Retries", "Total times job retried");
            s += this.form.TableHeader(VbrLocalizationHelper.Jss6, VbrLocalizationHelper.Jss6TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss7, VbrLocalizationHelper.Jss7TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss8, VbrLocalizationHelper.Jss8TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss9, VbrLocalizationHelper.Jss9TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss10, "Used size of all objects in job.");
            s += this.form.TableHeader(VbrLocalizationHelper.Jss11, "Avg Data Size divided by Max Data Size (average processed data divided by total consumed size of all VMs in the job)");
            s += this.form.TableHeader(VbrLocalizationHelper.Jss12, VbrLocalizationHelper.Jss12TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss13, "Max Waits (dd:hh:mm:ss)");
            s += this.form.TableHeader(VbrLocalizationHelper.Jss14, "Avg Waits (dd:hh:mm:ss)");
            s += this.form.TableHeader(VbrLocalizationHelper.Jss15, VbrLocalizationHelper.Jss15TT);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            return s;
        }
    }
}
