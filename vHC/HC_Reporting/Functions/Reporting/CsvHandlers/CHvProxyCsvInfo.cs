// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CHvProxyCsvInfo
    {
        // "Id","Name","Description","HostId","Host","Type","IsDisabled","Options","MaxTasksCount","Info"
        [Index(0)]
        public string Id { get; set; }

        [Index(1)]
        public string Name { get; set; }

        [Index(2)]
        public string Description { get; set; }

        [Index(3)]
        public string HostId { get; set; }

        [Index(4)]
        public string Host { get; set; }

        [Index(5)]
        public string Type { get; set; }

        [Index(6)]
        public string IsDisabled { get; set; }

        [Index(7)]
        public string Options { get; set; }

        [Index(8)]
        public string MaxTasksCount { get; set; }

        [Index(9)]
        public string Info { get; set; }
    }
}
