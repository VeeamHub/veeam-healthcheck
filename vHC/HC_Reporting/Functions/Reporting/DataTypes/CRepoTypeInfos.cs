// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CRepoTypeInfos
    {
        public string RepoName { get; set; }

        public int maxArchiveTasks { get; set; }

        public string isUnlimitedTaks { get; set; }

        public int dataRateLimit { get; set; }

        public bool autoDetectAffinity { get; set; }

        public string Info { get; set; }

        public string Host { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string SobrName { get; set; }

        public string HostId { get; set; }

        public string MountHostId { get; set; }

        public string Description { get; set; }

        public string CreationTime { get; set; }

        public string Path { get; set; }

        public string FullPath { get; set; }

        public string FriendlyPath { get; set; }

        public string ShareCredsId { get; set; }

        public string Type { get; set; }

        public string Status { get; set; }

        public bool IsUnavailable { get; set; }

        public string Group { get; set; }

        public string UseNfsOnMountHost { get; set; }

        public string VersionOfCreation { get; set; }

        public string Tag { get; set; }

        public bool IsTemporary { get; set; }

        public string TypeDisplay { get; set; }

        public bool IsRotatedDriveRepository { get; set; }

        public string EndPointCryptoKeyId { get; set; }

        public string Options { get; set; }

        public bool HasBackupChainLengthLimitation { get; set; }

        public bool IsSanSnapshotOnly { get; set; }

        public bool IsDedupStorage { get; set; }

        public bool SplitStoragesPerVm { get; set; }

        public bool IsImmutabilitySupported { get; set; }

        public int Ram { get; set; }

        public int Cores { get; set; }

        public bool IsAutoGateway { get; set; }

        public string Povisioning { get; set; }

        public int FreeSPace { get; set; }

        public int TotalSpace { get; set; }

        public int MaxTasks { get; set; }

        public bool IsDecompress { get; set; }

        public bool AlignBlocks { get; set; }

        public string GateHosts { get; set; }

        public bool ObjectLockEnabled { get; set; }

        public CRepoTypeInfos()
        {

        }
    }
}
