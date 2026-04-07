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
""Normal"",""55555555-5555-5555-5555-555555555555"",""77777777-8888-9999-aaaa-bbbbbbbbbbbb"",""Azure-Archive-Blob"",""AzureArchive"",""{immutableValue}"",""Direct"",""vbr-server-01.test.local""";
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
            // 16-column schema matching CProxyCsvInfos [Index(0..15)] and actual PS script output
            // Type is "VMware" (PS script converts Vi → VMware); TransportMode "Auto" for test assertions
            return @"""Id"",""Name"",""Description"",""Info"",""HostId"",""Host"",""Type"",""IsDisabled"",""Options"",""MaxTasksCount"",""UseSsl"",""FailoverToNetwork"",""TransportMode"",""IsVbrProxy"",""ChosenVm"",""ChassisType""
""11111111-2222-3333-4444-555555555555"",""Proxy-01"",""Test VMware proxy"",""Veeam.Backup.Model.CBackupProxyInfo"",""11111111-2222-3333-4444-555555555555"",""Proxy-01.domain.local"",""VMware"",""False"",""Veeam.Backup.Model.CDomViProxyOptions"",""4"",""False"",""True"",""Auto"","""","""",""HvVirtual""
""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"",""VBR-Server"",""VBR server as proxy"",""Veeam.Backup.Model.CBackupProxyInfo"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"",""VBR-Server.domain.local"",""VMware"",""False"",""Veeam.Backup.Model.CDomViProxyOptions"",""2"",""False"",""True"",""Auto"","""","""",""HvVirtual""";
        }

        /// <summary>
        /// Generate an empty Proxies.csv
        /// </summary>
        public static string GenerateEmptyProxies()
        {
            return @"""Id"",""Name"",""Description"",""Info"",""HostId"",""Host"",""Type"",""IsDisabled"",""Options"",""MaxTasksCount"",""UseSsl"",""FailoverToNetwork"",""TransportMode"",""IsVbrProxy"",""ChosenVm"",""ChassisType""";
        }

        #endregion

        #region Repositories.csv

        /// <summary>
        /// Generate a sample Repositories.csv
        /// </summary>
        public static string GenerateRepositories()
        {
            // 40-column schema matching CRepoCsvInfos [Index(0..39)]
            // Headers match actual PS script output (not all match property names):
            //   Options(maxtasks)=26, CachedTotalSpace=37, CachedFreeSpace=38, gatewayHosts=39
            // Booleans at indices 11,13,16,18,21,22,23,24,25
            return @"""Id"",""Name"",""HostId"",""Description"",""CreationTime"",""Path"",""FullPath"",""FriendlyPath"",""ShareCredsId"",""Type"",""Status"",""IsUnavailable"",""Group"",""UseNfsOnMountHost"",""VersionOfCreation"",""Tag"",""IsTemporary"",""TypeDisplay"",""IsRotatedDriveRepository"",""EndPointCryptoKeyId"",""Options"",""HasBackupChainLengthLimitation"",""IsSanSnapshotOnly"",""IsDedupStorage"",""SplitStoragesPerVm"",""IsImmutabilitySupported"",""Options(maxtasks)"",""Options(Unlimited Tasks)"",""Options(MaxArchiveTaskCount)"",""Options(CombinedDataRateLimit)"",""Options(Uncompress)"",""Options(OptimizeBlockAlign)"",""Options(RemoteAccessLimitation)"",""Options(EpEncryptionEnabled)"",""Options(OneBackupFilePerVm)"",""Options(IsAutoDetectAffinityProxies)"",""Options(NfsRepositoryEncoding)"",""CachedTotalSpace"",""CachedFreeSpace"",""gatewayHosts""
""aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"",""Default Backup Repository"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"",""Test repository"",""2020-01-01"",""C:\Backup"",""C:\Backup"",""C:\Backup"",""00000000-0000-0000-0000-000000000000"",""WinLocal"",""Ordinal"",""False"",""BackupRepository"",""False"",""12.0.0.0"","""",""False"","""",""False"",""00000000-0000-0000-0000-000000000000"",""Veeam.Backup.Model.CDomBackupRepositoryOptions"",""False"",""False"",""False"",""True"",""False"",""4"",""False"",""2"",""0"",""False"",""True"",""Everyone"",""False"",""True"",""True"",""Undefined"",""1024"",""512"",""""
""bbbbbbbb-1111-2222-3333-cccccccccccc"",""Hardened Repo"",""22222222-3333-4444-5555-666666666666"",""Linux hardened repository"",""2021-06-01"",""D:\HardenedBackup"",""D:\HardenedBackup"",""D:\HardenedBackup"",""00000000-0000-0000-0000-000000000000"",""LinuxHardened"",""Ordinal"",""False"",""BackupRepository"",""True"",""12.0.0.0"","""",""False"","""",""False"",""00000000-0000-0000-0000-000000000000"",""Veeam.Backup.Model.CDomBackupRepositoryOptions"",""False"",""False"",""False"",""True"",""True"",""8"",""False"",""2"",""0"",""False"",""True"",""Everyone"",""False"",""True"",""True"",""Undefined"",""2048"",""1600"",""""";
        }

        /// <summary>
        /// Generate repos with invalid data (free > total)
        /// </summary>
        public static string GenerateInvalidRepositories()
        {
            // 40-column schema matching CRepoCsvInfos [Index(0..39)]
            return @"""Id"",""Name"",""HostId"",""Description"",""CreationTime"",""Path"",""FullPath"",""FriendlyPath"",""ShareCredsId"",""Type"",""Status"",""IsUnavailable"",""Group"",""UseNfsOnMountHost"",""VersionOfCreation"",""Tag"",""IsTemporary"",""TypeDisplay"",""IsRotatedDriveRepository"",""EndPointCryptoKeyId"",""Options"",""HasBackupChainLengthLimitation"",""IsSanSnapshotOnly"",""IsDedupStorage"",""SplitStoragesPerVm"",""IsImmutabilitySupported"",""Options(maxtasks)"",""Options(Unlimited Tasks)"",""Options(MaxArchiveTaskCount)"",""Options(CombinedDataRateLimit)"",""Options(Uncompress)"",""Options(OptimizeBlockAlign)"",""Options(RemoteAccessLimitation)"",""Options(EpEncryptionEnabled)"",""Options(OneBackupFilePerVm)"",""Options(IsAutoDetectAffinityProxies)"",""Options(NfsRepositoryEncoding)"",""CachedTotalSpace"",""CachedFreeSpace"",""gatewayHosts""
""cccccccc-1111-2222-3333-dddddddddddd"",""Bad Repo"",""aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"",""Bad repo for testing"",""2020-01-01"",""C:\Backup"",""C:\Backup"",""C:\Backup"",""00000000-0000-0000-0000-000000000000"",""WinLocal"",""Ordinal"",""False"",""BackupRepository"",""False"",""12.0.0.0"","""",""False"","""",""False"",""00000000-0000-0000-0000-000000000000"",""Veeam.Backup.Model.CDomBackupRepositoryOptions"",""False"",""False"",""False"",""True"",""False"",""4"",""False"",""2"",""0"",""False"",""True"",""Everyone"",""False"",""True"",""True"",""Undefined"",""100"",""200"",""""";
        }

        #endregion

        #region _Jobs.csv

        /// <summary>
        /// Generate a sample _Jobs.csv
        /// </summary>
        public static string GenerateJobs()
        {
            // 42-column schema matching CJobCsvInfos [Index(0..41)]
            // Booleans at indices 9,26,28,30,32; doubles at 13,35; IsJobEnabled at 40, IsScheduleDisabled at 41 (Optional)
            return @"""Name"",""JobType"",""SheduleEnabledTime"",""ScheduleOptions"",""RestorePoints"",""RepoName"",""Algorithm"",""FullBackupScheduleKind"",""FullBackupDays"",""TransformFullToSyntethic"",""TransformIncrementsToSyntethic"",""TransformToSyntethicDays"",""PwdKeyId"",""OriginalSize"",""RetentionType"",""RetentionCount"",""RetainDaysToKeep"",""DeletedVmRetentionDays"",""DeletedVmRetention"",""CompressionLevel"",""Deduplication"",""BlockSize"",""IntegrityChecks"",""SpecificStorageEncryption"",""StgEncryptionEnabled"",""KeepFirstFullBackup"",""EnableFullBackup"",""BackupIsAttached"",""GfsWeeklyIsEnabled"",""GfsWeeklyCount"",""GfsMonthlyEnabled"",""GfsMonthlyCount"",""GfsYearlyEnabled"",""GfsYearlyCount"",""IndexingType"",""OnDiskGB"",""AAIPEnabled"",""VSSEnabled"",""VSSIgnoreErrors"",""GuestFSIndexingEnabled"",""IsJobEnabled"",""IsScheduleDisabled""
""Daily Backup"",""Backup"",""01/01/2026 01:00:00 a.m."",""Start time: [01/01/2026 1:00:00 a.m.]"",""14"",""Default Backup Repository"",""Increment"",""Daily"",""Sunday"",""False"",""False"",""Sunday"",""00000000-0000-0000-0000-000000000000"",""1099511627776"",""Days"",""14"",""14"",""14"",""False"",""5"",""True"",""KbBlockSize1024"",""True"",""False"",""True"",""False"",""False"",""False"",""False"",""4"",""False"",""1"",""False"",""1"",""None"",""102.5"",""False"",""True"",""False"","""",""True"",""False""
""Weekly Full"",""Backup"","""",""Start time: [07/01/2026 10:00:00 p.m.]"",""7"",""Default Backup Repository"",""Increment"",""Daily"",""Sunday"",""False"",""False"",""Sunday"",""enc-key-guid-1"",""0"",""Days"",""7"",""7"",""14"",""False"",""5"",""True"",""KbBlockSize1024"",""True"",""False"",""True"",""False"",""False"",""False"",""False"",""4"",""False"",""1"",""False"",""1"",""None"",""0"",""False"",""True"",""False"","""",""False"",""True""
""Copy to Cloud"",""SimpleBackupCopyPolicy"",""01/01/2026 03:00:00 a.m."",""Start time: [01/01/2026 3:00:00 a.m.]"",""30"",""Cloud Repository"",""Increment"",""Daily"",""Sunday"",""False"",""False"","""",""enc-key-guid-2"",""0"",""Days"",""30"",""30"",""14"",""False"",""5"",""True"",""KbBlockSize1024"",""True"",""False"",""True"",""False"",""False"",""False"",""False"",""4"",""False"",""1"",""False"",""1"",""None"",""65.4"",""False"",""True"",""True"","""",""True"",""False""";
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
