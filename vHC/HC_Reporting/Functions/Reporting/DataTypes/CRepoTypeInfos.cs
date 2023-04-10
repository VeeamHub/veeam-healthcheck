// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    class CRepoTypeInfos
    {
        public string RepoName { get; set; }
        public int maxArchiveTasks { get; set; }
        public string isUnlimitedTaks { get; set; }
        public int dataRateLimit { get; set; }
        public string autoDetectAffinity { get; set; }
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
        public string IsUnavailable { get; set; }
        public string Group { get; set; }
        public string UseNfsOnMountHost { get; set; }
        public string VersionOfCreation { get; set; }
        public string Tag { get; set; }
        public string IsTemporary { get; set; }
        public string TypeDisplay { get; set; }
        public string IsRotatedDriveRepository { get; set; }
        public string EndPointCryptoKeyId { get; set; }
        public string Options { get; set; }
        public string HasBackupChainLengthLimitation { get; set; }
        public string IsSanSnapshotOnly { get; set; }
        public string IsDedupStorage { get; set; }
        public string SplitStoragesPerVm { get; set; }
        public string IsImmutabilitySupported { get; set; }
        public int Ram { get; set; }
        public int Cores { get; set; }
        public string IsAutoGateway { get; set; }
        public string Povisioning { get; set; }
        public int FreeSPace { get; set; }
        public int TotalSpace { get; set; }
        public int MaxTasks { get; set; }
        public string IsDecompress { get; set; }
        public string AlignBlocks { get; set; }
        public string GateHosts { get; set; }
        public CRepoTypeInfos()
        {

        }
    }
}
