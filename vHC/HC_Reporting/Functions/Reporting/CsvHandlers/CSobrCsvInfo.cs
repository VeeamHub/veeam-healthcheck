// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CSobrCsvInfo
    {
        // 
        [Index(0)]
        public string PolicyType { get; set; }

        [Index(1)]
        public string Extents { get; set; }

        [Index(2)]
        public string UsePerVMBackupFiles { get; set; }

        [Index(3)]
        public string PerformFullWhenExtentOffline { get; set; }

        [Index(4)]
        public string EnableCapacityTier { get; set; }

        [Index(5)]
        public string OperationalRestorePeriod { get; set; }

        [Index(6)]
        public string OverridePolicyEnabled { get; set; }

        [Index(7)]
        public string OverrideSpaceThreshold { get; set; }

        [Index(8)]
        public string OffloadWindowOptions { get; set; }

        [Index(9)]
        public string CapacityExtent { get; set; }

        [Index(10)]
        public string EncryptionEnabled { get; set; }

        [Index(11)]
        public string EncryptionKey { get; set; }

        [Index(12)]
        public string CapacityTierCopyPolicyEnabled { get; set; }

        [Index(13)]
        public string CapacityTierMovePolicyEnabled { get; set; }

        [Index(14)]
        public string ArchiveTierEnabled { get; set; }

        [Index(15)]
        public string ArchiveExtent { get; set; }

        [Index(16)]
        public string ArchivePeriod { get; set; }

        [Index(17)]
        public string CostOptimizedArchiveEnabled { get; set; }

        [Index(18)]
        public string ArchiveFullBackupModeEnabled { get; set; }

        [Index(19)]
        public string PluginBackupsOffloadEnabled { get; set; }

        [Index(20)]
        public string CopyAllPluginBackupsEnabled { get; set; }

        [Index(21)]
        public string CopyAllMachineBackupsEnabled { get; set; }

        [Index(22)]
        public string Id { get; set; }

        [Index(23)]
        public string Name { get; set; }

        [Index(24)]
        public string Description { get; set; }

        [Index(25)]
        public string ArchiveTierEncryptionEnabled { get; set; }
    }
}
