// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.IO;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// A NAS backup job in _Jobs.csv with no matching row in _nasBackup.csv (the NAS collector
    /// failed, or the job has no backup yet) must not abort Job Info. The size lookup returns a
    /// null dynamic there, and double.TryParse(dynamic null, ...) is ambiguous between the string
    /// and ReadOnlySpan&lt;byte&gt; overloads at runtime on .NET 10, which threw and dropped every
    /// later section (CI/CD run 39621299142, integration-test-vbr-2).
    /// </summary>
    [Collection("GlobalState")]
    public class CJobInfoNasBackupTests : VbrTableScrubTestBase
    {
        public CJobInfoNasBackupTests() : base("VhcNasJobInfoTests_")
        {
            CGlobals.FullReportJson = new CFullReportJson();
            File.WriteAllText(
                Path.Combine(VbrDir, "_Jobs.csv"),
                VbrCsvSampleGenerator.GenerateJobsWithNasBackupThenVmBackup());
        }

        [Fact]
        public void Render_NasBackupJobWithoutNasBackupCsv_StillRendersTheLaterSections()
        {
            string html = new CJobInfoTable().Render(scrub: false);

            Assert.Contains("jobTable-nasbackup-nas-backup", html);
            Assert.Contains("jobTable-backup-vmware-backup", html);
        }

        [Fact]
        public void Render_NasBackupJobWithNoMatchingNasBackupRow_StillRendersTheLaterSections()
        {
            File.WriteAllText(
                Path.Combine(VbrDir, "_nasBackup.csv"),
                "\"name\",\"ondiskgb\",\"sourcegb\"\n\"Some Other NAS Job\",\"1\",\"2\"\n");

            string html = new CJobInfoTable().Render(scrub: false);

            Assert.Contains("jobTable-nasbackup-nas-backup", html);
            Assert.Contains("jobTable-backup-vmware-backup", html);
        }

        [Fact]
        public void Render_NasBackupJobWithNasBackupRow_ReportsTheSourceSizeFromTheCsv()
        {
            File.WriteAllText(
                Path.Combine(VbrDir, "_nasBackup.csv"),
                "\"name\",\"ondiskgb\",\"sourcegb\"\n\"A NAS Job\",\"12.5\",\"30\"\n");

            string html = new CJobInfoTable().Render(scrub: false);

            Assert.Contains("30 GB", html);
            Assert.Contains("jobTable-backup-vmware-backup", html);
        }
    }
}
