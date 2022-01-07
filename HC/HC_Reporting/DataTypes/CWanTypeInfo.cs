// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DataTypes
{
    class CWanTypeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string HostId { get; set; }
        public string Options { get; set; }
        public CWanTypeInfo()
        {

        }
    }
}
