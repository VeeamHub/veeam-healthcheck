// <copyright file="CPowerShellVersionCheckerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VeeamHealthCheck.Tests.Functions.Collection.PSCollections
{
    public class CPowerShellVersionCheckerTests
    {
        [Theory]
        [InlineData("PowerShellVersion = '7.6'", "7.6")]
        [InlineData("PowerShellVersion = \"7.6\"", "7.6")]
        [InlineData("PowerShellVersion   =   '7.5.1'", "7.5.1")]
        [InlineData("@{\n  RootModule = 'Veeam.Backup.PowerShell.dll'\n  PowerShellVersion = '7.6'\n  Author = 'Veeam'\n}", "7.6")]
        public void TryParseManifestContent_ValidPowerShellVersionEntry_ParsesVersion(string manifest, string expected)
        {
            bool success = CPowerShellVersionChecker.TryParseManifestContent(manifest, out Version result);

            Assert.True(success);
            Assert.Equal(Version.Parse(expected), result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("@{ RootModule = 'Veeam.Backup.PowerShell.dll' }")]
        [InlineData("PowerShellVersion = ''")]
        public void TryParseManifestContent_MissingOrUnparsableEntry_ReturnsFalse(string manifest)
        {
            bool success = CPowerShellVersionChecker.TryParseManifestContent(manifest, out Version result);

            Assert.False(success);
            Assert.Null(result);
        }

        [Theory]
        [InlineData("7.4.6", "7.4.6")]
        [InlineData("7.6.0\n", "7.6.0")]
        [InlineData("  7.6.0  ", "7.6.0")]
        [InlineData("7.6.0-preview.3", "7.6.0")]
        public void TryParsePwshVersionOutput_ValidOutput_ParsesVersion(string rawOutput, string expectedVersion)
        {
            bool success = CPowerShellVersionChecker.TryParsePwshVersionOutput(rawOutput, out Version installed, out string raw);

            Assert.True(success);
            Assert.Equal(Version.Parse(expectedVersion), installed);
            Assert.Equal(rawOutput.Trim(), raw);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("not-a-version")]
        public void TryParsePwshVersionOutput_EmptyOrUnparsableOutput_ReturnsFalse(string rawOutput)
        {
            bool success = CPowerShellVersionChecker.TryParsePwshVersionOutput(rawOutput, out Version installed, out _);

            Assert.False(success);
            Assert.Null(installed);
        }

        [Fact]
        public void VersionComparison_BelowRequirement_IsDetected()
        {
            CPowerShellVersionChecker.TryParsePwshVersionOutput("7.4.6", out Version installed, out _);
            CPowerShellVersionChecker.TryParseManifestContent("PowerShellVersion = '7.6'", out Version required);

            Assert.True(installed < required);
        }

        [Theory]
        [InlineData("7.6.0")]
        [InlineData("7.6.1")]
        [InlineData("8.0.0")]
        public void VersionComparison_MeetsOrExceedsRequirement_IsNotBelowRequirement(string installedRaw)
        {
            CPowerShellVersionChecker.TryParsePwshVersionOutput(installedRaw, out Version installed, out _);
            CPowerShellVersionChecker.TryParseManifestContent("PowerShellVersion = '7.6'", out Version required);

            Assert.False(installed < required);
        }

        [Fact]
        public void TryParsePwshVersionOutput_AnsiWrappedOutput_ParsesVersion()
        {
            string ansiOutput = ((char)0x1B) + "[93m7.6.0" + ((char)0x1B) + "[0m";

            bool success = CPowerShellVersionChecker.TryParsePwshVersionOutput(ansiOutput, out Version installed, out string raw);

            Assert.True(success);
            Assert.Equal(Version.Parse("7.6.0"), installed);
            Assert.Equal("7.6.0", raw);
        }

        [Fact]
        public void TryParsePwshVersionOutput_NoiseLineBeforeVersion_ParsesLastLine()
        {
            bool success = CPowerShellVersionChecker.TryParsePwshVersionOutput("Update available: 7.6.1\n7.4.6", out Version installed, out string raw);

            Assert.True(success);
            Assert.Equal(Version.Parse("7.4.6"), installed);
            Assert.Equal("7.4.6", raw);
        }

        [Fact]
        public void TryParseManifestContent_CommentedOutEntryPrecedingLiveEntry_ParsesLiveEntry()
        {
            bool success = CPowerShellVersionChecker.TryParseManifestContent(
                "# PowerShellVersion = '5.1'\nPowerShellVersion = '7.6'",
                out Version required);

            Assert.True(success);
            Assert.Equal(Version.Parse("7.6"), required);
        }

        [Fact]
        public void TryParseManifestContent_EntirelyCommentedOutEntry_ReturnsFalse()
        {
            bool success = CPowerShellVersionChecker.TryParseManifestContent("# PowerShellVersion = '5.1'", out Version required);

            Assert.False(success);
            Assert.Null(required);
        }
    }
}
