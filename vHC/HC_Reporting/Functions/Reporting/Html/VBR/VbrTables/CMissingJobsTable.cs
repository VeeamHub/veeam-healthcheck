// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Renders the Missing Jobs table (formerly AddMissingJobsTable in CHtmlTables).
    /// </summary>
    internal class CMissingJobsTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CMissingJobsTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("missingjobs", VbrLocalizationHelper.NpTitle, VbrLocalizationHelper.NpButton);

            string summary = this.sum.MissingJobsSUmmary();

            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.JobSum0, string.Empty);

                // _form.TableHeader("Count", "Total detected of this type")
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CJobSummaryTable st = new();
                Dictionary<string, int> types = st.JobSummaryTable();

                foreach (var t in types)
                {
                    if (t.Value == 0)
                    {
                        s += "<tr>";
                        s += this.form.TableDataLeftAligned(t.Key, string.Empty);
                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                this.log.Error("Missing Jobs Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON missing jobs
            try
            {
                CJobSummaryTable st = new();
                Dictionary<string, int> types = st.JobSummaryTable();
                List<string> headers = new() { "MissingJobType" };
                List<List<string>> rows = types.Where(t => t.Value == 0).Select(t => new List<string> { t.Key }).ToList();
                CHtmlTables.SetSectionPublic("missingJobs", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture missingJobs JSON section: " + ex.Message);
            }

            return s;
        }
    }
}
