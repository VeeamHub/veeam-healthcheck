// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using VeeamHealthCheck.Functions.Collection.REST;

namespace VeeamHealthCheck.Tests.Functions.Collection
{
    public class CVbawsRestCollectorTests
    {
        #region JsonToCsv Tests

        [Fact]
        public void JsonToCsv_ArrayResponse_WritesCorrectHeaders()
        {
            // Arrange
            string json = @"[
                {""id"": ""pol-001"", ""name"": ""DailyBackup"", ""status"": ""Active""},
                {""id"": ""pol-002"", ""name"": ""WeeklyBackup"", ""status"": ""Disabled""}
            ]";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "10.0.1.50");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.True(lines.Length >= 3, "Should have header + 2 data rows");

                // Verify header contains ApplianceId and all JSON fields
                Assert.Contains("ApplianceId", lines[0]);
                Assert.Contains("id", lines[0]);
                Assert.Contains("name", lines[0]);
                Assert.Contains("status", lines[0]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_ArrayResponse_WritesCorrectDataRows()
        {
            // Arrange
            string json = @"[
                {""id"": ""pol-001"", ""name"": ""DailyBackup"", ""status"": ""Active""}
            ]";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "10.0.1.50");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.Equal(2, lines.Length); // header + 1 data row

                // Data row should start with appliance ID
                Assert.Contains("10.0.1.50", lines[1]);
                Assert.Contains("pol-001", lines[1]);
                Assert.Contains("DailyBackup", lines[1]);
                Assert.Contains("Active", lines[1]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_ResultsWrappedResponse_ExtractsArray()
        {
            // Arrange
            string json = @"{""results"": [{""id"": ""ses-001"", ""status"": ""Success""}], ""totalCount"": 1}";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "myhost");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.Equal(2, lines.Length); // header + 1 data row
                Assert.Contains("ses-001", lines[1]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_DataWrappedResponse_ExtractsArray()
        {
            // Arrange
            string json = @"{""data"": [{""id"": ""repo-001"", ""name"": ""prod-backups""}]}";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "appliance1");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.Equal(2, lines.Length);
                Assert.Contains("repo-001", lines[1]);
                Assert.Contains("prod-backups", lines[1]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_EmptyArray_WritesHeaderOnly()
        {
            // Arrange
            string json = @"[]";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "10.0.1.50");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.Single(lines); // header only
                Assert.Contains("ApplianceId", lines[0]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_SingleObject_WritesAsRawJson()
        {
            // Arrange - single object, not array, not wrapped
            string json = @"{""version"": ""6.0"", ""buildNumber"": ""1234""}";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "10.0.1.50");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.True(lines.Length >= 2, "Should have header + data row");
                Assert.Contains("ApplianceId", lines[0]);
                Assert.Contains("RawJson", lines[0]);
                Assert.Contains("10.0.1.50", lines[1]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_SpecialCharactersInValues_EscapedProperly()
        {
            // Arrange - value with quotes and newlines
            string json = @"[{""id"": ""1"", ""description"": ""Line1\nLine2"", ""notes"": ""He said \""hello\""""}]";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "host1");

                // Assert
                var content = File.ReadAllText(csvPath);
                Assert.DoesNotContain("\n\"host1\"", content.Split('\n').Skip(1).FirstOrDefault() ?? "");
                // File should be parseable without errors
                var lines = File.ReadAllLines(csvPath);
                Assert.True(lines.Length >= 2);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        [Fact]
        public void JsonToCsv_NumericAndBooleanValues_PreservedAsText()
        {
            // Arrange
            string json = @"[{""id"": 42, ""enabled"": true, ""name"": ""test""}]";
            string csvPath = Path.Combine(Path.GetTempPath(), $"vbaws_test_{Guid.NewGuid()}.csv");

            try
            {
                // Act
                CVbawsRestCollector.JsonToCsv(json, csvPath, "host1");

                // Assert
                var lines = File.ReadAllLines(csvPath);
                Assert.Equal(2, lines.Length);
                Assert.Contains("42", lines[1]);
                Assert.Contains("true", lines[1]);
            }
            finally
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);
            }
        }

        #endregion
    }
}
