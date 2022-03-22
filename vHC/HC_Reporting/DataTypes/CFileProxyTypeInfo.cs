// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DataTypes
{
    class CFileProxyTypeInfo
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string Server { get; set; }
        public string ConcurrentTaskNumber { get; set; }

        public CFileProxyTypeInfo()
        {
                
        }
    }
}
