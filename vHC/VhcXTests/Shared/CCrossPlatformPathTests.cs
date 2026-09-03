using System.IO;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Shared
{
    public class CCrossPlatformPathTests
    {
        [Fact]
        public void Combine_SegmentsWithLeadingBackslash_StripsBackslashBeforeJoining()
        {
            string result = CCrossPlatformPath.Combine("/base", @"\vHC-Report", @"\JobSessionReports");

            Assert.Equal(Path.Combine("/base", "vHC-Report", "JobSessionReports"), result);
        }

        [Fact]
        public void Combine_SegmentsWithLeadingForwardSlash_StripsForwardSlashBeforeJoining()
        {
            string result = CCrossPlatformPath.Combine("/base", "/vHC-Report", "/JobSessionReports");

            Assert.Equal(Path.Combine("/base", "vHC-Report", "JobSessionReports"), result);
        }

        [Fact]
        public void Combine_SegmentLooksLikeWindowsDriveLetter_PassesThroughUntrimmed()
        {
            // Documented contract: callers must pass trusted, relative literal
            // segments. This helper only strips leading slashes/backslashes; it
            // does not sanitize an untrusted segment that still looks rooted
            // after trimming (e.g. "C:\evil") — that's SanitizeFileName's job
            // for user-controlled input such as job names.
            string result = CCrossPlatformPath.Combine("/base", @"C:\evil");

            Assert.Equal(Path.Combine("/base", @"C:\evil"), result);
        }
    }
}
