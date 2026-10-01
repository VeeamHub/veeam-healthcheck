// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Tests for the pure decision helpers behind CClientFunctions.VbrVersionSupportCheck: whether
    /// this build of Health Check is too new to run against a pre-v12 VBR. Issue #245: the old
    /// inline condition required the revision segment to exceed 546 for every major version (so
    /// 3.1.0.1 slipped through) and indexed segment [3] with no length check.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    public class CClientFunctionsVersionGuardTests
    {
        [Fact]
        public void IsVhcTooNewForPreV12_V2LastSupportedRevision_IsNotBlocked()
        {
            Assert.False(CClientFunctions.IsVhcTooNewForPreV12("2.0.0.546"));
        }

        [Fact]
        public void IsVhcTooNewForPreV12_V2RevisionAboveLastSupported_IsBlocked()
        {
            Assert.True(CClientFunctions.IsVhcTooNewForPreV12("2.0.0.547"));
        }

        [Theory]
        [InlineData("3.0.0.1")]
        [InlineData("3.1.0.1")]
        [InlineData("4.0.0.0")]
        public void IsVhcTooNewForPreV12_V3OrLaterWithLowRevision_IsBlocked(string version)
        {
            Assert.True(CClientFunctions.IsVhcTooNewForPreV12(version));
        }

        [Fact]
        public void IsVhcTooNewForPreV12_V1HighRevision_IsNotBlocked()
        {
            Assert.False(CClientFunctions.IsVhcTooNewForPreV12("1.0.0.999"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("3")]
        [InlineData("3.1.0")]
        public void IsVhcTooNewForPreV12_MissingOrShortVersion_DoesNotThrowAndIsNotBlocked(string version)
        {
            Assert.False(CClientFunctions.IsVhcTooNewForPreV12(version));
        }

        [Theory]
        [InlineData("x.y.z.w")]
        [InlineData("3.1.0.abc")]
        [InlineData("abc.0.0.1")]
        public void IsVhcTooNewForPreV12_NonNumericSegment_DoesNotThrowAndIsNotBlocked(string version)
        {
            Assert.False(CClientFunctions.IsVhcTooNewForPreV12(version));
        }

        [Fact]
        public void ShouldBlockVbrVersion_PreV12VbrWithV3Build_IsBlocked()
        {
            Assert.True(CClientFunctions.ShouldBlockVbrVersion(11, "3.1.0.1"));
        }

        [Theory]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(99)]
        public void ShouldBlockVbrVersion_V12OrLaterVbr_IsNeverBlocked(int vbrMajorVersion)
        {
            Assert.False(CClientFunctions.ShouldBlockVbrVersion(vbrMajorVersion, "3.1.0.1"));
            Assert.False(CClientFunctions.ShouldBlockVbrVersion(vbrMajorVersion, "2.0.0.547"));
        }

        [Fact]
        public void ShouldBlockVbrVersion_PreV12VbrWithLastSupportedV2Build_IsNotBlocked()
        {
            Assert.False(CClientFunctions.ShouldBlockVbrVersion(11, "2.0.0.546"));
        }

        [Fact]
        public void ShouldBlockVbrVersion_PreV12VbrWithUnparsableBuild_IsNotBlocked()
        {
            Assert.False(CClientFunctions.ShouldBlockVbrVersion(11, "not-a-version"));
        }
    }
}
