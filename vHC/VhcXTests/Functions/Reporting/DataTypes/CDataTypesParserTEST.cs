using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Shared;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.DataTypes
{
    /// <summary>
    /// Tests for CDataTypesParser functionality.
    /// Tests focus on CSV parsing and data type conversion.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CDataTypesParserTEST : IDisposable
    {
        private readonly string _testDataDir;
        private readonly string _vbrDir;
        private readonly string _originalDesiredPath;
        private readonly string _originalVbrServerName;

        public CDataTypesParserTEST()
        {
            // Save original global state
            _originalDesiredPath = CGlobals.desiredPath;
            _originalVbrServerName = CGlobals.VBRServerName;

            // Create test directory structure
            _testDataDir = Path.Combine(Path.GetTempPath(), "VhcDataTypesTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataDir);

            // Set up globals to point to our test directory
            CGlobals.desiredPath = _testDataDir;
            CGlobals.VBRServerName = "TestServer";

            // Create VBR directory structure that matches CVariables.vbrDir expectations
            _vbrDir = VbrCsvSampleGenerator.CreateTestDataDirectory(_testDataDir);
        }

        public void Dispose()
        {
            // Restore original global state
            CGlobals.desiredPath = _originalDesiredPath;
            CGlobals.VBRServerName = _originalVbrServerName;

            // Clean up test directory
            VbrCsvSampleGenerator.CleanupTestDirectory(_testDataDir);
        }

        #region CCsvParser Dynamic Method Tests

        [Fact]
        public void GetDynamicJobInfo_WithValidData_ReturnsRecords()
        {
            var parser = new CCsvParser(_vbrDir);
            var jobs = parser.GetDynamicJobInfo();

            Assert.NotNull(jobs);
            var jobList = jobs.ToList();
            Assert.Equal(3, jobList.Count);
        }

        [Fact]
        public void GetDynamicJobInfo_ContainsExpectedFields()
        {
            var parser = new CCsvParser(_vbrDir);
            var jobs = parser.GetDynamicJobInfo().ToList();

            // Verify job names (CsvHelper lowercases headers via PrepareHeaderForMatch)
            Assert.Contains(jobs, j => j.name == "Daily Backup");
            Assert.Contains(jobs, j => j.name == "Weekly Full");
            Assert.Contains(jobs, j => j.name == "Copy to Cloud");
        }

        [Fact]
        public void GetDynamicJobInfo_NoFile_ReturnsEmpty()
        {
            var emptyDir = Path.Combine(_testDataDir, "empty_jobs");
            Directory.CreateDirectory(emptyDir);

            var parser = new CCsvParser(emptyDir);
            var jobs = parser.GetDynamicJobInfo();

            Assert.NotNull(jobs);
            Assert.Empty(jobs);
        }

        [Fact]
        public void GetDynamicRepo_WithValidData_ReturnsRecords()
        {
            var parser = new CCsvParser(_vbrDir);
            var repos = parser.GetDynamicRepo();

            Assert.NotNull(repos);
            var repoList = repos.ToList();
            Assert.Equal(2, repoList.Count);
        }

        [Fact]
        public void GetDynamicRepo_ContainsImmutabilityField()
        {
            var parser = new CCsvParser(_vbrDir);
            var repos = parser.GetDynamicRepo().ToList();

            // Verify immutability field is present
            Assert.Contains(repos, r => r.isimmutabilitysupported == "True");
            Assert.Contains(repos, r => r.isimmutabilitysupported == "False");
        }

        [Fact]
        public void GetDynamicSobr_WithValidData_ReturnsRecords()
        {
            var parser = new CCsvParser(_vbrDir);
            var sobrs = parser.GetDynamicSobr();

            Assert.NotNull(sobrs);
            Assert.NotEmpty(sobrs);
        }

        [Fact]
        public void GetDynamicSobrExt_WithValidData_ReturnsRecords()
        {
            var parser = new CCsvParser(_vbrDir);
            var extents = parser.GetDynamicSobrExt();

            Assert.NotNull(extents);
            Assert.NotEmpty(extents);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void CCsvParser_WithEmptyDirectory_HandlesGracefully()
        {
            var emptyDir = Path.Combine(_testDataDir, "completely_empty");
            Directory.CreateDirectory(emptyDir);

            var parser = new CCsvParser(emptyDir);

            // All parsers should return empty or null for missing files
            Assert.Empty(parser.GetDynamicJobInfo());
            Assert.Empty(parser.GetDynamicVbrInfo());
        }

        [Fact]
        public void CCsvParser_WithNonExistentDirectory_HandlesGracefully()
        {
            var nonExistentDir = Path.Combine(_testDataDir, "does_not_exist");

            var parser = new CCsvParser(nonExistentDir);

            // Should not throw, should return empty
            Assert.Empty(parser.GetDynamicJobInfo());
        }

        #endregion

        #region License and Config Backup Tests

        [Fact]
        public void GetDynamicLicenseCsv_WithValidData_ReturnsLicenseInfo()
        {
            var parser = new CCsvParser(_vbrDir);
            var license = parser.GetDynamicLicenseCsv();

            Assert.NotNull(license);
            var licList = license.ToList();
            Assert.Single(licList);
        }

        [Fact]
        public void GetDynamincConfigBackup_WithValidData_ReturnsBackupInfo()
        {
            var parser = new CCsvParser(_vbrDir);
            var configBackup = parser.GetDynamincConfigBackup();

            Assert.NotNull(configBackup);
            Assert.NotEmpty(configBackup);
        }

        #endregion

        #region Network Rules Tests

        [Fact]
        public void GetDynamincNetRules_WithValidData_ReturnsRules()
        {
            var parser = new CCsvParser(_vbrDir);
            var rules = parser.GetDynamincNetRules();

            Assert.NotNull(rules);
            var ruleList = rules.ToList();
            Assert.Equal(2, ruleList.Count);
        }

        [Fact]
        public void GetDynamincNetRules_ContainsEncryptionField()
        {
            var parser = new CCsvParser(_vbrDir);
            var rules = parser.GetDynamincNetRules().ToList();

            Assert.Contains(rules, r => r.encryptionenabled == "True");
        }

        #endregion
    }
}
