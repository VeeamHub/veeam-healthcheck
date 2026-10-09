// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Issue #225: VBR's internal per-source Backup Copy worker (SimpleBackupCopyParentWorker,
    /// named "Parent\Child") must not show up as a job of its own in Job Info or the job summary.
    /// </summary>
    [Collection("GlobalState")]
    public class CJobsBackupCopyWorkerTests : VbrTableScrubTestBase
    {
        private const string WorkerJobType = "SimpleBackupCopyParentWorker";
        private const string WorkerName = "CopyJobA\\SourceJobA - Nutanix Backup";

        public CJobsBackupCopyWorkerTests() : base("VhcBcWorkerTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
            File.WriteAllText(
                Path.Combine(VbrDir, "_Jobs.csv"),
                VbrCsvSampleGenerator.GenerateJobsWithBackupCopyWorker());
        }

        [Fact]
        public void JobSummaryTable_BackupCopyWorkerRow_IsNotCounted()
        {
            var counts = new CJobSummaryTable().JobSummaryTable();

            Assert.DoesNotContain(WorkerJobType, counts.Keys);
            Assert.Equal(1, counts["Backup Copy"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Render_BackupCopyWorkerRow_IsNotListedAsAJob(bool scrub)
        {
            string html = new CJobInfoTable().Render(scrub);

            // The section anchor is "jobTable-<jobtype>-<friendly type>", so it exposes the
            // worker's own section whether or not the names in it are scrubbed.
            Assert.DoesNotContain("jobTable-simplebackupcopyparentworker", html);
            Assert.Contains("jobTable-simplebackupcopypolicy-backup-copy", html);
            Assert.Contains("jobTable-vmbapipolicytempjob-nutanix-backup", html);
        }

        [Fact]
        public void Render_BackupCopyWorkerRow_NameIsNotInTheUnscrubbedReport()
        {
            string html = new CJobInfoTable().Render(scrub: false);

            Assert.DoesNotContain(WorkerName, html);
            Assert.Contains("SourceJobA - Nutanix Backup", html);
        }

        [Fact]
        public void Render_BackupCopyWorkerRow_OnDiskSizeIsFoldedIntoTheParentInJson()
        {
            new CJobInfoTable().Render(scrub: false);

            var section = CGlobals.FullReportJson.Sections["jobInfo"];
            int nameCol = section.Headers.IndexOf("JobName");
            int onDiskCol = section.Headers.IndexOf("OnDiskGB");
            var onDiskByJob = section.Rows.ToDictionary(r => r[nameCol], r => r[onDiskCol]);

            Assert.Equal(new[] { "CopyJobA", "SourceJobA - Nutanix Backup" }, onDiskByJob.Keys.OrderBy(k => k).ToArray());
            Assert.Equal("35.25", onDiskByJob["CopyJobA"]);
            Assert.Equal("40", onDiskByJob["SourceJobA - Nutanix Backup"]);
        }
    }
}
