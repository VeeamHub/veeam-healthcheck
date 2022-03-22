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
    class CBnRCsvInfo
    {
        [Index(0)]
        public string Version { get; set; }
        [Index(1)]
        public string Fixes { get; set; }
        [Index(2)]
        public string SqlServer { get; set; }
        [Index(3)]
        public string Instance { get; set; }
    }
}
