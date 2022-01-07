// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.CsvHandlers
{
    class CPluginCsvInfo
    {
        //"PluginType","Id","Name","Type","LastRun","LastResult","LastState","NextRun","TargetRepositoryId","Description","IsEnabled"
        [Index(0)]
        public string JobType { get; set; }
        [Index(1)]
        public string PluginType { get; set; }
        [Index(2)]
        public string Id { get; set; }
        [Index(3)]
        public string Name { get; set; }
        [Index(4)]
        public string Type { get; set; }
        [Index(5)]
        public string LastRun { get; set; }
        [Index(6)]
        public string LastResult { get; set; }
        [Index(7)]
        public string LastState { get; set; }
        [Index(8)]
        public string NextRun { get; set; }
        [Index(9)]
        public string TargetRepositoryId { get; set; }
        [Index(10)]
        public string Description { get; set; }
        [Index(11)]
        public string IsEnabled { get; set; }
    }
}
