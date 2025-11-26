// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365
{
    public class CLocalRepos
    {
        [Index(0)]
        public string BoundProxy { get; set; }

        [Index(1)]
        public string Name { get; set; }

        [Index(2)]
        public string Description { get; set; }

        [Index(3)]
        public string Type { get; set; }

        [Index(4)]
        public string Path { get; set; }

        [Index(5)]
        public string ObjectRepo { get; set; }

        [Index(6)]
        public string Encryption { get; set; }

        [Index(7)]
        public string State { get; set; }

        [Index(8)]
        public string Capacity { get; set; }

        [Index(9)]
        public string Free { get; set; }

        [Index(10)]
        public string DataStored { get; set; }

        [Index(11)]
        public string CacheSpaceUsed { get; set; }

        [Index(12)]
        public string DailyChangeRate { get; set; }

        [Index(13)]
        public string Retention { get; set; }
    }
}
