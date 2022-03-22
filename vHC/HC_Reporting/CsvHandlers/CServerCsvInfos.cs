// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace VeeamHealthCheck.CsvHandlers
{
    class CServerCsvInfos
    {


        [Index(0)]
        public string Info { get; set; }
        [Index(1)]
        public string ParentId { get; set; }
        [Index(2)]
        public string Id { get; set; }
        [Index(3)]
        public string Uid { get; set; }
        [Index(4)]
        public string Name { get; set; }
        [Index(5)]
        public string Reference { get; set; }
        [Index(6)]
        public string Description { get; set; }
        [Index(7)]
        public string IsUnavailable { get; set; }
        [Index(8)]
        public string Type { get; set; }
        [Index(9)]
        public string ApiVersion { get; set; }
        [Index(10)]
        public string PhysHostId { get; set; }
        [Index(11)]
        public string ProxyServicesCreds { get; set; }
        [Index(12)]
        public string Cores { get; set; }
        [Index(13)]
        public string CPU { get; set; }
        [Index(14)]
        public string Ram { get; set; }
    }
}
