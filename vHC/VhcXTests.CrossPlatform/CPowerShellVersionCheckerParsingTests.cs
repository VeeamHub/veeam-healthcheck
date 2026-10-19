// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Xunit;
using VeeamHealthCheck.Functions.Collection.PSCollections;

namespace VhcXTests.CrossPlatform;

public class CPowerShellVersionCheckerParsingTests
{
    private static readonly string Esc = ((char)0x1B).ToString();

    [Fact]
    public void TryParsePwshVersionOutput_AnsiWrappedOutput_ParsesVersion()
    {
        string ansiOutput = Esc + "[93m7.6.0" + Esc + "[0m";

        bool success = CPowerShellVersionChecker.TryParsePwshVersionOutput(ansiOutput, out Version? installed, out string? raw);

        Assert.True(success);
        Assert.Equal(Version.Parse("7.6.0"), installed);
        Assert.Equal("7.6.0", raw);
    }

    [Fact]
    public void TryParsePwshVersionOutput_NoiseLineBeforeVersion_ParsesLastLine()
    {
        bool success = CPowerShellVersionChecker.TryParsePwshVersionOutput("Update available: 7.6.1\n7.4.6", out Version? installed, out string? raw);

        Assert.True(success);
        Assert.Equal(Version.Parse("7.4.6"), installed);
        Assert.Equal("7.4.6", raw);
    }

    [Fact]
    public void TryParseManifestContent_CommentedOutEntryPrecedingLiveEntry_ParsesLiveEntry()
    {
        bool success = CPowerShellVersionChecker.TryParseManifestContent(
            "# PowerShellVersion = '5.1'\nPowerShellVersion = '7.6'",
            out Version? required);

        Assert.True(success);
        Assert.Equal(Version.Parse("7.6"), required);
    }

    [Fact]
    public void TryParseManifestContent_EntirelyCommentedOutEntry_ReturnsFalse()
    {
        bool success = CPowerShellVersionChecker.TryParseManifestContent("# PowerShellVersion = '5.1'", out Version? required);

        Assert.False(success);
        Assert.Null(required);
    }
}
