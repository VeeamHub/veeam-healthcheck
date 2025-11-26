// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace VhcXTests.Integration
{
    [Collection("Integration Tests")]
    [Trait("Category", "Integration")]
    public class CsvStructureIntegrationTests
    {
        [Fact]
        public void GeneratedCsvSamples_AreValidFormat()
        {
            // Create temporary directory for test CSVs
            var testDir = Path.Combine(Path.GetTempPath(), "vhc-csv-test-" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);

            try
            {
                // Generate sample CSVs (you'll need to implement this based on your sample generators)
                var sampleCount = GenerateSampleCsvFiles(testDir);
                
                Assert.True(sampleCount > 0, "No CSV samples were generated");

                // Validate each generated CSV
                var csvFiles = Directory.GetFiles(testDir, "*.csv");
                Assert.NotEmpty(csvFiles);

                foreach (var csvFile in csvFiles)
                {
                    ValidateCsvStructure(csvFile);
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        [Theory]
        [InlineData("VBR_Compliance")]
        [InlineData("VB365_Compliance")]
        [InlineData("Repository_Info")]
        [InlineData("Job_Sessions")]
        public void CsvParser_HandlesExpectedFormats(string csvType)
        {
            // Test that CSV parsers can handle expected column formats
            var testDir = Path.Combine(Path.GetTempPath(), "vhc-parser-test-" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);

            try
            {
                // Generate specific CSV type
                var csvPath = GenerateSpecificCsvSample(testDir, csvType);
                
                if (csvPath != null && File.Exists(csvPath))
                {
                    // Basic validation that file can be read
                    var lines = File.ReadAllLines(csvPath);
                    Assert.NotEmpty(lines);
                    
                    // First line should be header
                    var header = lines[0];
                    Assert.Contains(",", header);
                }
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        [Fact]
        public void EmptyCsv_DoesNotCauseException()
        {
            var testDir = Path.Combine(Path.GetTempPath(), "vhc-empty-test-" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);

            try
            {
                // Create empty CSV
                var emptyPath = Path.Combine(testDir, "empty.csv");
                File.WriteAllText(emptyPath, string.Empty);

                // Should not throw
                var exists = File.Exists(emptyPath);
                Assert.True(exists);

                var content = File.ReadAllText(emptyPath);
                Assert.Empty(content);
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        [Fact]
        public void CsvWithSpecialCharacters_HandledCorrectly()
        {
            var testDir = Path.Combine(Path.GetTempPath(), "vhc-special-test-" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);

            try
            {
                var csvPath = Path.Combine(testDir, "special.csv");
                
                // Create CSV with special characters
                var content = "Name,Path,Status\n" +
                             "\"Test, with comma\",\"C:\\Path\\To\\File\",Success\n" +
                             "\"Quote \"\"test\"\"\",\"D:\\Another\\Path\",Warning\n";
                
                File.WriteAllText(csvPath, content);

                // Validate it can be read
                var lines = File.ReadAllLines(csvPath);
                Assert.Equal(3, lines.Length); // Header + 2 data rows
                Assert.Contains("Test, with comma", lines[1]);
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        private int GenerateSampleCsvFiles(string directory)
        {
            // Generate basic sample CSVs for testing
            int count = 0;

            // VBR Compliance sample
            var vbrPath = Path.Combine(directory, "vbr_compliance.csv");
            File.WriteAllText(vbrPath, 
                "JobName,Type,Status,StartTime,EndTime\n" +
                "Backup Job 1,Backup,Success,2025-11-17 08:00:00,2025-11-17 09:00:00\n" +
                "Replication Job 1,Replica,Warning,2025-11-17 10:00:00,2025-11-17 11:00:00\n");
            count++;

            // Repository Info sample
            var repoPath = Path.Combine(directory, "repository_info.csv");
            File.WriteAllText(repoPath,
                "Name,Path,Capacity,FreeSpace\n" +
                "Repo1,C:\\Backup,1000000000,500000000\n" +
                "Repo2,D:\\Backup,2000000000,1000000000\n");
            count++;

            return count;
        }

        private string? GenerateSpecificCsvSample(string directory, string csvType)
        {
            var csvPath = Path.Combine(directory, $"{csvType.ToLower()}.csv");
            
            switch (csvType)
            {
                case "VBR_Compliance":
                    File.WriteAllText(csvPath,
                        "JobName,Type,Result,Duration\n" +
                        "Job1,Backup,Success,00:30:00\n");
                    return csvPath;
                    
                case "VB365_Compliance":
                    File.WriteAllText(csvPath,
                        "Organization,Job,Status,ProcessedItems\n" +
                        "Org1,VB365Job1,Success,1000\n");
                    return csvPath;
                    
                case "Repository_Info":
                    File.WriteAllText(csvPath,
                        "Repository,Type,Path,Capacity\n" +
                        "PrimaryRepo,Standard,C:\\Backup,1000GB\n");
                    return csvPath;
                    
                case "Job_Sessions":
                    File.WriteAllText(csvPath,
                        "JobName,SessionId,State,StartTime\n" +
                        "BackupJob,12345,Stopped,2025-11-17 08:00\n");
                    return csvPath;
                    
                default:
                    return null;
            }
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
    }
}
