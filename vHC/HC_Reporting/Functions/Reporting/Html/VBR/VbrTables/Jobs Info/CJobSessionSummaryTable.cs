using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataFormers.AgentJobs;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
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
    internal class CJobSessionSummaryTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CJobSessionSummaryTable() { }

        private Dictionary<string, AgentJobRecord> BuildAgentJobsByName(bool scrub)
        {
            // ConvertJobSessSummaryToXml(scrub: true) returns sessions whose JobName has
            // already been scrubbed for output, while CDataFormer.AgentJobs is built from
            // raw _Jobs.csv. In scrub mode, key the lookup dictionary by the scrubbed
            // job name so both sides of the lookup go through the same transform —
            // otherwise the agent-session FriendlyType resolution always misses.
            return this.df.AgentJobs
                .ToDictionary(
                    a => ScrubJobNameIfNeeded(a.JobName, scrub),
                    a => a,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string ScrubJobNameIfNeeded(string jobName, bool scrub)
        {
            if (!scrub || string.IsNullOrEmpty(jobName))
            {
                return jobName ?? string.Empty;
            }
            return CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Job) ?? string.Empty;
        }

        private string ResolveSessionType(
            Dictionary<string, AgentJobRecord> agentJobsByName,
            string jobName,
            string rawJobType)
        {
            string lookupName = jobName ?? string.Empty;
            if (agentJobsByName.TryGetValue(lookupName, out var record))
            {
                return record.FriendlyType;
            }

            // Policy jobs emit per-machine child sessions with names like
            // "<ParentJobName> - <VmFqdn>". Try matching the prefix before
            // " - " so child sessions resolve to their parent job's
            // FriendlyType.
            const string childDelimiter = " - ";
            int delim = lookupName.IndexOf(childDelimiter, StringComparison.Ordinal);
            if (delim > 0)
            {
                string prefix = lookupName.Substring(0, delim);
                if (agentJobsByName.TryGetValue(prefix, out var parentRecord))
                {
                    return parentRecord.FriendlyType;
                }
            }

            return CJobTypesParser.GetJobType(rawJobType);
        }

        public string RenderFlat(bool scrub)
        {
            this.log.Info("Adding Job Session Summary Table");
            string s = this.form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            string summary = this.sum.JobSessSummary();

            s += this.SetJobSessionsHeaders();
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var agentJobsByName = this.BuildAgentJobsByName(scrub);

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
                        t += this.form.TableData(stu.AvgDedupRatio > 0 ? stu.AvgDedupRatio.ToString() + "x" : "", VbrLocalizationHelper.Jss16);
                        t += this.form.TableData(stu.AvgCompressionRatio > 0 ? stu.AvgCompressionRatio.ToString() + "x" : "", VbrLocalizationHelper.Jss17);
                        t += this.form.TableData(stu.WaitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += this.form.TableData(stu.MaxWait, VbrLocalizationHelper.Jss13);
                        t += this.form.TableData(stu.AvgWait, VbrLocalizationHelper.Jss14);
                        if (CGlobals.DEBUG)
                        {
                            this.log.Debug("Job Name = " + stu.JobName);
                            this.log.Debug("Job Type = " + stu.JobType);
                        }

                        string jobType = this.ResolveSessionType(agentJobsByName, stu.JobName, stu.JobType);
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

            s += this.form.SectionEndWithEmptyState(s, summary);
            this.log.Info("Job Session Summary Table added");

            // JSON job session summary
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var agentJobsByNameJson = this.BuildAgentJobsByName(scrub);
                List<string> headers = new() { "JobName", "ItemCount", "MinJobTime", "MaxJobTime", "AvgJobTime", "SessionCount", "Fails", "Retries", "SuccessRate", "AvgBackupSize", "MaxBackupSize", "AvgDataSize", "MaxDataSize", "AvgChangeRate", "AvgDedupRatio", "AvgCompressRatio", "WaitCount", "MaxWait", "AvgWait", "JobTypes" };
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
                    stu.AvgDedupRatio > 0 ? stu.AvgDedupRatio.ToString() + "x" : "",
                    stu.AvgCompressionRatio > 0 ? stu.AvgCompressionRatio.ToString() + "x" : "",
                    stu.WaitCount.ToString(),
                    stu.MaxWait,
                    stu.AvgWait,
                    this.ResolveSessionType(agentJobsByNameJson, stu.JobName, stu.JobType),
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
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var agentJobsByName = this.BuildAgentJobsByName(scrub);

                // Annotate each session with its FriendlyType, then group by that label.
                // Managed and standalone agents share session-level JobType (EEndPoint) and
                // would otherwise collapse into one section; grouping by FriendlyType keeps
                // them separate ("Windows Agent Backup Jobs" vs "Windows Agent Standalone
                // Jobs", etc.).
                //
                // The synthetic "Total" row (JobName="Total", JobType=null) is excluded —
                // it represents an aggregate across all sessions, not a real session, and
                // without filtering it would land in the parser-fallback "Other" group and
                // render as a phantom "Other Jobs" section containing just that summary row.
                var annotated = stuff
                    .Where(sess => !string.Equals(sess.JobName, "Total", StringComparison.Ordinal))
                    .Select(sess => new
                    {
                        Session = sess,
                        FriendlyType = this.ResolveSessionType(agentJobsByName, sess.JobName, sess.JobType),
                    })
                    .ToList();

                var sessionGroups = annotated
                    .GroupBy(x => x.FriendlyType ?? string.Empty)
                    .ToList();

                List<CJobSummaryTypes> OffloadJobs = new();

                try
                {
                    foreach (var grp in sessionGroups)
                    {
                        if (CGlobals.DEBUG)
                        {
                            this.log.Debug("Job Type = " + grp.Key);
                        }

                        bool skipTotals = false;
                        string sectionHeader = string.IsNullOrEmpty(grp.Key) ? "Summary of All" : grp.Key;
                        string sectionSlug = sectionHeader.ToLower().Replace(" ", "-");

                        string jobTable = this.form.SectionStartWithButton("jobTable-" + sectionSlug, sectionHeader + " Jobs", string.Empty);
                        s += jobTable;
                        s += this.SetJobSessionsHeaders();
                        var res = grp.Select(x => x.Session).ToList();

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
                        double weightedChangeRateSum = 0;
                        double totalDedupRatioSum = 0;
                        double totalCompressRatioSum = 0;
                        int dedupCount = 0;
                        int compressCount = 0;
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

                            // No JobType filter needed — sessions are already grouped by FriendlyType.

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
                                t += this.form.TableData(stu.AvgDedupRatio > 0 ? stu.AvgDedupRatio.ToString() + "x" : "", VbrLocalizationHelper.Jss16);
                                t += this.form.TableData(stu.AvgCompressionRatio > 0 ? stu.AvgCompressionRatio.ToString() + "x" : "", VbrLocalizationHelper.Jss17);
                                t += this.form.TableData(stu.WaitCount.ToString(), VbrLocalizationHelper.Jss12);
                                t += this.form.TableData(stu.MaxWait, VbrLocalizationHelper.Jss13);
                                t += this.form.TableData(stu.AvgWait, VbrLocalizationHelper.Jss14);
                                string jt = this.ResolveSessionType(agentJobsByName, stu.JobName, stu.JobType);
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
                                weightedChangeRateSum += stu.AvgChangeRate * stu.MaxDataSize;
                                if (stu.AvgDedupRatio > 0) { totalDedupRatioSum += stu.AvgDedupRatio; dedupCount++; }
                                if (stu.AvgCompressionRatio > 0) { totalCompressRatioSum += stu.AvgCompressionRatio; compressCount++; }

                                totalWaitCount += stu.WaitCount;
                            }

                            catch (Exception ex)
                            {
                                this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                                this.log.Error("\t" + ex.Message);
                            }
                        }

                        // clean up totals:
                        double successPercent = 0;
                        if (totalSessionCount > 0)
                        {
                            successPercent = (totalSessionCount - (double)totalFails) / totalSessionCount * 100;
                        }
                        totalSuccessRate = (double)Math.Round(successPercent, 2);
                        totalAvgChangeRate = totalMaxDataSize > 0
                            ? Math.Round(weightedChangeRateSum / totalMaxDataSize, 2) : 0;

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
                            totalRow += this.form.TableData(totalAvgChangeRate.ToString(), string.Empty);
                            totalRow += this.form.TableData(dedupCount > 0 ? Math.Round(totalDedupRatioSum / dedupCount, 2).ToString() + "x" : "", string.Empty);
                            totalRow += this.form.TableData(compressCount > 0 ? Math.Round(totalCompressRatioSum / compressCount, 2).ToString() + "x" : "", string.Empty);
                            totalRow += this.form.TableData(totalWaitCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += "</tr>";
                            s += totalRow;
                        }

                        s += this.form.SectionEndWithEmptyState(s, summary);
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
                var agentJobsByNameJson = this.BuildAgentJobsByName(scrub);
                List<string> headers = new() { "JobName", "ItemCount", "MinJobTime", "MaxJobTime", "AvgJobTime", "SessionCount", "Fails", "Retries", "SuccessRate", "AvgBackupSize", "MaxBackupSize", "AvgDataSize", "MaxDataSize", "AvgChangeRate", "AvgDedupRatio", "AvgCompressRatio", "WaitCount", "MaxWait", "AvgWait", "JobTypes" };
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
                    stu.AvgDedupRatio > 0 ? stu.AvgDedupRatio.ToString() + "x" : "",
                    stu.AvgCompressionRatio > 0 ? stu.AvgCompressionRatio.ToString() + "x" : "",
                    stu.WaitCount.ToString(),
                    stu.MaxWait,
                    stu.AvgWait,
                    this.ResolveSessionType(agentJobsByNameJson, stu.JobName, stu.JobType),
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
                double weightedChangeRateSum = 0;
                double totalDedupRatioSum = 0;
                double totalCompressRatioSum = 0;
                int dedupCount = 0;
                int compressCount = 0;
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
                        t += this.form.TableData(stu.AvgDedupRatio > 0 ? stu.AvgDedupRatio.ToString() + "x" : "", VbrLocalizationHelper.Jss16);
                        t += this.form.TableData(stu.AvgCompressionRatio > 0 ? stu.AvgCompressionRatio.ToString() + "x" : "", VbrLocalizationHelper.Jss17);
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
                        weightedChangeRateSum += stu.AvgChangeRate * stu.MaxDataSize;
                        if (stu.AvgDedupRatio > 0) { totalDedupRatioSum += stu.AvgDedupRatio; dedupCount++; }
                        if (stu.AvgCompressionRatio > 0) { totalCompressRatioSum += stu.AvgCompressionRatio; compressCount++; }

                        totalWaitCount += stu.WaitCount;
                    }

                    catch (Exception ex)
                    {
                        this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        this.log.Error("\t" + ex.Message);
                    }
                }

                // clean up totals:
                double successPercent = 0;
                if (totalSessionCount > 0)
                {
                    successPercent = (totalSessionCount - (double)totalFails) / totalSessionCount * 100;
                }
                totalSuccessRate = (double)Math.Round(successPercent, 2);
                totalAvgChangeRate = totalMaxDataSize > 0
                    ? Math.Round(weightedChangeRateSum / totalMaxDataSize, 2) : 0;

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
                totalRow += this.form.TableData(dedupCount > 0 ? Math.Round(totalDedupRatioSum / dedupCount, 2).ToString() + "x" : "", string.Empty);
                totalRow += this.form.TableData(compressCount > 0 ? Math.Round(totalCompressRatioSum / compressCount, 2).ToString() + "x" : "", string.Empty);
                totalRow += this.form.TableData(totalWaitCount.ToString(), string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += "</tr>";
                s += totalRow;

                s += this.form.SectionEndWithEmptyState(s, string.Empty);
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
            s += this.form.TableHeader(VbrLocalizationHelper.Jss11, "Average incremental data size divided by source VM size (weighted average for totals)");
            s += this.form.TableHeader(VbrLocalizationHelper.Jss16, VbrLocalizationHelper.Jss16TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Jss17, VbrLocalizationHelper.Jss17TT);
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
