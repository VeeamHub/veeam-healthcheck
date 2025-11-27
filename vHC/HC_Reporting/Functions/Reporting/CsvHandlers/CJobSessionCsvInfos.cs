// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CJobSessionCsvInfos
    {
        [Index(0)]
        public string JobName { get; set; }

        [Index(1)]
        public string VmName { get; set; }

        [Index(2)]
        public string Status { get; set; }

        [Index(3)]
        public string IsRetry { get; set; }

        [Index(4)]
        public string ProcessingMode { get; set; }

        [Index(5)]
        public string JobDuration { get; set; }

        [Index(6)]
        public string TaskDuration { get; set; }

        [Index(7)]
        public string Alg { get; set; }

        [Index(8)]
        public string CreationTime { get; set; }

        [Index(9)]
        public string BackupSize { get; set; }

        [Index(10)]
        public string DataSize { get; set; }

        [Index(11)]
        public string DedupRatio { get; set; }

        [Index(12)]
        public string CompressionRation { get; set; }

        [Index(13)]
        public string BottleneckDetails { get; set; }

        [Index(14)]
        public string PrimaryBottleneck { get; set; }

        [Index(15)]
        public string JobType { get; set; }
    }
}