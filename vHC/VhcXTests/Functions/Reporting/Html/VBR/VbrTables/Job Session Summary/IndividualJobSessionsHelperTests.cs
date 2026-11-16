using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary
{
    public class IndividualJobSessionsHelperTests
    {
        [Theory]
        [InlineData("C: Nightly Backup")]
        [InlineData("d:report")]
        [InlineData("Z:")]
        public void SanitizeFileName_JobNameLooksLikeWindowsDriveLetter_StripsColonSoPathCombineCannotEscape(string jobName)
        {
            string sanitized = IndividualJobSessionsHelper.SanitizeFileName(jobName);

            // Path.Combine(basePath, second) silently discards basePath whenever
            // `second` looks like a Windows drive-rooted segment: a single letter
            // followed by ':'. Assert against that exact rule rather than
            // Path.IsPathRooted, since IsPathRooted's drive-letter check is
            // Windows-only behavior and this suite also runs on macOS/Linux.
            bool looksLikeWindowsRootedDriveSegment =
                sanitized.Length >= 2 && char.IsLetter(sanitized[0]) && sanitized[1] == ':';

            Assert.False(
                looksLikeWindowsRootedDriveSegment,
                $"Sanitized name '{sanitized}' would still be treated as a rooted path segment by Path.Combine on Windows.");
        }
    }

    /// <summary>
    /// The anonymized per-job session reports (vHC-AnonymousReport/JobSessionReports) must carry
    /// no raw job, VM or server name anywhere in their HTML. Only synthetic names are used here.
    /// </summary>
    [Collection("GlobalState")]
    [Trait("Category", "Scrubbing")]
    public class IndividualJobSessionsHelperScrubTests : IDisposable
    {
        private const string JobName = "Synthetic-Job-Alpha";
        private const string VmName = "synthetic-vm-alpha-01";
        private const string ProxyName = "synthetic-proxy-alpha";

        private readonly string originalDesiredPath;
        private readonly string tempDir;
        private readonly CScrubHandler scrubber;

        public IndividualJobSessionsHelperScrubTests()
        {
            this.originalDesiredPath = CGlobals.desiredPath;

            // ScrubItem persists its key file under CVariables.unsafeDir; keep it out of the repo.
            this.tempDir = Path.Combine(Path.GetTempPath(), "vhc-jobsess-scrub-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(this.tempDir, "Original"));
            CGlobals.desiredPath = this.tempDir;

            this.scrubber = new CScrubHandler();
        }

        public void Dispose()
        {
            CGlobals.desiredPath = this.originalDesiredPath;
            try { Directory.Delete(this.tempDir, recursive: true); } catch { /* best effort */ }
        }

        private static CJobSessionInfo Session(string? jobName, string vmName) => new CJobSessionInfo
        {
            JobName = jobName,
            VmName = vmName,
            JobAlg = "Increment",
            Status = "Success",
            ProcessingMode = "hotadd",
            CreationTime = new DateTime(2026, 1, 2, 3, 4, 5),
        };

        private string BuildScrubbed(string displayName, params CJobSessionInfo[] sessions) =>
            new IndividualJobSessionsHelper(this.scrubber).BuildJobSessionReport(displayName, new List<CJobSessionInfo>(sessions), scrub: true);

        private string BuildRaw(string displayName, params CJobSessionInfo[] sessions) =>
            new IndividualJobSessionsHelper(this.scrubber).BuildJobSessionReport(displayName, new List<CJobSessionInfo>(sessions), scrub: false);

        [Fact]
        public void BuildJobSessionReport_Scrubbed_ContainsNoRawJobOrVmName()
        {
            string html = this.BuildScrubbed(JobName, Session(JobName, VmName));

            Assert.DoesNotContain(JobName, html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(VmName, html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BuildJobSessionReport_Scrubbed_HeadingMatchesScrubbedFileNameToken()
        {
            string html = this.BuildScrubbed(JobName, Session(JobName, VmName));

            // SetScrubDir names the file after scrubber.ScrubItem(displayName, Job).
            string fileToken = this.scrubber.ScrubItem(JobName, ScrubItemType.Job);
            Match heading = Regex.Match(html, "<h2>(.*?)</h2>");

            Assert.True(heading.Success, "Report has no <h2> heading.");
            Assert.Equal(fileToken, heading.Groups[1].Value);
            Assert.Equal(IndividualJobSessionsHelper.SanitizeFileName(fileToken), IndividualJobSessionsHelper.SanitizeFileName(heading.Groups[1].Value));
        }

        [Fact]
        public void BuildJobSessionReport_ScrubbedHierarchicalChild_RowUsesTokenOfRawIdentity()
        {
            // VBR reports per-machine child sessions as "<Parent>\<child>". The row must be
            // scrubbed from the raw identity, not from a "--"-rewritten copy that registers a
            // different original in the key file.
            string child = JobName + "\\" + VmName;
            string html = this.BuildScrubbed(JobName, Session(child, VmName));

            Assert.DoesNotContain(JobName, html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(VmName, html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(this.scrubber.ScrubItem(child, ScrubItemType.Job), html, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildJobSessionReport_ScrubbedRegisteredNameInFreeTextField_IsReplaced()
        {
            // A name already scrubbed elsewhere in the report (here a proxy) that turns up in a
            // free-text column must be caught by the same final pass the main report runs.
            string proxyToken = this.scrubber.ScrubItem(ProxyName, ScrubItemType.Server);
            var session = Session(JobName, VmName);
            session.ProcessingMode = "hotadd via " + ProxyName;

            string html = this.BuildScrubbed(JobName, session);

            Assert.DoesNotContain(ProxyName, html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(proxyToken, html, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildJobSessionReport_SessionWithNullJobName_ScrubbedDiffersFromRawAndHasNoRawName()
        {
            var session = Session(null, VmName);

            string scrubbed = this.BuildScrubbed(JobName, session);
            string raw = this.BuildRaw(JobName, session);

            Assert.NotEqual(raw, scrubbed);
            Assert.DoesNotContain(JobName, scrubbed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(VmName, scrubbed, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<td title=\"vmName\">", scrubbed, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildJobSessionReport_Unscrubbed_KeepsRealNames()
        {
            string html = this.BuildRaw(JobName, Session(JobName, VmName));

            Assert.Contains("<h2>" + JobName + "</h2>", html, StringComparison.Ordinal);
            Assert.Contains(VmName, html, StringComparison.Ordinal);
        }
    }
}
