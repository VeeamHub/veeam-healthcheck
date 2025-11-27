// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Analysis.DataModels
{
    public class BackupServer
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public int Cores { get; set; }

        public int RAM { get; set; }

        public bool ConfigBackupEnabled { get; set; }

        public string ConfigBackupLastResult { get; set; }

        public bool ConfigBackupEncryption { get; set; }

        public string ConfigBackupTarget { get; set; }

        public int ConfigBackupRetentionPoints { get; set; }

        public bool HasProxyRole { get; set; }

        public bool HasRepoRole { get; set; }

        public bool HasWanAccRole { get; set; }

        public bool HasFixes { get; set; }

        public string FixIds { get; set; }

        // Config DB Info
        public string DbType { get; set; }

        public string DbHostName { get; set; }

        public string DbVersion { get; set; }

        public string Edition { get; set; }

        public int DbCores { get; set; }

        public int DbRAM { get; set; }

        public bool IsLocal { get; set; }
    }
}
