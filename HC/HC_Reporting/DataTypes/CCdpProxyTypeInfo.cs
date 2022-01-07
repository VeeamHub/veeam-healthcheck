// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DataTypes
{
    class CCdpProxyTypeInfo
    {
        public string ServerId { get; set; }
        public string CacheSize { get; set; }
        public string CachePath { get; set; }
        public string IsEnabled { get; set; }
        public string SourceProxyTrafficPort { get; set; }
        public string TargetProxyTrafficPort { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string description { get; set; }

        public CCdpProxyTypeInfo()
        {

        }
    }
}
