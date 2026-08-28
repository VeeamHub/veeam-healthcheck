// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Xunit;
using VeeamHealthCheck.Functions.Collection.PSCollections;

namespace VhcXTests.CrossPlatform;

public class CPowerShellVersionCheckerStatusTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EvaluatePwshVersionStatus_PwshPathMissing_ReturnsNotInstalled(string? pwshPath)
    {
        PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
            pwshPath, installedVersion: null, requiredVersion: Version.Parse("7.6"));

        Assert.Equal(PwshVersionStatus.NotInstalled, status);
    }

    [Fact]
    public void EvaluatePwshVersionStatus_PwshPathMissingAndRequiredVersionAlsoNull_ReturnsNotInstalled()
    {
        // Regression case for issue #135: an unreadable module manifest (requiredVersion null)
        // must never mask a completely missing PowerShell 7 install.
        PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
            pwshPath: null, installedVersion: null, requiredVersion: null);

        Assert.Equal(PwshVersionStatus.NotInstalled, status);
    }

    [Fact]
    public void EvaluatePwshVersionStatus_PwshFoundButRequiredVersionUnknown_ReturnsVersionInconclusive()
    {
        PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
            pwshPath: @"C:\Program Files\PowerShell\7\pwsh.exe",
            installedVersion: Version.Parse("7.6.0"),
            requiredVersion: null);

        Assert.Equal(PwshVersionStatus.VersionInconclusive, status);
    }

    [Fact]
    public void EvaluatePwshVersionStatus_PwshFoundButInstalledVersionUndetermined_ReturnsVersionInconclusive()
    {
        PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
            pwshPath: @"C:\Program Files\PowerShell\7\pwsh.exe",
            installedVersion: null,
            requiredVersion: Version.Parse("7.6"));

        Assert.Equal(PwshVersionStatus.VersionInconclusive, status);
    }

    [Fact]
    public void EvaluatePwshVersionStatus_InstalledBelowRequired_ReturnsBelowRequirement()
    {
        PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
            pwshPath: @"C:\Program Files\PowerShell\7\pwsh.exe",
            installedVersion: Version.Parse("7.4.6"),
            requiredVersion: Version.Parse("7.6"));

        Assert.Equal(PwshVersionStatus.BelowRequirement, status);
    }

    [Theory]
    [InlineData("7.6.0")]
    [InlineData("7.6.1")]
    [InlineData("8.0.0")]
    public void EvaluatePwshVersionStatus_InstalledMeetsOrExceedsRequired_ReturnsMeetsRequirement(string installedRaw)
    {
        PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
            pwshPath: @"C:\Program Files\PowerShell\7\pwsh.exe",
            installedVersion: Version.Parse(installedRaw),
            requiredVersion: Version.Parse("7.6"));

        Assert.Equal(PwshVersionStatus.MeetsRequirement, status);
    }

    [Fact]
    public void BuildPwshVersionFailureMessage_NotInstalledWithKnownRequiredVersion_MentionsRequiredVersionAndInstallLink()
    {
        string msg = CPowerShellVersionChecker.BuildPwshVersionFailureMessage(
            PwshVersionStatus.NotInstalled, "13.1.0.1234", Version.Parse("7.6"), rawInstalledVersion: null);

        Assert.Contains("requires PowerShell 7.6 or higher", msg);
        Assert.Contains("no PowerShell 7 installation was found", msg);
        Assert.Contains("https://aka.ms/powershell-release?tag=stable", msg);
        Assert.Contains("VBR 13.1.0.1234", msg);
    }

    [Fact]
    public void BuildPwshVersionFailureMessage_NotInstalledWithUnknownRequiredVersion_UsesGenericPowerShell7Wording()
    {
        string msg = CPowerShellVersionChecker.BuildPwshVersionFailureMessage(
            PwshVersionStatus.NotInstalled, "13.1.0.1234", requiredVersion: null, rawInstalledVersion: null);

        Assert.Contains("requires PowerShell 7,", msg);
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

    [Theory]
    [InlineData(PwshVersionStatus.MeetsRequirement)]
    [InlineData(PwshVersionStatus.VersionInconclusive)]
    public void BuildPwshVersionFailureMessage_NonFailureStatus_ThrowsArgumentOutOfRangeException(PwshVersionStatus status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CPowerShellVersionChecker.BuildPwshVersionFailureMessage(status, "13.1.0.1234", Version.Parse("7.6"), "7.4.6"));
    }
}
