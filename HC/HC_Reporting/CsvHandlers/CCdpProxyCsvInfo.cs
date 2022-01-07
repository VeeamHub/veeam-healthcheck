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
    class CCdpProxyCsvInfo
    {
        //"ServerId","CacheSize","CachePath","IsEnabled","SourceProxyTrafficPort","TargetProxyTrafficPort","Id","Name",
        //"Description"
        [Index(0)]
        public string ServerId { get; set; }
        [Index(1)]
        public string CacheSize { get; set; }
        [Index(2)]
        public string CachePath { get; set; }
        [Index(3)]
        public string IsEnabled { get; set; }
        [Index(4)]
        public string SourceProxyTrafficPort { get; set; }
        [Index(5)]
        public string TargetProxyTrafficPort { get; set; }
        [Index(6)]
        public string Id { get; set; }
        [Index(7)]
        public string Name { get; set; }
        [Index(8)]
        public string description { get; set; }

    }

}
