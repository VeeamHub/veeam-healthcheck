using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info
{
    /// <summary>
    /// Renders the Job Summary table using CSectionTable.
    /// Extracted from CHtmlTables.AddJobSummaryTable().
    /// </summary>
    internal class CJobSummaryInfoTable
    {
        public CJobSummaryInfoTable() { }

        public string Render(bool scrub)
        {
            try
            {
                CJobSummaryTable st = new();
                Dictionary<string, int> list = st.JobSummaryTable();
                int totalJobs = list.Sum(x => x.Value);

                // Filter out zero-count entries and add a total row
                var displayData = list
                    .Where(d => d.Value > 0)
                    .Select(d => new JobSummaryRow { JobType = d.Key, Count = d.Value.ToString() })
                    .ToList();

                displayData.Add(new JobSummaryRow { JobType = "<b>Total Jobs", Count = totalJobs.ToString() + "</b>" });

                var table = new CSectionTable<JobSummaryRow>("jobsummary", VbrLocalizationHelper.JobSumTitle)
                    .WithIcon("J", "#eff6ff", "#1d4ed8")
                    .Column(VbrLocalizationHelper.JobSum0, VbrLocalizationHelper.JobSum0TT, item => item.JobType, leftAlign: true)
                    .Column(VbrLocalizationHelper.JobSum1, VbrLocalizationHelper.JobSum1TT, item => item.Count);

                string html = table.Render(displayData);

                // JSON capture for the structured report
                CaptureJson(list, totalJobs);

                return html;
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Job Summary Data import failed. ERROR:");
                CGlobals.Logger.Error("\t" + e.Message);
                return string.Empty;
            }
        }

        private static void CaptureJson(Dictionary<string, int> list, int totalJobs)
        {
            try
            {
                List<string> headers = new() { "JobType", "Count" };
                List<List<string>> rows = list
                    .Where(d => d.Value > 0)
                    .Select(d => new List<string> { d.Key, d.Value.ToString() })
                    .ToList();
                rows.Add(new List<string> { "Total Jobs", totalJobs.ToString() });

                if (CGlobals.FullReportJson == null)
                    CGlobals.FullReportJson = new();

                CGlobals.FullReportJson.Sections["jobSummary"] = new HtmlSection
                {
                    SectionName = "jobSummary",
                    Headers = headers,
                    Rows = rows,
                };
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error("Failed to capture jobSummary JSON section: " + ex.Message);
            }
        }

        /// <summary>
        /// Simple data class for job summary rows.
        /// </summary>
        private class JobSummaryRow
        {
            public string JobType { get; set; } = "";
            public string Count { get; set; } = "";
        }
    }
}
