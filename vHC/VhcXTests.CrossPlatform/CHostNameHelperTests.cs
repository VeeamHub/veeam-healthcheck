// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
// Cross-platform tests for CHostNameHelper (Issue #82)

using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests.CrossPlatform;

public class CHostNameHelperTests
{
    #region Localhost Detection Tests

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("LocalHost")]
    [InlineData("Localhost")]
    public void IsLocalHost_Localhost_ReturnsTrue(string hostname)
    {
        // Act
        var result = CHostNameHelper.IsLocalHost(hostname);

        // Assert
        Assert.True(result, $"'{hostname}' should be detected as local host");
    }

    [Fact]
    public void IsLocalHost_LoopbackIPv4_ReturnsTrue()
    {
        // Act
        var result = CHostNameHelper.IsLocalHost("127.0.0.1");

        // Assert
        Assert.True(result, "127.0.0.1 should be detected as local host");
    }

    #endregion

    #region Machine Name Detection Tests

    [Fact]
    public void IsLocalHost_MatchesEnvironmentMachineName_ReturnsTrue()
    {
        // Arrange
        string machineName = Environment.MachineName;

        // Act
        var result = CHostNameHelper.IsLocalHost(machineName);

        // Assert
        Assert.True(result, $"Machine name '{machineName}' should be detected as local host");
    }

    [Fact]
    public void IsLocalHost_MatchesMachineNameLowercase_ReturnsTrue()
    {
        // Arrange
        string machineName = Environment.MachineName.ToLowerInvariant();

        // Act
        var result = CHostNameHelper.IsLocalHost(machineName);

        // Assert
        Assert.True(result, $"Lowercase machine name '{machineName}' should be detected as local host");
    }

    [Fact]
    public void IsLocalHost_MatchesMachineNameUppercase_ReturnsTrue()
    {
        // Arrange
        string machineName = Environment.MachineName.ToUpperInvariant();

        // Act
        var result = CHostNameHelper.IsLocalHost(machineName);

        // Assert
        Assert.True(result, $"Uppercase machine name '{machineName}' should be detected as local host");
    }

    [Fact]
    public void IsLocalHost_MatchesDnsGetHostName_ReturnsTrue()
    {
        // Arrange
        string dnsHostName = System.Net.Dns.GetHostName();

        // Act
        var result = CHostNameHelper.IsLocalHost(dnsHostName);

        // Assert
        Assert.True(result, $"DNS host name '{dnsHostName}' should be detected as local host");
    }

    #endregion

    #region FQDN Detection Tests

    [Fact]
    public void IsLocalHost_FqdnStartingWithMachineName_ReturnsTrue()
    {
        // Arrange
        string machineName = Environment.MachineName;
        string fqdn = $"{machineName}.domain.local";

        // Act
        var result = CHostNameHelper.IsLocalHost(fqdn);

        // Assert
        Assert.True(result, $"FQDN '{fqdn}' starting with machine name should be detected as local host");
    }

    [Fact]
    public void IsLocalHost_FqdnWithMultipleDomainLevels_ReturnsTrue()
    {
        // Arrange
        string machineName = Environment.MachineName;
        string fqdn = $"{machineName}.subdomain.domain.com";

        // Act
        var result = CHostNameHelper.IsLocalHost(fqdn);

        // Assert
        Assert.True(result, $"Multi-level FQDN '{fqdn}' should be detected as local host");
    }

    #endregion

    #region Remote Host Detection Tests

    [Theory]
    [InlineData("remote-server")]
    [InlineData("vbr-prod")]
    [InlineData("192.168.1.100")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    public void IsLocalHost_ActualRemoteHost_ReturnsFalse(string hostname)
    {
        // Act
        var result = CHostNameHelper.IsLocalHost(hostname);

        // Assert
        Assert.False(result, $"'{hostname}' should NOT be detected as local host");
    }

    [Fact]
    public void IsLocalHost_RemoteFqdn_ReturnsFalse()
    {
        // Arrange - ensure it doesn't start with local machine name
        string remoteFqdn = "completely-different-server.domain.com";

        // Act
        var result = CHostNameHelper.IsLocalHost(remoteFqdn);

        // Assert
        Assert.False(result, $"Remote FQDN '{remoteFqdn}' should NOT be detected as local host");
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsLocalHost_NullOrWhitespace_ReturnsFalse(string? hostname)
    {
        // Act
        var result = CHostNameHelper.IsLocalHost(hostname!);

        // Assert
        Assert.False(result, "Null or whitespace hostname should return false");
    }

    [Fact]
    public void IsLocalHost_PartialMachineNameMatch_ReturnsFalse()
    {
        // Arrange - hostname that contains but doesn't equal machine name
        string machineName = Environment.MachineName;
        string partialMatch = machineName + "-backup";

        // Act
        var result = CHostNameHelper.IsLocalHost(partialMatch);

        // Assert
        Assert.False(result, $"Partial match '{partialMatch}' without dot should NOT be detected as local");
    }

    [Fact]
    public void IsLocalHost_MachineNameAsPrefix_ReturnsFalse()
    {
        // Arrange - hostname that starts with machine name but isn't FQDN
        string machineName = Environment.MachineName;
        string prefixMatch = machineName + "2"; // e.g., SERVER1 vs SERVER12

        // Act
        var result = CHostNameHelper.IsLocalHost(prefixMatch);

        // Assert
        Assert.False(result, $"Prefix match '{prefixMatch}' should NOT be detected as local (no dot)");
    }

    #endregion
}
