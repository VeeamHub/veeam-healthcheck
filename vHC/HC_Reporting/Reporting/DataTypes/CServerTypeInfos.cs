// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DataTypes
{
    class CServerTypeInfos
    {
        public string Info { get; set; }
        public Guid ParentId { get; set; }
        public string Id { get; set; }
        public Guid Uid { get; set; }
        public string Name { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }
        public string IsUnavailable { get; set; }
        public string Type { get; set; }
        public string ApiVersion { get; set; }
        public string PhysHostId { get; set; }
        public string ProxyServicesCreds { get; set; }
        public int Cores { get; set; }
        public int CpuCount { get; set; }
        public int Ram { get; set; }
        public int RepoTasks { get; set; }
        public int ProxyTasks { get; set; }
        public string OSInfo { get; set; }

        public CServerTypeInfos()
        {

        }
    }
}
