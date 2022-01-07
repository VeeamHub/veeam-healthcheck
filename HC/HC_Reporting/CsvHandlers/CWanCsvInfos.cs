﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.CsvHandlers
{
    class CWanCsvInfos
    {
        //"Id","Name","Description","HostId","Options"
        [Index(0)]
        public string Id { get; set; }
        [Index(1)]
        public string Name { get; set; }
        [Index(2)]
        public string Description { get; set; }
        [Index(3)]
        public string HostId { get; set; }
        [Index(4)]
        public string Options { get; set; }
    }
}
