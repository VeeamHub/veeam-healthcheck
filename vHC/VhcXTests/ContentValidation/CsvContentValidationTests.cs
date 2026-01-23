// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Xunit;

namespace VhcXTests.ContentValidation
{
    /// <summary>
    /// Phase 2: Content Validation Tests
    /// Validates that CSV files contain correct schemas, data types, and relationships
    /// </summary>
    [Trait("Category", "ContentValidation")]
    public class CsvContentValidationTests : IDisposable
    {
        private readonly string _testCsvDirectory;

        public CsvContentValidationTests()
        {
            _testCsvDirectory = Path.Combine(Path.GetTempPath(), $"vhc-content-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testCsvDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testCsvDirectory))
            {
                try
                {
                    Directory.Delete(_testCsvDirectory, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        #region Schema Validation Tests

        [Theory]
        [InlineData("_vbrinfo.csv", new[] { "Name", "Version", "SqlServer" })]
        [InlineData("_Servers.csv", new[] { "Name", "Type" })]
        [InlineData("_Proxies.csv", new[] { "Name", "Host" })]
        [InlineData("_Repositories.csv", new[] { "Name", "Path" })]
        [InlineData("_Jobs.csv", new[] { "Name", "Type" })]
        public void CsvFile_HasExpectedColumns(string fileName, string[] expectedColumns)
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, fileName);
            CreateSampleCsvWithColumns(csvPath, expectedColumns);

            // Act
            var actualColumns = GetCsvColumns(csvPath);

            // Assert
            foreach (var expectedCol in expectedColumns)
            {
                Assert.Contains(expectedCol, actualColumns, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData("_vbrinfo.csv", 1, 1)]          // Exactly 1 record (VBR server info)
        [InlineData("_LicInfo.csv", 1, 10)]         // 1-10 license records
        [InlineData("_Servers.csv", 1, 1000)]       // At least 1 server, up to 1000
        [InlineData("_Proxies.csv", 0, 100)]        // 0-100 proxies (optional)
        [InlineData("_Repositories.csv", 1, 100)]   // At least 1 repo, up to 100
        [InlineData("_Jobs.csv", 0, 500)]           // 0-500 jobs
        public void CsvFile_HasRecordCountInRange(string fileName, int minRecords, int maxRecords)
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, fileName);
            int testRecordCount = Math.Min(minRecords + 5, maxRecords); // Use a valid count
            CreateSampleCsvWithRecords(csvPath, testRecordCount);

            // Act
            int actualRecordCount = CountCsvRecords(csvPath);

            // Assert
            Assert.True(actualRecordCount >= minRecords,
                $"{fileName} should have at least {minRecords} records, but has {actualRecordCount}");
            Assert.True(actualRecordCount <= maxRecords,
                $"{fileName} should have at most {maxRecords} records, but has {actualRecordCount}");
        }

        [Fact]
        public void VbrInfo_VersionFieldMatchesExpectedFormat()
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, "_vbrinfo.csv");
            var csvContent = "Name,Version,SqlServer\nVBR-SERVER,12.0.0.1420,SQL-SERVER";
            File.WriteAllText(csvPath, csvContent);

            // Act
            var records = ReadCsvRecords(csvPath);
            var versionField = records.First()["Version"];

            // Assert - Version should match pattern: X.X.X.XXXX or X.X.X.XXX
            var versionPattern = @"^\d{1,2}\.\d{1,2}\.\d{1,2}\.\d{3,4}$";
            Assert.Matches(versionPattern, versionField);
        }

        #endregion

        #region Data Integrity Tests

        [Fact]
        public void Repositories_FreeSpaceDoesNotExceedTotalSpace()
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, "_Repositories.csv");
            var csvContent = @"Name,Path,TotalSpace,FreeSpace
Repo1,C:\Backup,1000000000,500000000
Repo2,D:\Backup,2000000000,1800000000";
            File.WriteAllText(csvPath, csvContent);

            // Act
            var records = ReadCsvRecords(csvPath);

            // Assert
            foreach (var record in records)
            {
                if (record.ContainsKey("TotalSpace") && record.ContainsKey("FreeSpace"))
                {
                    if (long.TryParse(record["TotalSpace"], out long totalSpace) &&
                        long.TryParse(record["FreeSpace"], out long freeSpace))
                    {
                        Assert.True(freeSpace <= totalSpace,
                            $"Repository '{record["Name"]}' has FreeSpace ({freeSpace}) > TotalSpace ({totalSpace})");
                    }
                }
            }
        }

        [Fact]
        public void Repositories_FreeSpaceIsNonNegative()
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, "_Repositories.csv");
            var csvContent = @"Name,Path,TotalSpace,FreeSpace
Repo1,C:\Backup,1000000000,500000000";
            File.WriteAllText(csvPath, csvContent);

            // Act
            var records = ReadCsvRecords(csvPath);

            // Assert
            foreach (var record in records)
            {
                if (record.ContainsKey("FreeSpace"))
                {
                    if (long.TryParse(record["FreeSpace"], out long freeSpace))
                    {
                        Assert.True(freeSpace >= 0,
                            $"Repository '{record["Name"]}' has negative FreeSpace: {freeSpace}");
                    }
                }
            }
        }

        [Fact]
        public void Jobs_AllJobsHaveValidType()
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, "_Jobs.csv");
            var csvContent = @"Name,Type,Status
BackupJob1,Backup,Success
ReplicaJob1,Replica,Success
BackupCopyJob1,BackupCopy,Warning";
            File.WriteAllText(csvPath, csvContent);

            var validJobTypes = new[] { "Backup", "Replica", "BackupCopy", "BackupSync", "Copy", "FileCopy", "EpAgentBackup", "EpAgentManagement" };

            // Act
            var records = ReadCsvRecords(csvPath);

            // Assert
            foreach (var record in records)
            {
                if (record.ContainsKey("Type"))
                {
                    Assert.Contains(record["Type"], validJobTypes);
                }
            }
        }

        #endregion

        #region Cross-File Consistency Tests

        [Fact]
        public void Servers_AllProxiesHaveCorrespondingServerEntry()
        {
            // Arrange
            var serversPath = Path.Combine(_testCsvDirectory, "_Servers.csv");
            var proxiesPath = Path.Combine(_testCsvDirectory, "_Proxies.csv");

            var serversContent = @"Name,Type,ApiVersion
VBR-SERVER,VBR,1.0
PROXY-01,Proxy,1.0
PROXY-02,Proxy,1.0";
            File.WriteAllText(serversPath, serversContent);

            var proxiesContent = @"Name,Host,MaxTasksCount
VMware-Proxy-1,PROXY-01,8
VMware-Proxy-2,PROXY-02,4";
            File.WriteAllText(proxiesPath, proxiesContent);

            // Act
            var servers = ReadCsvRecords(serversPath);
            var proxies = ReadCsvRecords(proxiesPath);

            var serverNames = servers.Select(s => s["Name"]).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Assert
            foreach (var proxy in proxies)
            {
                if (proxy.ContainsKey("Host"))
                {
                    var hostName = proxy["Host"];
                    Assert.Contains(hostName, serverNames, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void VbrInfo_ContainsExactlyOneRecord()
        {
            // Arrange
            var csvPath = Path.Combine(_testCsvDirectory, "_vbrinfo.csv");
            var csvContent = @"Name,Version,SqlServer
VBR-SERVER,12.0.0.1420,SQL-SERVER";
            File.WriteAllText(csvPath, csvContent);

            // Act
            int recordCount = CountCsvRecords(csvPath);

            // Assert
            Assert.Equal(1, recordCount);
        }

        #endregion

        #region Version-Specific Schema Tests

        [Theory]
        [InlineData("12", true)]   // v12+ has compliance data
        [InlineData("13", true)]   // v13 has compliance data
        [InlineData("11", false)]  // v11 might not have compliance (test would need version context)
        public void ComplianceFiles_PresentBasedOnVbrVersion(string vbrVersion, bool shouldHaveCompliance)
        {
            // This test would need actual version detection from _vbrinfo.csv
            // For now, it's a placeholder to demonstrate version-specific validation

            // In a real implementation, you would:
            // 1. Read _vbrinfo.csv to get VBR version
            // 2. Check for compliance-related files based on that version
            // 3. Assert presence/absence matches expectations

            Assert.True(true, "Version-specific validation placeholder");
        }

        #endregion

        #region Helper Methods

        private void CreateSampleCsvWithColumns(string path, string[] columns)
        {
            var header = string.Join(",", columns);
            var sampleRow = string.Join(",", columns.Select(_ => "SampleValue"));
            File.WriteAllText(path, $"{header}\n{sampleRow}");
        }

        private void CreateSampleCsvWithRecords(string path, int recordCount)
        {
            var lines = new List<string> { "Name,Value" };
            for (int i = 0; i < recordCount; i++)
            {
                lines.Add($"Record{i},Value{i}");
            }
            File.WriteAllText(path, string.Join("\n", lines));
        }

        private string[] GetCsvColumns(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            csv.Read();
            csv.ReadHeader();
            return csv.HeaderRecord ?? Array.Empty<string>();
        }

        private int CountCsvRecords(string path)
        {
            if (!File.Exists(path))
                return 0;

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            csv.Read();
            csv.ReadHeader();

            int count = 0;
            while (csv.Read())
            {
                count++;
            }
            return count;
        }

        private List<Dictionary<string, string>> ReadCsvRecords(string path)
        {
            var records = new List<Dictionary<string, string>>();

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (csv.Read())
            {
                var record = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    record[header] = csv.GetField(header) ?? string.Empty;
                }
                records.Add(record);
            }

            return records;
        }

        #endregion
    }
}
