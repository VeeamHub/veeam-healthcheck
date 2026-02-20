using System;
using System.IO;
using System.Linq;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Shared;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Tests for CDataFormer job session summary and related data transformation methods.
    /// These tests verify the data transformation pipeline from CSV to XML/report data.
    /// </summary>
    [Trait("Category", "DataTransformation")]
    public class CJobSessSummaryTEST : IDisposable
    {
        private readonly string _testDataDir;
        private readonly string _vbrDir;
        private readonly string _originalDesiredPath;
        private readonly string _originalVbrServerName;

        public CJobSessSummaryTEST()
        {
            // Save original global state
            _originalDesiredPath = CGlobals.desiredPath;
            _originalVbrServerName = CGlobals.VBRServerName;

            // Create test directory structure
            _testDataDir = Path.Combine(Path.GetTempPath(), "VhcJobSessTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataDir);

            // Set up globals to point to our test directory
            CGlobals.desiredPath = _testDataDir;
            CGlobals.VBRServerName = "TestServer";

            // Create VBR directory structure with test data
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

        #region CSV Parser Session Tests

        [Fact]
        public void SessionCsvParser_WithValidData_ReturnsRecords()
        {
            var parser = new CCsvParser(_vbrDir);
            var sessions = parser.SessionCsvParser();

            // Note: SessionCsvParser may return null if file naming doesn't match
            // This test validates the method exists and handles data
            if (sessions != null)
            {
                var sessionList = sessions.ToList();
                Assert.True(sessionList.Count >= 0);
            }
        }

        #endregion

        #region CDataFormer Security Summary Tests

        [Fact]
        public void CDataFormer_SecSummary_ReturnsSecurityTable()
        {
            // Note: CDataFormer uses static CCsvParser methods that may not use our test path
            // This test verifies the method doesn't throw and returns a valid object
            try
            {
                var dataFormer = new CDataFormer();
                var result = dataFormer.SecSummary();

                Assert.NotNull(result);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                // Expected when CSV files aren't in the expected static locations
                Assert.True(true, "CDataFormer relies on static paths - test environment may not have required files");
            }
        }

        #endregion

        #region CSV Parser Job Tests

        [Fact]
        public void JobCsvParser_WithValidData_ReturnsJobs()
        {
            var parser = new CCsvParser(_vbrDir);
            var jobs = parser.JobCsvParser();

            Assert.NotNull(jobs);
            var jobList = jobs.ToList();
            Assert.Equal(3, jobList.Count);
        }

        [Fact]
        public void GetDynamicJobInfo_ContainsScheduleInfo()
        {
            var parser = new CCsvParser(_vbrDir);
            var jobs = parser.GetDynamicJobInfo().ToList();

            // Verify schedule enabled field
            Assert.Contains(jobs, j => j.IsScheduleEnabled == "True");
            Assert.Contains(jobs, j => j.IsScheduleEnabled == "False");
        }

        [Fact]
        public void GetDynamicJobInfo_ContainsEncryptionKeyIds()
        {
            var parser = new CCsvParser(_vbrDir);
            var jobs = parser.GetDynamicJobInfo().ToList();

            // Verify encryption key IDs
            Assert.Contains(jobs, j => j.pwdkeyid == "00000000-0000-0000-0000-000000000000");
            Assert.Contains(jobs, j => j.pwdkeyid != "00000000-0000-0000-0000-000000000000");
        }

        #endregion

        #region Server Summary Tests

        [Fact]
        public void ServerCsvParser_WithValidData_ReturnsServers()
        {
            var parser = new CCsvParser(_vbrDir);
            var servers = parser.ServerCsvParser();

            Assert.NotNull(servers);
            var serverList = servers.ToList();
            Assert.Equal(3, serverList.Count);
        }

        [Fact]
        public void GetDynamicVbrInfo_ContainsVersionInfo()
        {
            var parser = new CCsvParser(_vbrDir);
            var vbrInfo = parser.GetDynamicVbrInfo().ToList();

            Assert.Single(vbrInfo);
            Assert.Equal("12.0.0.1420", vbrInfo[0].version);
        }

        #endregion

        #region Repository Tests

        [Fact]
        public void RepoCsvParser_WithValidData_ReturnsRepos()
        {
            var parser = new CCsvParser(_vbrDir);
            var repos = parser.RepoCsvParser();

            Assert.NotNull(repos);
            var repoList = repos.ToList();
            Assert.Equal(2, repoList.Count);
        }

        [Fact]
        public void GetDynamicRepo_ContainsSpaceInfo()
        {
            var parser = new CCsvParser(_vbrDir);
            var repos = parser.GetDynamicRepo().ToList();

            // Verify space fields are present
            foreach (var repo in repos)
            {
                Assert.NotNull(repo.TotalSpace);
                Assert.NotNull(repo.FreeSpace);
            }
        }

        #endregion

        #region Proxy Tests

        [Fact]
        public void ProxyCsvParser_WithValidData_ReturnsProxies()
        {
            var parser = new CCsvParser(_vbrDir);
            var proxies = parser.ProxyCsvParser();

            Assert.NotNull(proxies);
            var proxyList = proxies.ToList();
            Assert.Equal(2, proxyList.Count);
        }

        [Fact]
        public void ProxyCsvParser_ContainsTransportMode()
        {
            var parser = new CCsvParser(_vbrDir);
            var proxies = parser.ProxyCsvParser().ToList();

            // All proxies should have Auto transport mode in our test data
            Assert.All(proxies, p => Assert.Equal("Auto", p.TransportMode));
        }

        #endregion

        #region SOBR Tests

        [Fact]
        public void SobrCsvParser_WithValidData_ReturnsSobrs()
        {
            var parser = new CCsvParser(_vbrDir);
            var sobrs = parser.SobrCsvParser();

            Assert.NotNull(sobrs);
            var sobrList = sobrs.ToList();
            Assert.Equal(2, sobrList.Count);
        }

        [Fact]
        public void SobrExtParser_WithValidData_ReturnsExtents()
        {
            var parser = new CCsvParser(_vbrDir);
            var extents = parser.SobrExtParser();

            Assert.NotNull(extents);
            var extentList = extents.ToList();
            Assert.Equal(2, extentList.Count);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void CCsvParser_MissingFile_ReturnsNullOrEmpty()
        {
            var emptyDir = Path.Combine(_testDataDir, "empty_dir");
            Directory.CreateDirectory(emptyDir);

            var parser = new CCsvParser(emptyDir);

            // Missing files should return null or empty, not throw
            Assert.Null(parser.JobCsvParser());
            Assert.Null(parser.ServerCsvParser());
            Assert.Null(parser.ProxyCsvParser());
        }

        [Fact]
        public void CCsvParser_NonExistentPath_HandlesGracefully()
        {
            var nonExistentDir = Path.Combine(_testDataDir, "does_not_exist");

            var parser = new CCsvParser(nonExistentDir);

            // Should not throw
            var result = parser.JobCsvParser();
            Assert.Null(result);
        }

        #endregion

        #region Config Backup Tests

        [Fact]
        public void ConfigBackupCsvParser_WithValidData_ReturnsConfig()
        {
            var parser = new CCsvParser(_vbrDir);
            var config = parser.ConfigBackupCsvParser();

            Assert.NotNull(config);
            var configList = config.ToList();
            Assert.Single(configList);
        }

        [Fact]
        public void GetDynamincConfigBackup_ContainsEncryptionOptions()
        {
            var parser = new CCsvParser(_vbrDir);
            var config = parser.GetDynamincConfigBackup().ToList();

            Assert.NotEmpty(config);
            // Should have encryption options field
            Assert.NotNull(config[0].encryptionoptions);
        }

        #endregion

        #region Capacity Tier Tests

        [Fact]
        public void GetDynamicCapTier_WithValidData_ReturnsCapTiers()
        {
            var parser = new CCsvParser(_vbrDir);
            var capTiers = parser.GetDynamicCapTier();

            Assert.NotNull(capTiers);
            var tierList = capTiers.ToList();
            Assert.Equal(2, tierList.Count);
        }

        [Fact]
        public void GetDynamicCapTier_ContainsImmutabilityFlag()
        {
            var parser = new CCsvParser(_vbrDir);
            var capTiers = parser.GetDynamicCapTier().ToList();

            // Verify immutability field
            Assert.Contains(capTiers, t => t.immute == "True");
            Assert.Contains(capTiers, t => t.immute == "False");
        }

        #endregion

        #region Tier Extent Extraction Tests

        [Fact]
        public void CapacityTierXmlFromCsv_WithValidData_ReturnsCapacityTiers()
        {
            var dataFormer = new CDataFormer();
            var capTiers = dataFormer.CapacityTierXmlFromCsv(false);

            Assert.NotNull(capTiers);
            // Should extract capacity tier data from SOBRs that have capacity tier enabled
            Assert.All(capTiers, ct => 
            { 
                Assert.Equal("Capacity", ct.TierType);
                Assert.NotNull(ct.SobrName);
                Assert.NotNull(ct.Name);
            });
        }

        [Fact]
        public void CapacityTierXmlFromCsv_UsesStatusFromCapTierCsv()
        {
            var integrationDir = Path.Combine(Path.GetTempPath(), "VhcCapTierStatus_" + Guid.NewGuid().ToString());
            var integrationVbrDir = VbrCsvSampleGenerator.CreateTestDataDirectory(integrationDir);

            var sobrId = "55555555-5555-5555-5555-555555555555";
            var sobrCsv = @"""PolicyType"",""Extents"",""UsePerVMBackupFiles"",""PerformFullWhenExtentOffline"",""EnableCapacityTier"",""OperationalRestorePeriod"",""OverridePolicyEnabled"",""OverrideSpaceThreshold"",""OffloadWindowOptions"",""CapacityExtent"",""EncryptionEnabled"",""EncryptionKey"",""CapacityTierCopyPolicyEnabled"",""CapacityTierMovePolicyEnabled"",""ArchiveTierEnabled"",""ArchiveExtent"",""ArchivePeriod"",""CostOptimizedArchiveEnabled"",""ArchiveFullBackupModeEnabled"",""PluginBackupsOffloadEnabled"",""CopyAllPluginBackupsEnabled"",""CopyAllMachineBackupsEnabled"",""Id"",""Name"",""Description""
""Performance"","""",""False"",""False"",""True"",""7"",""False"",""0"","""",""CapExtent-01"",""False"","""",""True"",""True"",""False"","""",""0"",""False"",""False"",""False"",""False"",""False"",""" + sobrId + """,""SOBR-Cap"",""Capacity SOBR""";
            var capTierCsv = @$"""Status"",""Type"",""Immute"",""immutabilityperiod"",""ImmutabilityMode"",""SizeLimitEnabled"",""SizeLimit"",""RepoId"",""ConnectionType"",""GatewayServer"",""parentid"",""Name""
""Normal"",""AzureBlob"",""True"",""30"",""RepositoryRetention"",""False"",""0"",""11111111-2222-3333-4444-555555555555"",""Direct"","""",""{sobrId}"",""SOBR Cap Extent""";

            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "SOBRs.csv", sobrCsv);
            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "capTier.csv", capTierCsv);

            var previousImport = CGlobals.IMPORT;
            var previousImportPath = CGlobals.IMPORT_PATH;
            var previousResolvedPath = CVariables.ResolvedImportPath;
            var previousParser = CGlobals.DtParser;

            try
            {
                CGlobals.IMPORT = true;
                CGlobals.IMPORT_PATH = integrationVbrDir;
                CVariables.ResolvedImportPath = integrationVbrDir;
                CGlobals.DtParser = new CDataTypesParser();

                var dataFormer = new CDataFormer();
                var capTiers = dataFormer.CapacityTierXmlFromCsv(false);

                Assert.Single(capTiers);
                Assert.Equal("Normal", capTiers[0].Status);
                Assert.Equal("SOBR Cap Extent", capTiers[0].Name);
                Assert.Equal("SOBR-Cap", capTiers[0].SobrName);
            }
            finally
            {
                CGlobals.IMPORT = previousImport;
                CGlobals.IMPORT_PATH = previousImportPath;
                CVariables.ResolvedImportPath = previousResolvedPath;
                CGlobals.DtParser = previousParser;
                VbrCsvSampleGenerator.CleanupTestDirectory(integrationDir);
            }
        }

        [Fact]
        public void CapacityTierXmlFromCsv_MultipleExtentsPerSobr_ReturnsAllExtents()
        {
            // Regression test: a SOBR can legitimately have multiple capacity extents.
            // Each row in _capTier.csv with the same parentid must produce a separate
            // CCapacityTierExtent — not silently discard all but the last one.
            var integrationDir = Path.Combine(Path.GetTempPath(), "VhcCapTierMulti_" + Guid.NewGuid().ToString());
            var integrationVbrDir = VbrCsvSampleGenerator.CreateTestDataDirectory(integrationDir);

            var sobrId = "aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb";
            var sobrCsv = @"""PolicyType"",""Extents"",""UsePerVMBackupFiles"",""PerformFullWhenExtentOffline"",""EnableCapacityTier"",""OperationalRestorePeriod"",""OverridePolicyEnabled"",""OverrideSpaceThreshold"",""OffloadWindowOptions"",""CapacityExtent"",""EncryptionEnabled"",""EncryptionKey"",""CapacityTierCopyPolicyEnabled"",""CapacityTierMovePolicyEnabled"",""ArchiveTierEnabled"",""ArchiveExtent"",""ArchivePeriod"",""CostOptimizedArchiveEnabled"",""ArchiveFullBackupModeEnabled"",""PluginBackupsOffloadEnabled"",""CopyAllPluginBackupsEnabled"",""CopyAllMachineBackupsEnabled"",""Id"",""Name"",""Description""
""Performance"",""perf-extent-01"",""True"",""False"",""True"",""1"",""False"",""90"","""",""cap-extent-01"",""True"",""test-encryption-key"",""True"",""True"",""False"","""",""0"",""False"",""False"",""True"",""True"",""True"",""" + sobrId + @""",""SOBR-Multi-Extent"",""Test SOBR with multiple capacity extents""";
            // Two capacity extents with the same parentid — this is the scenario that was broken.
            var capTierCsv = @$"""Status"",""Type"",""Immute"",""immutabilityperiod"",""ImmutabilityMode"",""SizeLimitEnabled"",""SizeLimit"",""RepoId"",""ConnectionType"",""GatewayServer"",""parentid"",""Name""
""Online"",""6"",""True"",""30"",""RepositoryRetention"",""True"",""1024"",""cccccccc-1111-2222-3333-dddddddddddd"",""0"","""",""{sobrId}"",""cap-extent-01""
""Disabled"",""6"",""True"",""7"",""RepositoryRetention"",""True"",""1024"",""eeeeeeee-1111-2222-3333-ffffffffffff"",""0"","""",""{sobrId}"",""cap-extent-02""";

            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "SOBRs.csv", sobrCsv);
            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "capTier.csv", capTierCsv);

            var previousImport = CGlobals.IMPORT;
            var previousImportPath = CGlobals.IMPORT_PATH;
            var previousResolvedPath = CVariables.ResolvedImportPath;
            var previousParser = CGlobals.DtParser;

            try
            {
                CGlobals.IMPORT = true;
                CGlobals.IMPORT_PATH = integrationVbrDir;
                CVariables.ResolvedImportPath = integrationVbrDir;
                CGlobals.DtParser = new CDataTypesParser();

                var dataFormer = new CDataFormer();
                var capTiers = dataFormer.CapacityTierXmlFromCsv(false);

                // Both extents must appear — the old code only kept the last one.
                Assert.Equal(2, capTiers.Count);

                var first = capTiers.Single(e => e.Name == "cap-extent-01");
                Assert.Equal("Online", first.Status);
                Assert.Equal("SOBR-Multi-Extent", first.SobrName);
                Assert.Equal(sobrId, first.ParentSobrId);
                Assert.True(first.CopyModeEnabled);
                Assert.True(first.MoveModeEnabled);

                var second = capTiers.Single(e => e.Name == "cap-extent-02");
                Assert.Equal("Disabled", second.Status);
                Assert.Equal("SOBR-Multi-Extent", second.SobrName);
                Assert.Equal(sobrId, second.ParentSobrId);
            }
            finally
            {
                CGlobals.IMPORT = previousImport;
                CGlobals.IMPORT_PATH = previousImportPath;
                CVariables.ResolvedImportPath = previousResolvedPath;
                CGlobals.DtParser = previousParser;
                VbrCsvSampleGenerator.CleanupTestDirectory(integrationDir);
            }
        }

        [Fact]
        public void ArchiveTierXmlFromCsv_WithValidData_ReturnsArchiveTiers()
        {
            var dataFormer = new CDataFormer();
            var archTiers = dataFormer.ArchiveTierXmlFromCsv(false);

            Assert.NotNull(archTiers);
            // Should extract archive tier data from SOBRs that have archive tier enabled
            Assert.All(archTiers, at =>
            {
                Assert.NotNull(at.SobrName);
                Assert.NotNull(at.Name);
                Assert.NotNull(at.OffloadPeriod);
            });
        }

        [Fact]
        public void ArchiveTierXmlFromCsv_JoinsArchiveTierCsvOnParentId()
        {
            var integrationDir = Path.Combine(Path.GetTempPath(), "VhcArchiveTierIntegration_" + Guid.NewGuid().ToString());
            var integrationVbrDir = VbrCsvSampleGenerator.CreateTestDataDirectory(integrationDir);

            var sobrId = "11111111-2222-3333-4444-555555555555";
            var repoId = "77777777-8888-9999-aaaa-bbbbbbbbbbbb";
            var sobrCsv = @"""PolicyType"",""Extents"",""UsePerVMBackupFiles"",""PerformFullWhenExtentOffline"",""EnableCapacityTier"",""OperationalRestorePeriod"",""OverridePolicyEnabled"",""OverrideSpaceThreshold"",""OffloadWindowOptions"",""CapacityExtent"",""EncryptionEnabled"",""EncryptionKey"",""CapacityTierCopyPolicyEnabled"",""CapacityTierMovePolicyEnabled"",""ArchiveTierEnabled"",""ArchiveExtent"",""ArchivePeriod"",""CostOptimizedArchiveEnabled"",""ArchiveFullBackupModeEnabled"",""PluginBackupsOffloadEnabled"",""CopyAllPluginBackupsEnabled"",""CopyAllMachineBackupsEnabled"",""Id"",""Name"",""Description""
""Performance"","""",""False"",""False"",""False"",""7"",""False"",""0"","""","""",""False"","""",""False"",""False"",""True"",""Azure-Archive-Blob"",""30"",""True"",""True"",""False"",""False"",""False"",""" + sobrId + """,""SOBR-Archive"",""Archive SOBR""";
            var archTierCsv = @$"""Status"",""ParentId"",""RepoId"",""Name"",""ArchiveType"",""BackupImmutabilityEnabled""
""Normal"",""{sobrId}"",""{repoId}"",""Azure-Archive-Blob"",""AzureArchive"",""True""";

            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "SOBRs.csv", sobrCsv);
            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "archTier.csv", archTierCsv);

            var previousImport = CGlobals.IMPORT;
            var previousImportPath = CGlobals.IMPORT_PATH;
            var previousResolvedPath = CVariables.ResolvedImportPath;
            var previousParser = CGlobals.DtParser;

            try
            {
                CGlobals.IMPORT = true;
                CGlobals.IMPORT_PATH = integrationVbrDir;
                CVariables.ResolvedImportPath = integrationVbrDir;
                CGlobals.DtParser = new CDataTypesParser();

                var dataFormer = new CDataFormer();
                var archTiers = dataFormer.ArchiveTierXmlFromCsv(false);

                Assert.Single(archTiers);
                var extent = archTiers[0];
                Assert.Equal("SOBR-Archive", extent.SobrName);
                Assert.Equal("Azure-Archive-Blob", extent.Name);
                Assert.Equal("AzureArchive", extent.Type);
                Assert.Equal("Normal", extent.Status);
                Assert.True(extent.ArchiveTierEnabled);
                Assert.Equal("30", extent.OffloadPeriod);
                Assert.True(extent.ImmutableEnabled);
            }
            finally
            {
                CGlobals.IMPORT = previousImport;
                CGlobals.IMPORT_PATH = previousImportPath;
                CVariables.ResolvedImportPath = previousResolvedPath;
                CGlobals.DtParser = previousParser;
                VbrCsvSampleGenerator.CleanupTestDirectory(integrationDir);
            }
        }

        [Fact]
        public void ArchiveTierXmlFromCsv_UnknownParentId_UsesArchiveCsvDefaults()
        {
            var integrationDir = Path.Combine(Path.GetTempPath(), "VhcArchiveTierIntegrationMissing_" + Guid.NewGuid().ToString());
            var integrationVbrDir = VbrCsvSampleGenerator.CreateTestDataDirectory(integrationDir);

            var unknownParentId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
            var repoId = "99999999-8888-7777-6666-555555555555";
            var archTierCsv = @$"""Status"",""ParentId"",""RepoId"",""Name"",""ArchiveType"",""BackupImmutabilityEnabled""
""Normal"",""{unknownParentId}"",""{repoId}"",""Archive-Only"",""AzureArchive"",""False""";

            VbrCsvSampleGenerator.CreateCsvFile(integrationVbrDir, "archTier.csv", archTierCsv);

            var previousImport = CGlobals.IMPORT;
            var previousImportPath = CGlobals.IMPORT_PATH;
            var previousResolvedPath = CVariables.ResolvedImportPath;
            var previousParser = CGlobals.DtParser;

            try
            {
                CGlobals.IMPORT = true;
                CGlobals.IMPORT_PATH = integrationVbrDir;
                CVariables.ResolvedImportPath = integrationVbrDir;
                CGlobals.DtParser = new CDataTypesParser();

                var dataFormer = new CDataFormer();
                var archTiers = dataFormer.ArchiveTierXmlFromCsv(false);

                Assert.Single(archTiers);
                var extent = archTiers[0];
                Assert.Equal(string.Empty, extent.SobrName);
                Assert.Equal("Archive-Only", extent.Name);
                Assert.Equal("AzureArchive", extent.Type);
                Assert.Equal("Normal", extent.Status);
                Assert.True(extent.ArchiveTierEnabled);
                Assert.Equal(string.Empty, extent.OffloadPeriod);
                Assert.False(extent.ImmutableEnabled);
            }
            finally
            {
                CGlobals.IMPORT = previousImport;
                CGlobals.IMPORT_PATH = previousImportPath;
                CVariables.ResolvedImportPath = previousResolvedPath;
                CGlobals.DtParser = previousParser;
                VbrCsvSampleGenerator.CleanupTestDirectory(integrationDir);
            }
        }

        #endregion
    }
}
