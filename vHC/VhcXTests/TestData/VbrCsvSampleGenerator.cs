using System;
using System.Collections.Generic;
using System.IO;

namespace VhcXTests.TestData
{
    /// <summary>
    /// Helper class to generate sample VBR CSV files for testing.
    /// Creates realistic test data for different VBR versions and scenarios.
    /// </summary>
    public static class VbrCsvSampleGenerator
    {
        #region vbrinfo.csv

        /// <summary>
        /// Generate a sample vbrinfo.csv with VBR server information
        /// </summary>
        public static string GenerateVbrInfo(string version = "12.0.0.1420", bool mfaEnabled = true, bool fourEyesEnabled = false)
        {
            return $@"""Name"",""Version"",""SqlServer"",""SqlDatabase"",""mfa"",""foureyes""
""VBR-Server"",""{version}"",""localhost"",""VeeamBackup"",""{mfaEnabled}"",""{fourEyesEnabled}""";
        }

        #endregion

        #region archTier.csv

        /// <summary>
        /// Generate a sample archTier.csv matching the production script structure.
        /// This represents the _archTier.csv file created by Get-VBRConfig.ps1
        /// when collecting archive extent information.
        /// </summary>
        public static string GenerateArchTier(bool immutableEnabled = false)
        {
            var immutableValue = immutableEnabled.ToString();
            return @$"""Status"",""ParentId"",""RepoId"",""Name"",""ArchiveType"",""BackupImmutabilityEnabled"",""GatewayMode"",""GatewayServer""
""Normal"",""55555555-5555-5555-5555-555555555555"",""77777777-8888-9999-aaaa-bbbbbbbbbbbb"",""Azure-Archive-Blob"",""AzureArchive"",""{immutableValue}"",""Direct"",""vbr-server-01.test.local(aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb); proxy-server-01.test.local(cccccccc-1111-2222-3333-dddddddddddd)""";
        }

        #endregion

        #region Servers.csv

        /// <summary>
        /// Generate a sample Servers.csv
        /// </summary>
        public static string GenerateServers()
        {
            return @"""Name"",""Type"",""ApiVersion"",""HostId""
""VBR-Server"",""Microsoft Windows Server"",""12.0.0.1420"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee""
""Proxy-01"",""Microsoft Windows Server"",""12.0.0.1420"",""11111111-2222-3333-4444-555555555555""
""Repo-01"",""Microsoft Windows Server"",""12.0.0.1420"",""22222222-3333-4444-5555-666666666666""";
        }

        /// <summary>
        /// Generate an empty Servers.csv (headers only)
        /// </summary>
        public static string GenerateEmptyServers()
        {
            return @"""Name"",""Type"",""ApiVersion"",""HostId""";
        }

        #endregion

        #region Proxies.csv

        /// <summary>
        /// Generate a sample Proxies.csv (VI proxies)
        /// </summary>
        public static string GenerateProxies()
        {
            return @"""Name"",""Host"",""MaxTasksCount"",""TransportMode"",""HostId""
""Proxy-01"",""Proxy-01.domain.local"",""4"",""Auto"",""11111111-2222-3333-4444-555555555555""
""VBR-Server"",""VBR-Server.domain.local"",""2"",""Auto"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee""";
        }

        /// <summary>
        /// Generate an empty Proxies.csv
        /// </summary>
        public static string GenerateEmptyProxies()
        {
            return @"""Name"",""Host"",""MaxTasksCount"",""TransportMode"",""HostId""";
        }

        #endregion

        #region Repositories.csv

        /// <summary>
        /// Generate a sample Repositories.csv
        /// </summary>
        public static string GenerateRepositories()
        {
            // Headers match CRepoCsvInfos [Index(n)] positional mapping (40 columns).
            // Position 25 uses "isimmutabilitysupported" (lowercase) so dynamic tests can access r.isimmutabilitysupported.
            // Positions 37/38 use "TotalSpace"/"FreeSpace" so dynamic tests can access repo.TotalSpace / repo.FreeSpace.
            // Bool positions (11,13,16,18,21,22,23,24,25) must have valid True/False values.
            return @"""Id"",""Name"",""HostId"",""Description"",""CreationTime"",""Path"",""FullPath"",""FriendlyPath"",""ShareCredsId"",""Type"",""Status"",""IsUnavailable"",""Group"",""UseNfsOnMountHost"",""VersionOfCreation"",""Tag"",""IsTemporary"",""TypeDisplay"",""IsRotatedDriveRepository"",""EndPointCryptoKeyId"",""Options"",""HasBackupChainLengthLimitation"",""IsSanSnapshotOnly"",""IsDedupStorage"",""SplitStoragesPerVm"",""isimmutabilitysupported"",""MaxTasks"",""UnlimitedTasks"",""MaxArchiveTasks"",""DataRateLimit"",""Uncompress"",""AlignBlock"",""RemoteAccessLimitation"",""EpEncryptionEnabled"",""OneBackupFilePerVM"",""AutoDetectAffinity"",""NfsRepoEncoding"",""TotalSpace"",""FreeSpace"",""GateHosts""
""repo-id-1"",""Default Backup Repository"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"","""","""",""C:\Backup"",""C:\Backup"",""C:\Backup"","""",""WinLocal"",""Available"",""False"","""",""False"","""","""",""False"",""Windows local"",""False"","""","""",""False"",""False"",""False"",""False"",""False"",""4"",""False"",""0"",""0"",""False"",""False"",""None"",""False"",""False"",""True"","""",""1099511627776"",""549755813888"",""""
""repo-id-2"",""Hardened Repo"",""22222222-3333-4444-5555-666666666666"","""","""",""D:\HardenedBackup"",""D:\HardenedBackup"",""D:\HardenedBackup"","""",""LinuxHardened"",""Available"",""False"","""",""False"","""","""",""False"",""Linux hardened"",""False"","""","""",""False"",""False"",""False"",""True"",""True"",""4"",""False"",""0"",""0"",""False"",""False"",""None"",""False"",""False"",""True"","""",""2199023255552"",""1649267441664"",""""";
        }

        /// <summary>
        /// Generate repos with invalid data (free > total)
        /// </summary>
        public static string GenerateInvalidRepositories()
        {
            return @"""Id"",""Name"",""HostId"",""Description"",""CreationTime"",""Path"",""FullPath"",""FriendlyPath"",""ShareCredsId"",""Type"",""Status"",""IsUnavailable"",""Group"",""UseNfsOnMountHost"",""VersionOfCreation"",""Tag"",""IsTemporary"",""TypeDisplay"",""IsRotatedDriveRepository"",""EndPointCryptoKeyId"",""Options"",""HasBackupChainLengthLimitation"",""IsSanSnapshotOnly"",""IsDedupStorage"",""SplitStoragesPerVm"",""isimmutabilitysupported"",""MaxTasks"",""UnlimitedTasks"",""MaxArchiveTasks"",""DataRateLimit"",""Uncompress"",""AlignBlock"",""RemoteAccessLimitation"",""EpEncryptionEnabled"",""OneBackupFilePerVM"",""AutoDetectAffinity"",""NfsRepoEncoding"",""TotalSpace"",""FreeSpace"",""GateHosts""
""bad-repo-id"",""Bad Repo"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"","""","""",""C:\Backup"",""C:\Backup"",""C:\Backup"","""",""WinLocal"",""Available"",""False"","""",""False"","""","""",""False"",""Windows local"",""False"","""","""",""False"",""False"",""False"",""False"",""False"",""4"",""False"",""0"",""0"",""False"",""False"",""None"",""False"",""False"",""True"","""",""100"",""200"",""""";
        }

        #endregion

        #region _Jobs.csv

        /// <summary>
        /// Generate a sample _Jobs.csv
        /// </summary>
        public static string GenerateJobs()
        {
            // Headers match CJobCsvInfos [Index(n)] positional mapping (40 columns).
            // Position 4 uses "IsScheduleEnabled" header so dynamic tests can access j.IsScheduleEnabled.
            // Position 12 uses "pwdkeyid" (lowercase) so dynamic tests can access j.pwdkeyid.
            // Bool positions (9,26,28,30,32) and double position (13) must have valid values.
            return @"""Name"",""JobType"",""SheduleEnabledTime"",""ScheduleOptions"",""IsScheduleEnabled"",""RepoName"",""Algorithm"",""FullBackupScheduleKind"",""FullBackupDays"",""TransformFullToSyntethic"",""TransformIncrementsToSyntethic"",""TransformToSyntethicDays"",""pwdkeyid"",""OriginalSize"",""RetentionType"",""RetentionCount"",""RetainDaysToKeep"",""DeletedVmRetentionDays"",""DeletedVmRetention"",""CompressionLevel"",""Deduplication"",""BlockSize"",""IntegrityChecks"",""SpecificStorageEncryption"",""StgEncryptionEnabled"",""KeepFirstFullBackup"",""EnableFullBackup"",""BackupIsAttached"",""GfsWeeklyIsEnabled"",""GfsWeeklyCount"",""GfsMonthlyEnabled"",""GfsMonthlyCount"",""GfsYearlyEnabled"",""GfsYearlyCount"",""IndexingType"",""OnDiskGB"",""AAIPEnabled"",""VSSEnabled"",""VSSIgnoreErrors"",""GuestFSIndexingEnabled""
""Daily Backup"",""Backup"",""Daily"",""AtSpecifiedTime"",""True"",""Default Backup Repository"",""Incremental"",""FullMonthly"",""1"",""False"",""False"","""",""00000000-0000-0000-0000-000000000000"",""1000"",""Days"",""7"",""0"",""0"",""False"",""Optimal"",""True"",""LargeDeviceBlocks"",""False"",""False"",""False"",""False"",""False"",""False"",""False"",""0"",""False"",""0"",""False"",""0"",""None"",""0.5"",""True"",""True"",""False"",""True""
""Weekly Full"",""Backup"",""Weekly"",""AtSpecifiedTime"",""True"",""Default Backup Repository"",""Incremental"",""FullWeekly"",""7"",""False"",""False"","""",""enc-key-guid-1"",""2000"",""Days"",""14"",""0"",""0"",""False"",""High"",""True"",""LargeDeviceBlocks"",""False"",""False"",""False"",""False"",""False"",""False"",""False"",""0"",""False"",""0"",""False"",""0"",""None"",""1.0"",""True"",""True"",""False"",""False""
""Copy to Cloud"",""BackupCopy"",""Daily"",""AtSpecifiedTime"",""False"",""Cloud Repo"",""Reverse"","""","""",""False"",""False"","""",""enc-key-guid-2"",""500"",""Days"",""7"",""0"",""0"",""False"",""Optimal"",""True"",""LargeDeviceBlocks"",""False"",""False"",""False"",""False"",""False"",""False"",""False"",""0"",""False"",""0"",""False"",""0"",""None"",""0.25"",""False"",""False"",""False"",""False""";
        }

        #endregion

        #region SOBRs.csv and SOBRExtents.csv

        /// <summary>
        /// Generate a sample SOBRs.csv
        /// </summary>
        public static string GenerateSobrs()
        {
            return @"""PolicyType"",""Extents"",""UsePerVMBackupFiles"",""PerformFullWhenExtentOffline"",""EnableCapacityTier"",""OperationalRestorePeriod"",""OverridePolicyEnabled"",""OverrideSpaceThreshold"",""OffloadWindowOptions"",""CapacityExtent"",""EncryptionEnabled"",""EncryptionKey"",""CapacityTierCopyPolicyEnabled"",""CapacityTierMovePolicyEnabled"",""ArchiveTierEnabled"",""ArchiveExtent"",""ArchivePeriod"",""CostOptimizedArchiveEnabled"",""ArchiveFullBackupModeEnabled"",""PluginBackupsOffloadEnabled"",""CopyAllPluginBackupsEnabled"",""CopyAllMachineBackupsEnabled"",""Id"",""Name"",""Description"",""ArchiveTierEncryptionEnabled""
""PerVM"",""Extent1;Extent2"",""True"",""True"",""True"",""7"",""False"",""80"","""",""Capacity-Repo"",""True"","""",""True"",""True"",""True"",""Archive-Repo"",""30"",""True"",""False"",""True"",""False"",""True"",""55555555-5555-5555-5555-555555555555"",""SOBR-01"",""Scale-Out Repository for production workloads"",""True""
""DataLocality"",""Extent3"",""True"",""False"",""False"",""14"",""True"",""70"","""","""",""False"","""",""False"",""False"",""False"","""",""0"",""False"",""False"",""False"",""False"",""False"",""66666666-6666-6666-6666-666666666666"",""SOBR-02"",""Scale-Out Repository for archive data"",""False""";
        }

        /// <summary>
        /// Generate a sample SOBRExtents.csv
        /// </summary>
        public static string GenerateSobrExtents()
        {
            return @"""Name"",""Repository"",""TotalSpace"",""FreeSpace"",""isimmutabilitysupported""
""Extent-01"",""SOBR-01"",""5497558138880"",""2748779069440"",""True""
""Extent-02"",""SOBR-01"",""5497558138880"",""1374389534720"",""False""";
        }

        #endregion

        #region LicInfo.csv

        /// <summary>
        /// Generate a sample LicInfo.csv
        /// </summary>
        public static string GenerateLicenseInfo()
        {
            return @"""Edition"",""LicensedTo"",""SupportExpiry"",""Type"",""SocketCount"",""InstanceCount""
""Enterprise Plus"",""Test Company"",""2027-01-01"",""Subscription"",""100"",""500""";
        }

        #endregion

        #region configBackup.csv

        /// <summary>
        /// Generate a sample configBackup.csv
        /// </summary>
        public static string GenerateConfigBackup(bool encryptionEnabled = true)
        {
            return $@"""Enabled"",""Target"",""encryptionoptions"",""LastResult""
""True"",""C:\ConfigBackup"",""{encryptionEnabled}"",""Success""";
        }

        #endregion

        #region trafficRules.csv

        /// <summary>
        /// Generate a sample trafficRules.csv
        /// </summary>
        public static string GenerateTrafficRules(bool encryptionEnabled = true)
        {
            return $@"""Name"",""SourceIp"",""TargetIp"",""encryptionenabled"",""ThrottlingEnabled""
""LAN Rule"",""192.168.1.0/24"",""192.168.2.0/24"",""{encryptionEnabled}"",""False""
""WAN Rule"",""10.0.0.0/8"",""172.16.0.0/12"",""True"",""True""";
        }

        #endregion

        #region capTier.csv

        /// <summary>
        /// Generate a sample capTier.csv matching the production script structure.
        /// This represents the _capTier.csv file created by Get-VBRConfig.ps1
        /// when collecting capacity extent information.
        /// </summary>
        public static string GenerateCapTier(bool immutableEnabled = true)
        {
            var immutableValue = immutableEnabled.ToString();
            return @$"""Status"",""Type"",""Immute"",""immutabilityperiod"",""ImmutabilityMode"",""SizeLimitEnabled"",""SizeLimit"",""RepoId"",""ConnectionType"",""GatewayServer"",""parentid"",""Name""
""Online"",""AmazonS3"",""{immutableValue}"",""30"",""RepositoryRetention"",""False"",""0"",""66666666-7777-8888-9999-aaaaaaaaaaaa"",""Direct"",""vbr-server-01.test.local(aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb); proxy-server-01.test.local(cccccccc-1111-2222-3333-dddddddddddd)"",""55555555-5555-5555-5555-555555555555"",""s3-repo-01""
""Online"",""AzureBlob"",""False"",""0"","""",""True"",""10240"",""77777777-8888-9999-aaaa-bbbbbbbbbbbb"",""Direct"",""vbr-server-01.test.local(aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb)"",""66666666-6666-6666-6666-666666666666"",""azure-blob-01""";
        }

        #endregion

        #region regkeys.csv

        /// <summary>
        /// Generate a sample regkeys.csv
        /// </summary>
        public static string GenerateRegKeys()
        {
            return @"""KeyPath"",""ValueName"",""Value"",""DefaultValue""
""HKLM\SOFTWARE\Veeam"",""MaxConcurrentTasks"",""10"",""8""
""HKLM\SOFTWARE\Veeam"",""BackupCopyInterval"",""15"",""15""";
        }

        #endregion

        #region Protected/Unprotected VMs

        /// <summary>
        /// Generate a sample ViProtected.csv
        /// </summary>
        public static string GenerateViProtected()
        {
            return @"""Name"",""Path"",""JobName"",""LastBackup""
""VM-Prod-01"",""/datacenter/vm/VM-Prod-01"",""Daily Backup"",""2026-01-21""
""VM-Prod-02"",""/datacenter/vm/VM-Prod-02"",""Daily Backup"",""2026-01-21""";
        }

        /// <summary>
        /// Generate a sample ViUnprotected.csv
        /// </summary>
        public static string GenerateViUnprotected()
        {
            return @"""Name"",""Path"",""Reason""
""VM-Dev-01"",""/datacenter/vm/VM-Dev-01"",""No backup job assigned""
""VM-Test-01"",""/datacenter/vm/VM-Test-01"",""Excluded from protection""";
        }

        #endregion

        #region Session Report CSV

        /// <summary>
        /// Generate a sample VeeamSessionReport.csv for job session testing
        /// </summary>
        public static string GenerateSessionReport()
        {
            return @"""JobName"",""JobType"",""Status"",""StartTime"",""EndTime"",""Duration"",""DataSize"",""TransferredSize""
""Daily Backup"",""Backup"",""Success"",""2026-01-20 01:00:00"",""2026-01-20 02:30:00"",""01:30:00"",""1099511627776"",""549755813888""
""Daily Backup"",""Backup"",""Success"",""2026-01-19 01:00:00"",""2026-01-19 02:15:00"",""01:15:00"",""1099511627776"",""524288000000""
""Weekly Full"",""Backup"",""Warning"",""2026-01-18 22:00:00"",""2026-01-19 04:30:00"",""06:30:00"",""5497558138880"",""2748779069440""
""Copy to Cloud"",""BackupCopy"",""Failed"",""2026-01-20 03:00:00"",""2026-01-20 03:15:00"",""00:15:00"",""0"",""0""
""Copy to Cloud"",""BackupCopy"",""Success"",""2026-01-19 03:00:00"",""2026-01-19 05:00:00"",""02:00:00"",""549755813888"",""274877906944""";
        }

        #endregion

        #region User Roles CSV

        /// <summary>
        /// Generate a sample _UserRoles.csv
        /// </summary>
        public static string GenerateUserRoles()
        {
            return @"""Name"",""Role"",""Description""
""DOMAIN\VeeamAdmin"",""Veeam Backup Administrator"",""Full backup administrator access""
""DOMAIN\VeeamOperator"",""Veeam Backup Operator"",""Operator access for daily operations""
""DOMAIN\VeeamViewer"",""Veeam Restore Operator"",""Read-only and restore access""";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Safely write content to a file within a validated directory.
        /// Uses Path.GetFullPath to normalize paths and prevent traversal attacks.
        /// </summary>
        private static void SafeWriteFile(string directory, string fileName, string content)
        {
            // Normalize the directory path to prevent traversal
            var normalizedDir = Path.GetFullPath(directory);
            // Use GetFileName to strip any path components from filename (defense in depth)
            var safeFileName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(normalizedDir, safeFileName);

            // Verify the resulting path is still within the intended directory
            if (!fullPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid file path: {fileName}");
            }

            File.WriteAllText(fullPath, content);
        }

        /// <summary>
        /// Create a test data directory with all standard CSV files
        /// </summary>
        public static string CreateTestDataDirectory(string basePath = null)
        {
            // Normalize base path to prevent path traversal
            basePath = Path.GetFullPath(basePath ?? Path.Combine(Path.GetTempPath(), "VhcTests_" + Guid.NewGuid().ToString()));
            var vbrDir = Path.Combine(basePath, "VBR");
            Directory.CreateDirectory(vbrDir);

            // Create all standard CSV files using safe write method
            SafeWriteFile(vbrDir, "vbrinfo.csv", GenerateVbrInfo());
            SafeWriteFile(vbrDir, "Servers.csv", GenerateServers());
            SafeWriteFile(vbrDir, "Proxies.csv", GenerateProxies());
            SafeWriteFile(vbrDir, "Repositories.csv", GenerateRepositories());
            SafeWriteFile(vbrDir, "_Jobs.csv", GenerateJobs());
            SafeWriteFile(vbrDir, "SOBRs.csv", GenerateSobrs());
            SafeWriteFile(vbrDir, "SOBRExtents.csv", GenerateSobrExtents());
            SafeWriteFile(vbrDir, "LicInfo.csv", GenerateLicenseInfo());
            SafeWriteFile(vbrDir, "configBackup.csv", GenerateConfigBackup());
            SafeWriteFile(vbrDir, "trafficRules.csv", GenerateTrafficRules());
            SafeWriteFile(vbrDir, "regkeys.csv", GenerateRegKeys());
            SafeWriteFile(vbrDir, "ViProtected.csv", GenerateViProtected());
            SafeWriteFile(vbrDir, "ViUnprotected.csv", GenerateViUnprotected());
            SafeWriteFile(vbrDir, "VeeamSessionReport.csv", GenerateSessionReport());
            SafeWriteFile(vbrDir, "_UserRoles.csv", GenerateUserRoles());
            SafeWriteFile(vbrDir, "capTier.csv", GenerateCapTier());
            SafeWriteFile(vbrDir, "archTier.csv", GenerateArchTier());

            return vbrDir;
        }

        /// <summary>
        /// Create a minimal test data directory with only critical files
        /// </summary>
        public static string CreateMinimalTestDataDirectory(string basePath = null)
        {
            // Normalize base path to prevent path traversal
            basePath = Path.GetFullPath(basePath ?? Path.Combine(Path.GetTempPath(), "VhcTests_" + Guid.NewGuid().ToString()));
            var vbrDir = Path.Combine(basePath, "VBR");
            Directory.CreateDirectory(vbrDir);

            // Only critical files - using safe write method
            SafeWriteFile(vbrDir, "vbrinfo.csv", GenerateVbrInfo());
            SafeWriteFile(vbrDir, "Servers.csv", GenerateServers());
            SafeWriteFile(vbrDir, "Proxies.csv", GenerateProxies());
            SafeWriteFile(vbrDir, "Repositories.csv", GenerateRepositories());
            SafeWriteFile(vbrDir, "_Jobs.csv", GenerateJobs());

            return vbrDir;
        }

        /// <summary>
        /// Create a specific CSV file in the given directory
        /// </summary>
        public static void CreateCsvFile(string directory, string fileName, string content)
        {
            var safePath = Path.GetFullPath(directory);
            var safeFileName = Path.GetFileName(fileName);

            if (!Directory.Exists(safePath))
            {
                Directory.CreateDirectory(safePath);
            }

            File.WriteAllText(Path.Combine(safePath, safeFileName), content);
        }

        /// <summary>
        /// Clean up a test data directory
        /// </summary>
        public static void CleanupTestDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }

        #endregion
    }
}
