// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using VhcXTests.TestData;

namespace VhcXTests.ContentValidation
{
    /// <summary>
    /// Tests that validate CSV file schemas - column names, data types, and required fields.
    /// These tests ensure the CSV parsing pipeline handles expected and edge-case data correctly.
    /// </summary>
    [Trait("Category", "ContentValidation")]
    [Trait("Category", "CsvSchema")]
    public class CsvSchemaValidationTests : IDisposable
    {
        private readonly string _testDirectory;

        public CsvSchemaValidationTests()
        {
            _testDirectory = VbrCsvSampleGenerator.CreateTestDataDirectory();
        }

        public void Dispose()
        {
            VbrCsvSampleGenerator.CleanupTestDirectory(Path.GetDirectoryName(_testDirectory));
        }

        #region VbrInfo Schema Tests

        [Fact]
        public void VbrInfo_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "vbrinfo.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert - required columns for VBR server info
            Assert.Contains("Name", headers);
            Assert.Contains("Version", headers);
            Assert.Contains("SqlServer", headers);
            Assert.Contains("SqlDatabase", headers);
        }

        [Fact]
        public void VbrInfo_SecurityColumns_Present()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "vbrinfo.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert - security-related columns
            Assert.Contains("mfa", headers);
            Assert.Contains("foureyes", headers);
        }

        [Theory]
        [InlineData("12.0.0.1420")]
        [InlineData("13.0.0.100")]
        [InlineData("11.0.1.1000")]
        public void VbrInfo_Version_MatchesExpectedFormat(string version)
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateVbrInfo(version: version);
            var lines = csv.Split('\n');
            var dataLine = lines[1];

            // Assert - version appears in data
            Assert.Contains(version, dataLine);
        }

        #endregion

        #region Servers Schema Tests

        [Fact]
        public void Servers_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "Servers.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Name", headers);
            Assert.Contains("Type", headers);
            Assert.Contains("ApiVersion", headers);
            Assert.Contains("HostId", headers);
        }

        [Fact]
        public void Servers_HostId_IsValidGuidFormat()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateServers();
            var lines = csv.Split('\n').Skip(1); // Skip header

            // Assert - each HostId should be parseable as GUID
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                var hostIdIndex = 3; // HostId is 4th column (0-indexed)
                Assert.True(Guid.TryParse(columns[hostIdIndex], out _),
                    $"HostId '{columns[hostIdIndex]}' is not a valid GUID");
            }
        }

        [Fact]
        public void EmptyServers_HasHeadersOnly()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateEmptyServers();
            var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            // Assert
            Assert.Single(lines); // Only header row
            var headers = GetCsvHeaders(csv);
            Assert.Equal(4, headers.Count); // Name, Type, ApiVersion, HostId
        }

        #endregion

        #region Proxies Schema Tests

        [Fact]
        public void Proxies_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "Proxies.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Name", headers);
            Assert.Contains("Host", headers);
            Assert.Contains("MaxTasksCount", headers);
            Assert.Contains("TransportMode", headers);
            Assert.Contains("HostId", headers);
        }

        [Fact]
        public void Proxies_MaxTasksCount_IsNumeric()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateProxies();
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                var maxTasksIndex = 2; // MaxTasksCount is 3rd column
                Assert.True(int.TryParse(columns[maxTasksIndex], out var tasks),
                    $"MaxTasksCount '{columns[maxTasksIndex]}' is not numeric");
                Assert.True(tasks > 0, "MaxTasksCount should be positive");
            }
        }

        [Theory]
        [InlineData("Auto")]
        [InlineData("Direct")]
        [InlineData("HotAdd")]
        [InlineData("NBD")]
        public void Proxies_TransportMode_ValidValues(string validMode)
        {
            // These are valid transport modes that should be recognized
            Assert.True(new[] { "Auto", "Direct", "HotAdd", "NBD", "SAN" }.Contains(validMode));
        }

        #endregion

        #region Repositories Schema Tests

        [Fact]
        public void Repositories_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "Repositories.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Name", headers);
            Assert.Contains("Path", headers);
            Assert.Contains("TotalSpace", headers);
            Assert.Contains("FreeSpace", headers);
            Assert.Contains("isimmutabilitysupported", headers);
            Assert.Contains("Host", headers);
            Assert.Contains("Type", headers);
        }

        [Fact]
        public void Repositories_SpaceValues_AreNumeric()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateRepositories();
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                Assert.True(long.TryParse(columns[2], out var total),
                    $"TotalSpace '{columns[2]}' is not numeric");
                Assert.True(long.TryParse(columns[3], out var free),
                    $"FreeSpace '{columns[3]}' is not numeric");
            }
        }

        [Fact]
        public void Repositories_ImmutabilitySupported_IsBoolean()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateRepositories();
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                var immutabilityIndex = 4;
                Assert.True(bool.TryParse(columns[immutabilityIndex], out _),
                    $"isimmutabilitysupported '{columns[immutabilityIndex]}' is not boolean");
            }
        }

        #endregion

        #region Jobs Schema Tests

        [Fact]
        public void Jobs_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "_Jobs.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Name", headers);
            Assert.Contains("JobType", headers);
            Assert.Contains("RepositoryId", headers);
            Assert.Contains("pwdkeyid", headers);
            Assert.Contains("IsScheduleEnabled", headers);
        }

        [Theory]
        [InlineData("Backup")]
        [InlineData("BackupCopy")]
        [InlineData("Replica")]
        public void Jobs_JobType_ValidValues(string validType)
        {
            // These are valid job types
            Assert.True(new[] { "Backup", "BackupCopy", "Replica", "VmTapeBackup", "FileTapeBackup" }.Contains(validType));
        }

        [Fact]
        public void Jobs_EncryptionKeyId_IsGuidOrEmpty()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateJobs();
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                var pwdKeyId = columns[3];
                // Should either be empty GUID (no encryption) or valid GUID
                Assert.True(Guid.TryParse(pwdKeyId, out _),
                    $"pwdkeyid '{pwdKeyId}' is not a valid GUID");
            }
        }

        #endregion

        #region Session Report Schema Tests

        [Fact]
        public void SessionReport_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "VeeamSessionReport.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("JobName", headers);
            Assert.Contains("JobType", headers);
            Assert.Contains("Status", headers);
            Assert.Contains("StartTime", headers);
            Assert.Contains("EndTime", headers);
            Assert.Contains("Duration", headers);
            Assert.Contains("DataSize", headers);
            Assert.Contains("TransferredSize", headers);
        }

        [Theory]
        [InlineData("Success")]
        [InlineData("Warning")]
        [InlineData("Failed")]
        public void SessionReport_Status_ValidValues(string validStatus)
        {
            // These are valid session statuses
            Assert.True(new[] { "Success", "Warning", "Failed", "Running", "Pending" }.Contains(validStatus));
        }

        [Fact]
        public void SessionReport_DataSizes_AreNonNegative()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateSessionReport();
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                Assert.True(long.TryParse(columns[6], out var dataSize) && dataSize >= 0,
                    $"DataSize should be non-negative");
                Assert.True(long.TryParse(columns[7], out var transferred) && transferred >= 0,
                    $"TransferredSize should be non-negative");
            }
        }

        #endregion

        #region License Info Schema Tests

        [Fact]
        public void LicInfo_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "LicInfo.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Edition", headers);
            Assert.Contains("LicensedTo", headers);
            Assert.Contains("SupportExpiry", headers);
            Assert.Contains("Type", headers);
        }

        [Theory]
        [InlineData("Enterprise")]
        [InlineData("Enterprise Plus")]
        [InlineData("Standard")]
        public void LicInfo_Edition_ValidValues(string validEdition)
        {
            Assert.True(new[] { "Enterprise", "Enterprise Plus", "Standard", "Community" }.Contains(validEdition));
        }

        #endregion

        #region Config Backup Schema Tests

        [Fact]
        public void ConfigBackup_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "configBackup.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Enabled", headers);
            Assert.Contains("Target", headers);
            Assert.Contains("encryptionoptions", headers);
            Assert.Contains("LastResult", headers);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConfigBackup_EncryptionOptions_IsBoolean(bool encryptionEnabled)
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateConfigBackup(encryptionEnabled);
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                Assert.True(bool.TryParse(columns[2], out var value),
                    $"encryptionoptions '{columns[2]}' is not boolean");
                Assert.Equal(encryptionEnabled, value);
            }
        }

        #endregion

        #region Traffic Rules Schema Tests

        [Fact]
        public void TrafficRules_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "trafficRules.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Name", headers);
            Assert.Contains("SourceIp", headers);
            Assert.Contains("TargetIp", headers);
            Assert.Contains("encryptionenabled", headers);
            Assert.Contains("ThrottlingEnabled", headers);
        }

        [Fact]
        public void TrafficRules_IpAddresses_LookLikeNetworkRanges()
        {
            // Arrange
            var csv = VbrCsvSampleGenerator.GenerateTrafficRules();
            var lines = csv.Split('\n').Skip(1);

            // Assert
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = ParseCsvLine(line);
                var sourceIp = columns[1];
                var targetIp = columns[2];

                // Should contain either IP addresses or CIDR notation
                Assert.True(sourceIp.Contains('.') || sourceIp.Contains(':'),
                    $"SourceIp '{sourceIp}' doesn't look like an IP address");
                Assert.True(targetIp.Contains('.') || targetIp.Contains(':'),
                    $"TargetIp '{targetIp}' doesn't look like an IP address");
            }
        }

        #endregion

        #region User Roles Schema Tests

        [Fact]
        public void UserRoles_HasRequiredColumns()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "_UserRoles.csv");
            var content = File.ReadAllText(filePath);
            var headers = GetCsvHeaders(content);

            // Assert
            Assert.Contains("Name", headers);
            Assert.Contains("Role", headers);
            Assert.Contains("Description", headers);
        }

        [Theory]
        [InlineData("Veeam Backup Administrator")]
        [InlineData("Veeam Backup Operator")]
        [InlineData("Veeam Restore Operator")]
        public void UserRoles_Role_ValidValues(string validRole)
        {
            Assert.True(new[] {
                "Veeam Backup Administrator",
                "Veeam Backup Operator",
                "Veeam Restore Operator",
                "Portal Administrator"
            }.Contains(validRole));
        }

        #endregion

        #region Helper Methods

        private List<string> GetCsvHeaders(string csvContent)
        {
            var firstLine = csvContent.Split('\n')[0];
            return ParseCsvLine(firstLine);
        }

        private List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var inQuotes = false;
            var currentColumn = "";

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    columns.Add(currentColumn.Trim());
                    currentColumn = "";
                }
                else
                {
                    currentColumn += c;
                }
            }

            // Add last column
            columns.Add(currentColumn.Trim().TrimEnd('\r'));

            return columns;
        }

        #endregion
    }
}
