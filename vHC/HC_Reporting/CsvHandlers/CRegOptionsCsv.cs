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
    class CRegOptionsCsv
    {
        [Index(0)]
        public string KeyName { get; set; }
        [Index(1)]
        public string Value { get; set; }
    }
}
