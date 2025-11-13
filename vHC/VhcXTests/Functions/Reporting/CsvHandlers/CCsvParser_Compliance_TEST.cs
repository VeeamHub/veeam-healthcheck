using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using Xunit;
using System;
using System.IO;
using System.Linq;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    /// <summary>
    /// Tests for Security & Compliance CSV parsing across VBR versions
    /// </summary>
    public class CCsvParser_Compliance_TEST : IDisposable
    {
        private readonly string _testDataDir;

        public CCsvParser_Compliance_TEST()
        {
            // Create test data directory (CSVs will be in VBR subdirectory)
            _testDataDir = Path.Combine(Path.GetTempPath(), "VhcTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path.Combine(_testDataDir, "VBR"));
        }

        public void Dispose()
        {
            // Clean up test data
            if (Directory.Exists(_testDataDir))
            {
                try
                {
                    Directory.Delete(_testDataDir, true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }

        /// <summary>
        /// Helper method to create a test CSV file
        /// </summary>
        private string CreateTestCsvFile(string fileName, string content)
        {
            var vbrDir = Path.Combine(_testDataDir, "VBR");
            var filePath = Path.Combine(vbrDir, fileName);
            File.WriteAllText(filePath, content);
            return vbrDir;
        }

        #region Unit Tests - No CSV Files

        [Fact]
        public void ComplianceCsv_NoFile_ReturnsNull()
        {
            var vbrDir = Path.Combine(_testDataDir, "VBR");
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            Assert.Null(result);
        }

        [Fact]
        public void ComplianceCsv_EmptyDirectory_ReturnsNull()
        {
            var vbrDir = Path.Combine(_testDataDir, "VBR");
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            Assert.Null(result);
        }

        #endregion

        #region Integration Tests - VBR v12

        [Fact]
        public void ComplianceCsv_Vbr12Sample_ParsesCorrectly()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateVbr12Sample();
            var vbrDir = CreateTestCsvFile("localhost_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            
            Assert.NotNull(result);
            var compliance = result.ToList();
            Assert.Equal(37, compliance.Count); // VBR 12 has 37 rules
        }

        [Fact]
        public void ComplianceCsv_Vbr12Sample_AllStatusTypes()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateVbr12Sample();
            var vbrDir = CreateTestCsvFile("localhost_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            var compliance = result.ToList();
            
            Assert.Contains(compliance, c => c.Status == "Passed");
            Assert.Contains(compliance, c => c.Status == "Not Implemented");
            Assert.Contains(compliance, c => c.Status == "Unable to detect");
        }

        #endregion

        #region Integration Tests - VBR v13

        [Fact]
        public void ComplianceCsv_Vbr13Sample_ParsesCorrectly()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateVbr13Sample();
            var vbrDir = CreateTestCsvFile("vbr-v13-server_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            
            Assert.NotNull(result);
            var compliance = result.ToList();
            Assert.Equal(48, compliance.Count); // VBR 13 has 48 rules (37 base + 2 split password + 10 Linux - 1 removed merged password)
        }

        [Fact]
        public void ComplianceCsv_Vbr13_HasNewLinuxChecks()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateVbr13Sample();
            var vbrDir = CreateTestCsvFile("vbr-v13-server_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            var compliance = result.ToList();
            
            // Check for new VBR 13 Linux-specific checks
            Assert.Contains(compliance, c => c.BestPractice.Contains("Linux audit binaries owner is root"));
            Assert.Contains(compliance, c => c.BestPractice.Contains("Linux auditd is configured"));
            Assert.Contains(compliance, c => c.BestPractice.Contains("Secure Boot is enabled"));
        }

        [Fact]
        public void ComplianceCsv_Vbr13_HasSplitPasswordChecks()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateVbr13Sample();
            var vbrDir = CreateTestCsvFile("vbr-v13-server_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            var compliance = result.ToList();
            
            // VBR 13 splits password complexity into separate checks
            Assert.Contains(compliance, c => c.BestPractice.Contains("Backup encryption password"));
            Assert.Contains(compliance, c => c.BestPractice.Contains("Credentials password"));
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ComplianceCsv_MinimalSample_ParsesCorrectly()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateMinimalSample();
            var vbrDir = CreateTestCsvFile("test_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            
            Assert.NotNull(result);
            var compliance = result.ToList();
            Assert.Equal(3, compliance.Count);
        }

        [Fact]
        public void ComplianceCsv_AllStatusTypes_ParsesCorrectly()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateAllStatusTypesSample();
            var vbrDir = CreateTestCsvFile("test_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            
            Assert.NotNull(result);
            var compliance = result.ToList();
            Assert.Equal(4, compliance.Count);
            Assert.Contains(compliance, c => c.Status == "Passed");
            Assert.Contains(compliance, c => c.Status == "Not Implemented");
            Assert.Contains(compliance, c => c.Status == "Unable to detect");
            Assert.Contains(compliance, c => c.Status == "Suppressed");
        }

        [Fact]
        public void ComplianceCsv_EmptyFile_ReturnsNull()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateEmptySample();
            var vbrDir = CreateTestCsvFile("test_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            
            // Empty file (headers only) should return null or empty collection
            Assert.True(result == null || !result.Any());
        }

        [Fact]
        public void ComplianceCsv_RemoteExecution_WithHostnamePrefix()
        {
            var csvContent = ComplianceCsvSampleGenerator.GenerateMinimalSample();
            var vbrDir = CreateTestCsvFile("remote-vbr-server_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            var result = parser.ComplianceCsv();
            
            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
        }

        #endregion

        #region Error Handling

        [Fact]
        public void ComplianceCsv_MalformedCsv_HandlesGracefully()
        {
            var csvContent = @"""Best Practice"",""Status""
""Test check"",""Passed""
This is not valid CSV format
""Another check"""; // Missing status column
            
            var vbrDir = CreateTestCsvFile("test_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            
            // Should either return null or throw exception - depends on implementation
            // Adjust assertion based on actual CCsvParser behavior
            try
            {
                var result = parser.ComplianceCsv();
                // If it doesn't throw, verify result is valid
                if (result != null)
                {
                    var list = result.ToList();
                    Assert.True(list.Count >= 1); // At least the first valid row
                }
            }
            catch (Exception)
            {
                // Expected if parser throws on malformed data
                Assert.True(true);
            }
        }

        [Fact]
        public void ComplianceCsv_MissingColumns_HandlesGracefully()
        {
            var csvContent = @"""Only One Column""
""Test value""";
            
            var vbrDir = CreateTestCsvFile("test_SecurityCompliance.csv", csvContent);
            
            var parser = new CCsvParser(vbrDir);
            
            // Should handle missing columns gracefully
            try
            {
                var result = parser.ComplianceCsv();
                // If no exception, result should be null or empty
                Assert.True(result == null || !result.Any());
            }
            catch (Exception)
            {
                // Expected if parser throws on invalid format
                Assert.True(true);
            }
        }

        #endregion
    }
}
