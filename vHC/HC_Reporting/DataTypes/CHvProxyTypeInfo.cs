﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DataTypes
{
    class CHvProxyTypeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string HostId { get; set; }
        public string Host { get; set; }
        public string Type { get; set; }
        public string IsDisabled { get; set; }
        public string Options { get; set; }
        public string MaxTasksCount { get; set; }
        public string Info { get; set; }

        public CHvProxyTypeInfo()
        {

        }
    }
}
