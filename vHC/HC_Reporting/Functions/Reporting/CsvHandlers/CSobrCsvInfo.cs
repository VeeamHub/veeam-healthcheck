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
        public bool UsePerVMBackupFiles { get; set; }
        [Index(3)]
        public bool PerformFullWhenExtentOffline { get; set; }
        [Index(4)]
        public bool EnableCapacityTier { get; set; }
        [Index(5)]
        public string OperationalRestorePeriod { get; set; }
        [Index(6)]
        public bool OverridePolicyEnabled { get; set; }
        [Index(7)]
        public string OverrideSpaceThreshold { get; set; }
        [Index(8)]
        public string OffloadWindowOptions { get; set; }
        [Index(9)]
        public string CapacityExtent { get; set; }
        [Index(10)]
        public bool EncryptionEnabled { get; set; }
        [Index(11)]
        public string EncryptionKey { get; set; }
        [Index(12)]
        public bool CapacityTierCopyPolicyEnabled { get; set; }
        [Index(13)]
        public bool CapacityTierMovePolicyEnabled { get; set; }
        [Index(14)]
        public bool ArchiveTierEnabled { get; set; }
        [Index(15)]
        public string ArchiveExtent { get; set; }
        [Index(16)]
        public string ArchivePeriod { get; set; }
        [Index(17)]
        public bool CostOptimizedArchiveEnabled { get; set; }
        [Index(18)]
        public bool ArchiveFullBackupModeEnabled { get; set; }
        [Index(19)]
        public bool PluginBackupsOffloadEnabled { get; set; }
        [Index(20)]
        public bool CopyAllPluginBackupsEnabled { get; set; }
        [Index(21)]
        public bool CopyAllMachineBackupsEnabled { get; set; }
        [Index(22)]
        public string Id { get; set; }
        [Index(23)]
        public string Name { get; set; }
        [Index(24)]
        public string Description { get; set; }
    }
}
