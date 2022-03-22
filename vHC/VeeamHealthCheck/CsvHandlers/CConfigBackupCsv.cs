// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.CsvHandlers
{
    class CConfigBackupCsv
    {
        [Index(0)]
        public string Enabled { get; set; }
        [Index(1)]
        public string Repository { get; set; }
        [Index(2)]
        public string ScheduleOptions { get; set; }
        [Index(3)]
        public string RestorePointsToKeep { get; set; }
        [Index(4)]
        public string EncryptionOptions { get; set; }
        [Index(5)]
        public string NotificationOptions { get; set; }
        [Index(6)]
        public string NextRun { get; set; }
        [Index(7)]
        public string Target { get; set; }
        [Index(8)]
        public string Type { get; set; }
        [Index(9)]
        public string LastResult { get; set; }
        [Index(10)]
        public string LastState { get; set; }
        [Index(11)]
        public string Id { get; set; }
        [Index(12)]
        public string Name { get; set; }
        [Index(13)]
        public string Description { get; set; }
    }
}
