// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CViProtected
    {
        // Name,PowerState,ProvisionedSize,UsedSize,Path,Type
        [Index(0)]
        public string Name { get; set; }

        [Index(1)]
        public string PowerState { get; set; }

        [Index(2)]
        public string ProvisionedSize { get; set; }

        [Index(3)]
        public string UsedSize { get; set; }

        [Index(4)]
        public string Path { get; set; }

        [Index(5)]
        public string Type { get; set; }
    }
}
