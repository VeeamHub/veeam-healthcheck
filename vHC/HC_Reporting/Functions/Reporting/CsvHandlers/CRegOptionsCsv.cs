// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CRegOptionsCsv
    {
        [Index(0)]
        public string KeyName { get; set; }
        [Index(1)]
        public string Value { get; set; }
    }
}
