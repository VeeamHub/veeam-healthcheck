using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary;
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
}
