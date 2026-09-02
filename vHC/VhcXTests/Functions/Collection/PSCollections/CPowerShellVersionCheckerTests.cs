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
        public void TryParsePwshVersionOutput_VersionBelowManifestRequirement_ComparesAsLower()
        {
            CPowerShellVersionChecker.TryParsePwshVersionOutput("7.4.6", out Version installed, out _);
            CPowerShellVersionChecker.TryParseManifestContent("PowerShellVersion = '7.6'", out Version required);

            Assert.True(installed < required);
        }

        [Theory]
        [InlineData("7.6.0")]
        [InlineData("7.6.1")]
        [InlineData("8.0.0")]
        public void TryParsePwshVersionOutput_VersionMeetsOrExceedsManifestRequirement_DoesNotCompareAsLower(string installedRaw)
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void EvaluatePwshVersionStatus_PwshPathMissing_ReturnsNotInstalled(string pwshPath)
        {
            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
                pwshPath, null, Version.Parse("7.6"));

            Assert.Equal(PwshVersionStatus.NotInstalled, status);
        }

        [Fact]
        public void EvaluatePwshVersionStatus_PwshPathMissingAndRequiredVersionAlsoNull_ReturnsNotInstalled()
        {
            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
                null, null, null);

            Assert.Equal(PwshVersionStatus.NotInstalled, status);
        }

        [Fact]
        public void EvaluatePwshVersionStatus_PwshFoundButRequiredVersionUnknown_ReturnsVersionInconclusive()
        {
            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
                @"C:\Program Files\PowerShell\7\pwsh.exe", Version.Parse("7.6.0"), null);

            Assert.Equal(PwshVersionStatus.VersionInconclusive, status);
        }

        [Fact]
        public void EvaluatePwshVersionStatus_PwshFoundButInstalledVersionUndetermined_ReturnsVersionInconclusive()
        {
            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
                @"C:\Program Files\PowerShell\7\pwsh.exe", null, Version.Parse("7.6"));

            Assert.Equal(PwshVersionStatus.VersionInconclusive, status);
        }

        [Fact]
        public void EvaluatePwshVersionStatus_InstalledBelowRequired_ReturnsBelowRequirement()
        {
            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
                @"C:\Program Files\PowerShell\7\pwsh.exe", Version.Parse("7.4.6"), Version.Parse("7.6"));

            Assert.Equal(PwshVersionStatus.BelowRequirement, status);
        }

        [Theory]
        [InlineData("7.6.0")]
        [InlineData("7.6.1")]
        [InlineData("8.0.0")]
        public void EvaluatePwshVersionStatus_InstalledMeetsOrExceedsRequired_ReturnsMeetsRequirement(string installedRaw)
        {
            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
                @"C:\Program Files\PowerShell\7\pwsh.exe", Version.Parse(installedRaw), Version.Parse("7.6"));

            Assert.Equal(PwshVersionStatus.MeetsRequirement, status);
        }

        [Fact]
        public void BuildPwshVersionFailureMessage_NotInstalledWithKnownRequiredVersion_MentionsRequiredVersionAndInstallLink()
        {
            string msg = CPowerShellVersionChecker.BuildPwshVersionFailureMessage(
                PwshVersionStatus.NotInstalled, "13.1.0.1234", Version.Parse("7.6"), null);

            Assert.Contains("requires PowerShell 7.6 or higher", msg);
            Assert.Contains("no PowerShell 7 installation was found", msg);
            Assert.Contains("https://aka.ms/powershell-release?tag=stable", msg);
            Assert.Contains("VBR 13.1.0.1234", msg);
        }

        [Fact]
        public void BuildPwshVersionFailureMessage_NotInstalledWithUnknownRequiredVersion_UsesGenericPowerShell7Wording()
        {
            string msg = CPowerShellVersionChecker.BuildPwshVersionFailureMessage(
                PwshVersionStatus.NotInstalled, "13.1.0.1234", null, null);

            Assert.Contains("requires PowerShell 7", msg);
            Assert.DoesNotContain("requires PowerShell 7.", msg);
            Assert.Contains("no PowerShell 7 installation was found", msg);
        }

        [Fact]
        public void BuildPwshVersionFailureMessage_BelowRequirement_MatchesExistingMessageWording()
        {
            string msg = CPowerShellVersionChecker.BuildPwshVersionFailureMessage(
                PwshVersionStatus.BelowRequirement, "13.1.0.1234", Version.Parse("7.6"), "7.4.6");

            Assert.Equal(
                "The Veeam Backup & Replication PowerShell module (VBR 13.1.0.1234) requires PowerShell 7.6 or higher, " +
                "but this computer has PowerShell 7.4.6 installed. Install a newer PowerShell 7 release " +
                "(https://aka.ms/powershell-release?tag=stable) and re-run Veeam Health Check.",
                msg);
        }

        [Fact]
        public void BuildPwshVersionFailureMessage_NonFailureStatus_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CPowerShellVersionChecker.BuildPwshVersionFailureMessage(PwshVersionStatus.MeetsRequirement, "13.1.0.1234", Version.Parse("7.6"), "7.4.6"));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CPowerShellVersionChecker.BuildPwshVersionFailureMessage(PwshVersionStatus.VersionInconclusive, "13.1.0.1234", Version.Parse("7.6"), "7.4.6"));
        }
    }
}
