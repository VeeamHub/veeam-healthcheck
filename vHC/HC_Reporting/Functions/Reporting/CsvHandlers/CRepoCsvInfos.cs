﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CRepoCsvInfos
    {
        [Index(0)]
        public string Id { get; set; }
        [Index(1)]
        public string Name { get; set; }
        [Index(2)]
        public string HostId { get; set; }
        [Index(3)]
        //public string MountHostId { get; set; }
        public string Description { get; set; }
        [Index(4)]
        public string CreationTime { get; set; }
        [Index(5)]
        public string Path { get; set; }
        [Index(6)]
        public string FullPath { get; set; }
        [Index(7)]
        public string FriendlyPath { get; set; }
        [Index(8)]
        public string ShareCredsId { get; set; }
        [Index(9)]
        public string Type { get; set; }
        [Index(10)]
        public string Status { get; set; }
        [Index(11)]
        public bool IsUnavailable { get; set; }
        [Index(12)]
        public string Group { get; set; }
        [Index(13)]
        public bool UseNfsOnMountHost { get; set; }
        [Index(14)]
        public string VersionOfCreation { get; set; }
        [Index(15)]
        public string Tag { get; set; }
        [Index(16)]
        public bool IsTemporary { get; set; }
        [Index(17)]
        public string TypeDisplay { get; set; }
        [Index(18)]
        public bool IsRotatedDriveRepository { get; set; }
        [Index(19)]
        public string EndPointCryptoKeyId { get; set; }
        [Index(20)]
        public string Options { get; set; }
        [Index(21)]
        public bool HasBackupChainLengthLimitation { get; set; }
        [Index(22)]
        public bool IsSanSnapshotOnly { get; set; }
        [Index(23)]
        public bool IsDedupStorage { get; set; }
        [Index(24)]
        public bool SplitStoragesPerVm { get; set; }
        [Index(25)]
        public bool IsImmutabilitySupported { get; set; }
        [Index(26)]
        public string MaxTasks { get; set; }
        [Index(27)]
        public string UnlimitedTasks { get; set; }
        [Index(28)]
        public string MaxArchiveTasks { get; set; }
        [Index(29)]
        public string DataRateLimit { get; set; }
        [Index(30)]
        public string Uncompress { get; set; }
        [Index(31)]
        public string AlignBlock { get; set; }
        [Index(32)]
        public string RemoteAccessLimitation { get; set; }
        [Index(33)]
        public string EpEncryptionEnabled { get; set; }
        [Index(34)]
        public string OneBackupFilePerVM { get; set; }
        [Index(35)]
        public string AutoDetectAffinity { get; set; }
        [Index(36)]
        public string NfsRepoEncoding { get; set; }
        [Index(37)]
        public string TotalSpace { get; set; }
        [Index(38)]
        public string FreeSpace { get; set; }
        [Index(39)]
        public string GateHosts { get; set; }
    }
}
