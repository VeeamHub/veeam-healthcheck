// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Threading;
using Xunit;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Tests.Collection
{
    [Trait("Category", "ImportPathResolver")]
    public class CImportPathResolverTests : IDisposable
    {
        private readonly string _testDirectory;

        public CImportPathResolverTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"vhc-import-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            CleanupTestDirectory();
        }

        private void CleanupTestDirectory()
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

        #region FindCsvDirectory Tests

        [Fact]
        public void FindCsvDirectory_ReturnsNull_WhenPathIsNull()
        {
            // Act
            var result = CImportPathResolver.FindCsvDirectory(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindCsvDirectory_ReturnsNull_WhenPathIsEmpty()
        {
            // Act
            var result = CImportPathResolver.FindCsvDirectory(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindCsvDirectory_ReturnsNull_WhenPathDoesNotExist()
        {
            // Arrange
            string nonExistentPath = Path.Combine(_testDirectory, "nonexistent");

            // Act
            var result = CImportPathResolver.FindCsvDirectory(nonExistentPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindCsvDirectory_FindsCsvsInFlatStructure()
        {
            // Arrange - create CSV files directly in test directory
            CreateCriticalVbrCsvFiles(_testDirectory);

            // Act
            var result = CImportPathResolver.FindCsvDirectory(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_testDirectory, result);
        }

        [Fact]
        public void FindCsvDirectory_FindsCsvsInVbrSubdirectory()
        {
            // Arrange - create VBR subdirectory structure
            string vbrDir = Path.Combine(_testDirectory, "VBR");
            Directory.CreateDirectory(vbrDir);
            CreateCriticalVbrCsvFiles(vbrDir);

            // Act
            var result = CImportPathResolver.FindCsvDirectory(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(vbrDir, result);
        }

        [Fact]
        public void FindCsvDirectory_FindsCsvsInOriginalVbrSubdirectory()
        {
            // Arrange - create Original/VBR subdirectory structure
            string originalDir = Path.Combine(_testDirectory, "Original");
            string vbrDir = Path.Combine(originalDir, "VBR");
            Directory.CreateDirectory(vbrDir);
            CreateCriticalVbrCsvFiles(vbrDir);

            // Act
            var result = CImportPathResolver.FindCsvDirectory(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(vbrDir, result);
        }

        [Fact]
        public void FindCsvDirectory_FindsCsvsInServerTimestampStructure()
        {
            // Arrange - create VBR/servername/timestamp structure
            string vbrDir = Path.Combine(_testDirectory, "VBR");
            string serverDir = Path.Combine(vbrDir, "myserver");
            string timestampDir = Path.Combine(serverDir, "20250201_120000");
            Directory.CreateDirectory(timestampDir);
            CreateCriticalVbrCsvFiles(timestampDir);

            // Act
            var result = CImportPathResolver.FindCsvDirectory(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(timestampDir, result);
        }

        [Fact]
        public void FindCsvDirectory_PrefersMostRecentTimestamp()
        {
            // Arrange - create two timestamp directories
            string vbrDir = Path.Combine(_testDirectory, "VBR");
            string serverDir = Path.Combine(vbrDir, "myserver");

            string olderDir = Path.Combine(serverDir, "20250101_120000");
            string newerDir = Path.Combine(serverDir, "20250201_120000");

            Directory.CreateDirectory(olderDir);
            Directory.CreateDirectory(newerDir);

            CreateCriticalVbrCsvFiles(olderDir);
            // Add small delay to ensure newer files have later timestamps
            Thread.Sleep(100);
            CreateCriticalVbrCsvFiles(newerDir);

            // Act
            var result = CImportPathResolver.FindCsvDirectory(_testDirectory);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(newerDir, result);
        }

        [Fact]
        public void FindCsvDirectory_ReturnsNull_WhenNoCriticalFilesFound()
        {
            // Arrange - create directory with non-critical CSV files
            File.WriteAllText(Path.Combine(_testDirectory, "random.csv"), "Header\nValue");
            File.WriteAllText(Path.Combine(_testDirectory, "other.csv"), "Header\nValue");

            // Act
            var result = CImportPathResolver.FindCsvDirectory(_testDirectory);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region ValidateCsvFiles Tests

        [Fact]
        public void ValidateCsvFiles_ReturnsInvalid_WhenPathIsNull()
        {
            // Act
            var result = CImportPathResolver.ValidateCsvFiles(null);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ValidateCsvFiles_ReturnsInvalid_WhenPathDoesNotExist()
        {
            // Act
            var result = CImportPathResolver.ValidateCsvFiles(Path.Combine(_testDirectory, "nonexistent"));

            // Assert
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ValidateCsvFiles_ReturnsValid_WhenAllCriticalFilesPresent()
        {
            // Arrange
            CreateCriticalVbrCsvFiles(_testDirectory);

            // Act
            var result = CImportPathResolver.ValidateCsvFiles(_testDirectory);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("VBR", result.ProductType);
            Assert.Empty(result.MissingCriticalFiles);
            Assert.Equal(5, result.PresentFiles.Count);
        }

        [Fact]
        public void ValidateCsvFiles_ReturnsPartiallyValid_WhenSomeCriticalFilesMissing()
        {
            // Arrange - create only some critical files
            File.WriteAllText(Path.Combine(_testDirectory, "_Jobs.csv"), "Header\nValue");
            File.WriteAllText(Path.Combine(_testDirectory, "Proxies.csv"), "Header\nValue");
            File.WriteAllText(Path.Combine(_testDirectory, "Repositories.csv"), "Header\nValue");

            // Act
            var result = CImportPathResolver.ValidateCsvFiles(_testDirectory);

            // Assert
            Assert.True(result.IsValid); // Still valid with 3 of 5 critical files
            Assert.Equal(2, result.MissingCriticalFiles.Count);
            Assert.Contains("Servers.csv", result.MissingCriticalFiles);
            Assert.Contains("vbrinfo.csv", result.MissingCriticalFiles);
        }

        [Fact]
        public void ValidateCsvFiles_DetectsVb365ProductType()
        {
            // Arrange - create VB365 specific files
            File.WriteAllText(Path.Combine(_testDirectory, "Organizations.csv"), "Header\nValue");
            File.WriteAllText(Path.Combine(_testDirectory, "Proxies.csv"), "Header\nValue");
            File.WriteAllText(Path.Combine(_testDirectory, "Repositories.csv"), "Header\nValue");

            // Act
            var result = CImportPathResolver.ValidateCsvFiles(_testDirectory);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("VB365", result.ProductType);
        }

        [Fact]
        public void ValidateCsvFiles_ListsPresentFiles()
        {
            // Arrange
            CreateCriticalVbrCsvFiles(_testDirectory);

            // Act
            var result = CImportPathResolver.ValidateCsvFiles(_testDirectory);

            // Assert
            Assert.Contains("_Jobs.csv", result.PresentFiles);
            Assert.Contains("Proxies.csv", result.PresentFiles);
            Assert.Contains("Repositories.csv", result.PresentFiles);
            Assert.Contains("Servers.csv", result.PresentFiles);
            Assert.Contains("vbrinfo.csv", result.PresentFiles);
        }

        #endregion

        #region ExtractTimestamps Tests

        [Fact]
        public void ExtractTimestamps_ReturnsDefaults_WhenPathIsNull()
        {
            // Act
            var (earliest, latest) = CImportPathResolver.ExtractTimestamps(null);

            // Assert
            Assert.True(earliest < DateTime.Now);
            Assert.True(latest <= DateTime.Now);
        }

        [Fact]
        public void ExtractTimestamps_ReturnsDefaults_WhenPathDoesNotExist()
        {
            // Act
            var (earliest, latest) = CImportPathResolver.ExtractTimestamps(Path.Combine(_testDirectory, "nonexistent"));

            // Assert
            Assert.True(earliest < DateTime.Now);
            Assert.True(latest <= DateTime.Now);
        }

        [Fact]
        public void ExtractTimestamps_ReturnsValidTimestamps_FromCsvFiles()
        {
            // Arrange
            var beforeCreation = DateTime.Now.AddSeconds(-1);
            CreateCriticalVbrCsvFiles(_testDirectory);
            var afterCreation = DateTime.Now.AddSeconds(1);

            // Act
            var (earliest, latest) = CImportPathResolver.ExtractTimestamps(_testDirectory);

            // Assert
            Assert.True(earliest >= beforeCreation.AddMinutes(-1)); // Allow some tolerance
            Assert.True(latest <= afterCreation.AddMinutes(1));
        }

        [Fact]
        public void ExtractTimestamps_ReturnsDefaults_WhenNoCsvFiles()
        {
            // Arrange - directory exists but no CSV files
            // _testDirectory is already created but empty

            // Act
            var (earliest, latest) = CImportPathResolver.ExtractTimestamps(_testDirectory);

            // Assert - should return defaults (now - 7 days to now)
            var expectedEarliest = DateTime.Now.AddDays(-7);
            Assert.True(Math.Abs((earliest - expectedEarliest).TotalMinutes) < 5);
        }

        #endregion

        #region ImportValidationResult Tests

        [Fact]
        public void ImportValidationResult_DefaultValues()
        {
            // Act
            var result = new ImportValidationResult();

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(result.CsvDirectory);
            Assert.Null(result.ProductType);
            Assert.Null(result.MissingCriticalFiles);
            Assert.Null(result.MissingOptionalFiles);
            Assert.Null(result.PresentFiles);
            Assert.Null(result.ErrorMessage);
        }

        #endregion

        #region Helper Methods

        private void CreateCriticalVbrCsvFiles(string directory)
        {
            File.WriteAllText(Path.Combine(directory, "_Jobs.csv"), "JobName,JobType\nBackup1,VMware");
            File.WriteAllText(Path.Combine(directory, "Proxies.csv"), "ProxyName,Type\nProxy1,VMware");
            File.WriteAllText(Path.Combine(directory, "Repositories.csv"), "RepoName,Type\nRepo1,Windows");
            File.WriteAllText(Path.Combine(directory, "Servers.csv"), "ServerName,Type\nServer1,Managed");
            File.WriteAllText(Path.Combine(directory, "vbrinfo.csv"), "Version,Build\n12.0,12.0.0.1420");
        }

        #endregion
    }
}
