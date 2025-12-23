// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Linq;
using Xunit;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Functions.Collection;

namespace VeeamHealthCheck.Tests.Collection
{
    [Trait("Category", "CsvValidation")]
    public class CsvValidationResultTests
    {
        [Fact]
        public void Missing_CreatesCorrectResult()
        {
            // Arrange
            string fileName = "TestFile";
            string filePath = "/path/to/TestFile.csv";
            var severity = CsvValidationSeverity.Critical;

            // Act
            var result = CsvValidationResult.Missing(fileName, filePath, severity);

            // Assert
            Assert.Equal(fileName, result.FileName);
            Assert.Equal(filePath, result.FilePath);
            Assert.False(result.IsPresent);
            Assert.Equal(severity, result.Severity);
            Assert.Equal(0, result.RecordCount);
            Assert.Contains("not found", result.Message);
            Assert.True(result.ValidationTime <= DateTime.Now);
        }

        [Fact]
        public void Present_CreatesCorrectResult_WithRecords()
        {
            // Arrange
            string fileName = "TestFile";
            string filePath = "/path/to/TestFile.csv";
            int recordCount = 42;

            // Act
            var result = CsvValidationResult.Present(fileName, filePath, recordCount);

            // Assert
            Assert.Equal(fileName, result.FileName);
            Assert.Equal(filePath, result.FilePath);
            Assert.True(result.IsPresent);
            Assert.Equal(CsvValidationSeverity.Info, result.Severity);
            Assert.Equal(recordCount, result.RecordCount);
            Assert.Contains("42 records", result.Message);
        }

        [Fact]
        public void Present_CreatesCorrectResult_WithZeroRecords()
        {
            // Arrange
            string fileName = "EmptyFile";
            string filePath = "/path/to/EmptyFile.csv";
            int recordCount = 0;

            // Act
            var result = CsvValidationResult.Present(fileName, filePath, recordCount);

            // Assert
            Assert.True(result.IsPresent);
            Assert.Equal(0, result.RecordCount);
            Assert.Contains("no records", result.Message);
        }

        [Fact]
        public void ToString_IncludesAllRelevantInfo()
        {
            // Arrange
            var result = CsvValidationResult.Missing("TestFile", "/path/TestFile.csv", CsvValidationSeverity.Warning);

            // Act
            string toString = result.ToString();

            // Assert
            Assert.Contains("[Warning]", toString);
            Assert.Contains("TestFile", toString);
            Assert.Contains("Missing", toString);
        }

        [Theory]
        [InlineData(CsvValidationSeverity.Critical)]
        [InlineData(CsvValidationSeverity.Warning)]
        [InlineData(CsvValidationSeverity.Info)]
        public void Missing_SeverityIsPreserved(CsvValidationSeverity severity)
        {
            // Act
            var result = CsvValidationResult.Missing("File", "/path/File.csv", severity);

            // Assert
            Assert.Equal(severity, result.Severity);
        }
    }

    [Trait("Category", "CsvValidation")]
    public class CCsvValidatorTests : IDisposable
    {
        private readonly string _testDirectory;

        public CCsvValidatorTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"vhc-csv-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void ValidateSingleFile_ReturnsMissing_WhenFileNotExists()
        {
            // Arrange
            var validator = new CCsvValidator(_testDirectory);

            // Act
            var result = validator.ValidateSingleFile("NonExistent", CsvValidationSeverity.Critical);

            // Assert
            Assert.False(result.IsPresent);
            Assert.Equal(CsvValidationSeverity.Critical, result.Severity);
            Assert.Equal("NonExistent", result.FileName);
        }

        [Fact]
        public void ValidateSingleFile_ReturnsPresent_WhenFileExists()
        {
            // Arrange
            string fileName = "TestPresent";
            string filePath = Path.Combine(_testDirectory, fileName + ".csv");
            File.WriteAllText(filePath, "Header1,Header2\nValue1,Value2\nValue3,Value4");
            var validator = new CCsvValidator(_testDirectory);

            // Act
            var result = validator.ValidateSingleFile(fileName, CsvValidationSeverity.Warning);

            // Assert
            Assert.True(result.IsPresent);
            Assert.Equal(2, result.RecordCount); // 3 lines - 1 header = 2 records
        }

        [Fact]
        public void ValidateSingleFile_CountsRecordsCorrectly_EmptyFile()
        {
            // Arrange
            string fileName = "EmptyData";
            string filePath = Path.Combine(_testDirectory, fileName + ".csv");
            File.WriteAllText(filePath, "Header1,Header2"); // Only header, no data
            var validator = new CCsvValidator(_testDirectory);

            // Act
            var result = validator.ValidateSingleFile(fileName, CsvValidationSeverity.Info);

            // Assert
            Assert.True(result.IsPresent);
            Assert.Equal(0, result.RecordCount);
        }

        [Fact]
        public void GetReportSummary_ReturnsCorrectMessage_AllFilesPresent()
        {
            // Arrange
            var results = new System.Collections.Generic.List<CsvValidationResult>
            {
                CsvValidationResult.Present("File1", "/path/File1.csv", 10),
                CsvValidationResult.Present("File2", "/path/File2.csv", 20),
            };

            // Act
            string summary = CCsvValidator.GetReportSummary(results);

            // Assert
            Assert.Contains("2/2 files loaded successfully", summary);
        }

        [Fact]
        public void GetReportSummary_ReturnsWarning_WhenCriticalMissing()
        {
            // Arrange
            var results = new System.Collections.Generic.List<CsvValidationResult>
            {
                CsvValidationResult.Present("File1", "/path/File1.csv", 10),
                CsvValidationResult.Missing("File2", "/path/File2.csv", CsvValidationSeverity.Critical),
            };

            // Act
            string summary = CCsvValidator.GetReportSummary(results);

            // Assert
            Assert.Contains("1/2 files loaded", summary);
            Assert.Contains("critical", summary.ToLower());
        }

        [Fact]
        public void GetReportSummary_ReturnsOptionalMessage_WhenOnlyOptionalMissing()
        {
            // Arrange
            var results = new System.Collections.Generic.List<CsvValidationResult>
            {
                CsvValidationResult.Present("File1", "/path/File1.csv", 10),
                CsvValidationResult.Missing("File2", "/path/File2.csv", CsvValidationSeverity.Warning),
            };

            // Act
            string summary = CCsvValidator.GetReportSummary(results);

            // Assert
            Assert.Contains("optional", summary.ToLower());
        }

        [Fact]
        public void GetMissingFilesList_ReturnsMissingFilesOnly()
        {
            // Arrange
            var results = new System.Collections.Generic.List<CsvValidationResult>
            {
                CsvValidationResult.Present("PresentFile", "/path/PresentFile.csv", 10),
                CsvValidationResult.Missing("MissingFile1", "/path/MissingFile1.csv", CsvValidationSeverity.Critical),
                CsvValidationResult.Missing("MissingFile2", "/path/MissingFile2.csv", CsvValidationSeverity.Warning),
            };

            // Act
            var missingFiles = CCsvValidator.GetMissingFilesList(results);

            // Assert
            Assert.Equal(2, missingFiles.Count);
            Assert.Contains(missingFiles, f => f.Contains("MissingFile1"));
            Assert.Contains(missingFiles, f => f.Contains("MissingFile2"));
            Assert.DoesNotContain(missingFiles, f => f.Contains("PresentFile"));
        }

        [Fact]
        public void GetMissingFilesList_OrdersBySeverity()
        {
            // Arrange
            var results = new System.Collections.Generic.List<CsvValidationResult>
            {
                CsvValidationResult.Missing("InfoFile", "/path/InfoFile.csv", CsvValidationSeverity.Info),
                CsvValidationResult.Missing("CriticalFile", "/path/CriticalFile.csv", CsvValidationSeverity.Critical),
                CsvValidationResult.Missing("WarningFile", "/path/WarningFile.csv", CsvValidationSeverity.Warning),
            };

            // Act
            var missingFiles = CCsvValidator.GetMissingFilesList(results);

            // Assert
            Assert.Equal(3, missingFiles.Count);
            Assert.Contains("[Critical]", missingFiles[0]);
            Assert.Contains("[Warning]", missingFiles[1]);
            Assert.Contains("[Info]", missingFiles[2]);
        }

        [Fact]
        public void GetReportSummary_HandlesNullInput()
        {
            // Act
            string summary = CCsvValidator.GetReportSummary(null);

            // Assert
            Assert.Contains("No CSV validation data available", summary);
        }

        [Fact]
        public void GetReportSummary_HandlesEmptyList()
        {
            // Act
            string summary = CCsvValidator.GetReportSummary(new System.Collections.Generic.List<CsvValidationResult>());

            // Assert
            Assert.Contains("No CSV validation data available", summary);
        }
    }

    [Trait("Category", "CsvValidation")]
    public class CGlobalsValidationTests
    {
        [Fact]
        public void CsvValidationResults_DefaultsToEmptyList()
        {
            // Assert
            Assert.NotNull(CGlobals.CsvValidationResults);
            Assert.IsType<System.Collections.Generic.List<CsvValidationResult>>(CGlobals.CsvValidationResults);
        }

        [Fact]
        public void CsvValidationResults_CanBeAssigned()
        {
            // Arrange
            var originalResults = CGlobals.CsvValidationResults;
            var newResults = new System.Collections.Generic.List<CsvValidationResult>
            {
                CsvValidationResult.Present("TestFile", "/path/TestFile.csv", 5)
            };

            try
            {
                // Act
                CGlobals.CsvValidationResults = newResults;

                // Assert
                Assert.Same(newResults, CGlobals.CsvValidationResults);
                Assert.Single(CGlobals.CsvValidationResults);
            }
            finally
            {
                // Cleanup - restore original
                CGlobals.CsvValidationResults = originalResults ?? new System.Collections.Generic.List<CsvValidationResult>();
            }
        }
    }
}
