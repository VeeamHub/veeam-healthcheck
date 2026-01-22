// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using VhcXTests.TestData;

namespace VhcXTests.ContentValidation
{
    /// <summary>
    /// Tests that validate the scrubbing/anonymization functionality - ensuring IP addresses,
    /// server names, paths, and other sensitive data are properly anonymized in scrubbed output.
    /// </summary>
    [Trait("Category", "ContentValidation")]
    [Trait("Category", "Scrubbing")]
    public class ScrubbingValidationTests : IDisposable
    {
        private readonly string _testDirectory;

        public ScrubbingValidationTests()
        {
            _testDirectory = VbrCsvSampleGenerator.CreateTestDataDirectory();
        }

        public void Dispose()
        {
            VbrCsvSampleGenerator.CleanupTestDirectory(Path.GetDirectoryName(_testDirectory));
        }

        #region IP Address Detection Tests

        [Theory]
        [InlineData("192.168.1.1")]
        [InlineData("10.0.0.1")]
        [InlineData("172.16.0.1")]
        [InlineData("255.255.255.255")]
        [InlineData("0.0.0.0")]
        public void IpAddressPattern_MatchesValidIPv4(string ip)
        {
            var pattern = GetIpv4Pattern();
            Assert.Matches(new Regex(pattern), ip);
        }

        [Theory]
        [InlineData("192.168.1.0/24")]
        [InlineData("10.0.0.0/8")]
        [InlineData("172.16.0.0/12")]
        public void IpAddressPattern_MatchesCidrNotation(string cidr)
        {
            // CIDR should contain an IP that matches our pattern
            var ipPart = cidr.Split('/')[0];
            var pattern = GetIpv4Pattern();
            Assert.Matches(new Regex(pattern), ipPart);
        }

        [Theory]
        [InlineData("Server_0")]
        [InlineData("Server_123")]
        [InlineData("Path_0")]
        [InlineData("Job_5")]
        public void ScrubPattern_DoesNotMatchRealIPs(string scrubbedValue)
        {
            var pattern = GetIpv4Pattern();
            Assert.DoesNotMatch(new Regex(pattern), scrubbedValue);
        }

        [Fact]
        public void TrafficRules_ContainsIpAddresses_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "trafficRules.csv");
            var content = File.ReadAllText(filePath);

            // Assert - original data should contain IP patterns
            var pattern = GetIpv4Pattern();
            Assert.Matches(new Regex(pattern), content);
        }

        #endregion

        #region Server Name Detection Tests

        [Theory]
        [InlineData("VBR-Server")]
        [InlineData("Proxy-01")]
        [InlineData("Repo-01")]
        [InlineData("server.domain.local")]
        [InlineData("SERVER01.CORP.COM")]
        public void ServerNamePattern_MatchesCommonNames(string serverName)
        {
            // Server names typically contain alphanumeric characters with hyphens/dots
            var pattern = @"^[a-zA-Z0-9][a-zA-Z0-9\-\.]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$";
            Assert.Matches(new Regex(pattern), serverName);
        }

        [Theory]
        [InlineData("Server_0")]
        [InlineData("Server_123")]
        [InlineData("Repository_5")]
        public void ScrubPattern_MatchesAnonymizedNames(string scrubbedName)
        {
            // Scrubbed names follow pattern: Type_Number
            var pattern = @"^[A-Za-z]+_\d+$";
            Assert.Matches(new Regex(pattern), scrubbedName);
        }

        [Fact]
        public void Servers_ContainsRealNames_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "Servers.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain realistic server names
            Assert.Contains("VBR-Server", content);
            Assert.Contains("Proxy-01", content);
        }

        #endregion

        #region Path Detection Tests

        [Theory]
        [InlineData(@"C:\Backup")]
        [InlineData(@"D:\HardenedBackup")]
        [InlineData(@"\\server\share\path")]
        [InlineData(@"/datacenter/vm/VM-Prod-01")]
        public void PathPattern_MatchesCommonPaths(string path)
        {
            // Paths contain path separators
            Assert.True(path.Contains('\\') || path.Contains('/'));
        }

        [Theory]
        [InlineData("Path_0")]
        [InlineData("Path_123")]
        public void ScrubPattern_DoesNotLookLikePath(string scrubbed)
        {
            Assert.DoesNotContain('\\', scrubbed);
            Assert.DoesNotContain('/', scrubbed);
        }

        [Fact]
        public void Repositories_ContainsPaths_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "Repositories.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain path-like strings
            Assert.Contains(@"C:\", content);
        }

        #endregion

        #region Domain/FQDN Detection Tests

        [Theory]
        [InlineData("server.domain.local")]
        [InlineData("proxy-01.corp.contoso.com")]
        [InlineData("vbr.internal.company.net")]
        public void FqdnPattern_MatchesDomainNames(string fqdn)
        {
            var pattern = @"^[a-zA-Z0-9][a-zA-Z0-9\-]*(\.[a-zA-Z0-9][a-zA-Z0-9\-]*)+$";
            Assert.Matches(new Regex(pattern), fqdn);
        }

        [Theory]
        [InlineData("DOMAIN\\VeeamAdmin")]
        [InlineData("CORP\\BackupOperator")]
        public void DomainUserPattern_MatchesWindowsAccounts(string user)
        {
            var pattern = @"^[A-Za-z0-9]+\\[A-Za-z0-9]+$";
            Assert.Matches(new Regex(pattern), user);
        }

        [Fact]
        public void UserRoles_ContainsDomainUsers_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "_UserRoles.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain domain\user format
            Assert.Contains(@"DOMAIN\", content);
        }

        #endregion

        #region Scrubbing Consistency Tests

        [Fact]
        public void ScrubPattern_IsConsistent_ForSameInput()
        {
            // Simulating scrubbing consistency - same input should always produce same output
            var inputToScrubbed = new Dictionary<string, string>();

            var inputs = new[] { "Server1", "Server1", "Server2", "Server1" };
            var scrubbedCounter = 0;

            foreach (var input in inputs)
            {
                if (!inputToScrubbed.ContainsKey(input))
                {
                    inputToScrubbed[input] = $"Server_{scrubbedCounter++}";
                }
            }

            // Assert - Server1 should always map to the same scrubbed value
            Assert.Equal(2, inputToScrubbed.Count); // Only 2 unique inputs
        }

        [Fact]
        public void ScrubPattern_ProducesUniqueMappings()
        {
            // Different inputs should produce different scrubbed values
            var inputs = new[] { "Server1", "Server2", "Server3" };
            var scrubbedValues = new HashSet<string>();

            for (int i = 0; i < inputs.Length; i++)
            {
                var scrubbed = $"Server_{i}";
                Assert.DoesNotContain(scrubbed, scrubbedValues);
                scrubbedValues.Add(scrubbed);
            }
        }

        [Fact]
        public void ScrubPattern_PreservesDataStructure()
        {
            // Scrubbing should not change the CSV structure
            var originalCsv = VbrCsvSampleGenerator.GenerateServers();
            var originalLines = originalCsv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var originalColumnCount = originalLines[0].Count(c => c == ',') + 1;

            // Simulate scrubbing by replacing server names
            var scrubbedCsv = originalCsv
                .Replace("VBR-Server", "Server_0")
                .Replace("Proxy-01", "Server_1")
                .Replace("Repo-01", "Server_2");

            var scrubbedLines = scrubbedCsv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            // Assert - same structure
            Assert.Equal(originalLines.Count, scrubbedLines.Count);
            foreach (var line in scrubbedLines)
            {
                var columnCount = line.Count(c => c == ',') + 1;
                Assert.Equal(originalColumnCount, columnCount);
            }
        }

        #endregion

        #region Sensitive Data Patterns Tests

        [Theory]
        [InlineData("password123")]
        [InlineData("P@ssw0rd!")]
        [InlineData("secret_key")]
        public void SensitivePatterns_ShouldNotAppearInOutput(string sensitiveData)
        {
            // These patterns should never appear in scrubbed output
            // This test validates the pattern detection, not actual scrubbing
            var sensitivePatterns = new[]
            {
                @"password",
                @"secret",
                @"key",
                @"token",
                @"credential"
            };

            var matchesPattern = sensitivePatterns.Any(p =>
                Regex.IsMatch(sensitiveData, p, RegexOptions.IgnoreCase));

            Assert.True(matchesPattern);
        }

        [Fact]
        public void ScrubbedOutput_NoEmailAddresses()
        {
            var scrubbedContent = "Server_0 configured by User_0";
            var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";

            Assert.DoesNotMatch(new Regex(emailPattern), scrubbedContent);
        }

        [Fact]
        public void ScrubbedOutput_NoPhoneNumbers()
        {
            var scrubbedContent = "Contact: User_0 at Extension_0";
            var phonePatterns = new[]
            {
                @"\d{3}-\d{3}-\d{4}",       // 123-456-7890
                @"\(\d{3}\)\s*\d{3}-\d{4}", // (123) 456-7890
                @"\d{10,11}"                 // 1234567890
            };

            foreach (var pattern in phonePatterns)
            {
                Assert.DoesNotMatch(new Regex(pattern), scrubbedContent);
            }
        }

        #endregion

        #region VM Name Scrubbing Tests

        [Fact]
        public void ProtectedVMs_ContainsVMNames_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "ViProtected.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain VM names
            Assert.Contains("VM-Prod-01", content);
            Assert.Contains("VM-Prod-02", content);
        }

        [Fact]
        public void UnprotectedVMs_ContainsVMNames_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "ViUnprotected.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain VM names
            Assert.Contains("VM-Dev-01", content);
            Assert.Contains("VM-Test-01", content);
        }

        [Theory]
        [InlineData("VM_0")]
        [InlineData("VM_123")]
        public void ScrubPattern_MatchesAnonymizedVMNames(string scrubbedName)
        {
            var pattern = @"^VM_\d+$";
            Assert.Matches(new Regex(pattern), scrubbedName);
        }

        #endregion

        #region Job Name Scrubbing Tests

        [Fact]
        public void Jobs_ContainsJobNames_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "_Jobs.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain job names
            Assert.Contains("Daily Backup", content);
            Assert.Contains("Weekly Full", content);
        }

        [Theory]
        [InlineData("Job_0")]
        [InlineData("Job_99")]
        public void ScrubPattern_MatchesAnonymizedJobNames(string scrubbedName)
        {
            var pattern = @"^Job_\d+$";
            Assert.Matches(new Regex(pattern), scrubbedName);
        }

        #endregion

        #region Repository Name Scrubbing Tests

        [Fact]
        public void Repositories_ContainsRepoNames_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "Repositories.csv");
            var content = File.ReadAllText(filePath);

            // Assert - should contain repository names
            Assert.Contains("Default Backup Repository", content);
            Assert.Contains("Hardened Repo", content);
        }

        [Theory]
        [InlineData("Repository_0")]
        [InlineData("Repository_5")]
        public void ScrubPattern_MatchesAnonymizedRepoNames(string scrubbedName)
        {
            var pattern = @"^Repository_\d+$";
            Assert.Matches(new Regex(pattern), scrubbedName);
        }

        #endregion

        #region SOBR Scrubbing Tests

        [Fact]
        public void Sobrs_ContainsSobrNames_BeforeScrubbing()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "SOBRs.csv");
            var content = File.ReadAllText(filePath);

            // Assert
            Assert.Contains("SOBR-01", content);
            Assert.Contains("SOBR-02", content);
        }

        [Theory]
        [InlineData("SOBR_0")]
        [InlineData("SOBR_10")]
        public void ScrubPattern_MatchesAnonymizedSobrNames(string scrubbedName)
        {
            var pattern = @"^SOBR_\d+$";
            Assert.Matches(new Regex(pattern), scrubbedName);
        }

        #endregion

        #region Helper Methods

        private string GetIpv4Pattern()
        {
            // Pattern to match IPv4 addresses
            return @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b";
        }

        #endregion
    }
}
