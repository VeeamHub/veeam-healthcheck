// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CArchiveTierCsv
    {
        // Status	ParentId	RepoId	Name	ArchiveType	BackupImmutabilityEnabled
        [Index(0)]
        public string Status { get; set; }

        [Index(1)]
        public string ParentId { get; set; }

        [Index(2)]
        public string RepoId { get; set; }

        [Index(3)]
        public string Name { get; set; }

        [Index(4)]
        public string ArchiveType { get; set; }

        [Index(5)]
        public string BackupImmutabilityEnabled { get; set; }

        [Index(6)]
        public string GatewayMode { get; set; }

        [Index(7)]
        public string GatewayServer { get; set; }
    }
}
