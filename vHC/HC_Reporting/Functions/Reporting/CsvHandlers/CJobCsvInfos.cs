// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CJobCsvInfos
    {
        // 
        [Index(0)]
        public string Name { get; set; }

        [Index(1)]
        public string JobType { get; set; }

        [Index(2)]
        public string SheduleEnabledTime { get; set; }

        [Index(3)]
        public string ScheduleOptions { get; set; }

        [Index(4)]
        public string RestorePoints { get; set; }

        [Index(5)]
        public string RepoName { get; set; }

        [Index(6)]
        public string Algorithm { get; set; }

        [Index(7)]
        public string FullBackupScheduleKind { get; set; }

        [Index(8)]
        public string FullBackupDays { get; set; }

        [Index(9)]
        public bool TransformFullToSyntethic { get; set; }

        [Index(10)]
        public string TransformIncrementsToSyntethic { get; set; }

        [Index(11)]
        public string TransformToSyntethicDays { get; set; }

        [Index(12)]
        public string PwdKeyId { get; set; }

        [Index(13)]
        public double OriginalSize { get; set; }

        // convert each of these to an object with indexing label: RetentionType	RetentionCount	RetainDaysToKeep	DeletedVmRetentionDays	DeletedVmRetention	CompressionLevel	Deduplication	BlockSize	IntegrityChecks	SpecificStorageEncryption	StgEncryptionEnabled	KeepFirstFullBackup	EnableFullBackup	BackupIsAttached	GfsWeeklyIsEnabled	GfsWeeklyCount	GfsMonthlyEnabled	GfsMonthlyCount	GfsYearlyEnabled	GfsYearlyCount	IndexingType
        [Index(14)]
        public string RetentionType { get; set; }

        [Index(15)]
        public string RetentionCount { get; set; }

        [Index(16)]
        public string RetainDaysToKeep { get; set; }

        [Index(17)]
        public string DeletedVmRetentionDays { get; set; }

        [Index(18)]
        public string DeletedVmRetention { get; set; }

        [Index(19)]
        public string CompressionLevel { get; set; }

        [Index(20)]
        public string Deduplication { get; set; }

        [Index(21)]
        public string BlockSize { get; set; }

        [Index(22)]
        public string IntegrityChecks { get; set; }

        [Index(23)]
        public string SpecificStorageEncryption { get; set; }

        [Index(24)]
        public string StgEncryptionEnabled { get; set; }

        [Index(25)]
        public string KeepFirstFullBackup { get; set; }

        [Index(26)]
        public bool EnableFullBackup { get; set; }

        [Index(27)]
        public string BackupIsAttached { get; set; }

        [Index(28)]
        public bool GfsWeeklyIsEnabled { get; set; }

        [Index(29)]
        public string GfsWeeklyCount { get; set; }

        [Index(30)]
        public bool GfsMonthlyEnabled { get; set; }

        [Index(31)]
        public string GfsMonthlyCount { get; set; }

        [Index(32)]
        public bool GfsYearlyEnabled { get; set; }

        [Index(33)]
        public string GfsYearlyCount { get; set; }

        [Index(34)]
        public string IndexingType { get; set; }
    }
}
