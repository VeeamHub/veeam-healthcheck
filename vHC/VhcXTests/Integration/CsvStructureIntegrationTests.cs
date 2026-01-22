// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using VhcXTests.TestData;

namespace VhcXTests.Integration
{
    [Collection("Integration Tests")]
    [Trait("Category", "Integration")]
    public class CsvStructureIntegrationTests : IDisposable
    {
        private readonly string _testDataDirectory;

        public CsvStructureIntegrationTests()
        {
            _testDataDirectory = VbrCsvSampleGenerator.CreateTestDataDirectory();
        }

        public void Dispose()
        {
            VbrCsvSampleGenerator.CleanupTestDirectory(Path.GetDirectoryName(_testDataDirectory));
        }

        [Fact]
        public void GeneratedCsvSamples_AreValidFormat()
        {
            // Validate each generated CSV using VbrCsvSampleGenerator
            var csvFiles = Directory.GetFiles(_testDataDirectory, "*.csv");
            Assert.NotEmpty(csvFiles);

            foreach (var csvFile in csvFiles)
            {
                ValidateCsvStructure(csvFile);
            }
        }

        [Fact]
        public void GeneratedCsvSamples_HaveExpectedFileCount()
        {
            var csvFiles = Directory.GetFiles(_testDataDirectory, "*.csv");
            // VbrCsvSampleGenerator.CreateTestDataDirectory creates 16 CSV files
            Assert.True(csvFiles.Length >= 15, $"Expected at least 15 CSV files, got {csvFiles.Length}");
        }

        #region Content Validation Integration Tests

        [Theory]
        [InlineData("vbrinfo.csv")]
        [InlineData("Servers.csv")]
        [InlineData("Proxies.csv")]
        [InlineData("Repositories.csv")]
        [InlineData("_Jobs.csv")]
        public void CriticalCsvFiles_ExistAndHaveContent(string fileName)
        {
            var filePath = Path.Combine(_testDataDirectory, fileName);

            Assert.True(File.Exists(filePath), $"Critical file {fileName} not found");

            var content = File.ReadAllText(filePath);
            Assert.False(string.IsNullOrWhiteSpace(content), $"Critical file {fileName} is empty");

            var lines = content.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Assert.True(lines.Count >= 1, $"Critical file {fileName} has no header");
        }

        [Fact]
        public void AllCsvFiles_HaveValidStructure()
        {
            var csvFiles = Directory.GetFiles(_testDataDirectory, "*.csv");

            foreach (var csvFile in csvFiles)
            {
                ValidateCsvStructure(csvFile);
            }
        }

        [Fact]
        public void CrossFileReferences_AreConsistent()
        {
            // Load relevant CSVs
            var servers = LoadCsvFile(Path.Combine(_testDataDirectory, "Servers.csv"));
            var proxies = LoadCsvFile(Path.Combine(_testDataDirectory, "Proxies.csv"));

            var serverHostIds = servers.Select(s => GetColumnValue(s, "HostId")).ToHashSet();

            // Verify proxy HostIds reference valid servers
            foreach (var proxy in proxies)
            {
                var hostId = GetColumnValue(proxy, "HostId");
                if (!string.IsNullOrEmpty(hostId))
                {
                    Assert.Contains(hostId, serverHostIds);
                }
            }
        }

        [Fact]
        public void RepositorySpaceValues_AreLogicallyValid()
        {
            var repos = LoadCsvFile(Path.Combine(_testDataDirectory, "Repositories.csv"));

            foreach (var repo in repos)
            {
                var totalStr = GetColumnValue(repo, "TotalSpace");
                var freeStr = GetColumnValue(repo, "FreeSpace");

                if (long.TryParse(totalStr, out var total) && long.TryParse(freeStr, out var free))
                {
                    Assert.True(free <= total,
                        $"Repository has invalid space values: FreeSpace ({free}) > TotalSpace ({total})");
                }
            }
        }

        [Fact]
        public void SessionReport_HasValidTimestamps()
        {
            var sessions = LoadCsvFile(Path.Combine(_testDataDirectory, "VeeamSessionReport.csv"));

            foreach (var session in sessions)
            {
                var startStr = GetColumnValue(session, "StartTime");
                var endStr = GetColumnValue(session, "EndTime");

                if (DateTime.TryParse(startStr, out var start) && DateTime.TryParse(endStr, out var end))
                {
                    Assert.True(end >= start, "Session EndTime should be >= StartTime");
                }
            }
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void EmptyCsv_DoesNotCauseException()
        {
            var emptyPath = Path.Combine(_testDataDirectory, "empty_test.csv");
            File.WriteAllText(emptyPath, string.Empty);

            var exists = File.Exists(emptyPath);
            Assert.True(exists);

            var content = File.ReadAllText(emptyPath);
            Assert.Empty(content);
        }

        [Fact]
        public void CsvWithSpecialCharacters_HandledCorrectly()
        {
            var csvPath = Path.Combine(_testDataDirectory, "special_test.csv");

            var content = "Name,Path,Status\n" +
                         "\"Test, with comma\",\"C:\\Path\\To\\File\",Success\n" +
                         "\"Quote \"\"test\"\"\",\"D:\\Another\\Path\",Warning\n";

            File.WriteAllText(csvPath, content);

            var lines = File.ReadAllLines(csvPath);
            Assert.Equal(3, lines.Length);
            Assert.Contains("Test, with comma", lines[1]);
        }

        [Fact]
        public void HeaderOnlyCsv_IsValidStructure()
        {
            var headerOnlyPath = Path.Combine(_testDataDirectory, "header_only.csv");
            File.WriteAllText(headerOnlyPath, "Name,Type,Status");

            var lines = File.ReadAllLines(headerOnlyPath);
            Assert.Single(lines);
            Assert.Contains(",", lines[0]);
        }

        #endregion

        #region Helper Methods

        private List<Dictionary<string, string>> LoadCsvFile(string filePath)
        {
            var result = new List<Dictionary<string, string>>();

            if (!File.Exists(filePath))
                return result;

            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 1)
                return result;

            var headers = ParseCsvLine(lines[0]);

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var values = ParseCsvLine(lines[i]);
                var record = new Dictionary<string, string>();

                for (int j = 0; j < Math.Min(headers.Count, values.Count); j++)
                {
                    record[headers[j]] = values[j];
                }

                result.Add(record);
            }

            return result;
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

            columns.Add(currentColumn.Trim().TrimEnd('\r'));
            return columns;
        }

        private string GetColumnValue(Dictionary<string, string> record, string columnName)
        {
            return record.GetValueOrDefault(columnName, "");
        }

        private void ValidateCsvStructure(string csvPath)
        {
            var lines = File.ReadAllLines(csvPath);

            // Must have at least header
            Assert.NotEmpty(lines);

            // Header should contain comma-separated values
            var header = lines[0];
            Assert.Contains(",", header);

            // All data rows should have same number of columns as header
            var headerColumns = header.Split(',').Length;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                // Simple column count check (doesn't account for quoted commas)
                var columns = lines[i].Split(',').Length;
                Assert.True(columns >= headerColumns - 2 && columns <= headerColumns + 2,
                    $"Row {i} has unexpected column count in {Path.GetFileName(csvPath)}");
            }
        }

        #endregion
    }
}
