// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CBjobCsv
    {

        [Index(0)]
        public string JobType { get; set; }
        [Index(1)]
        public string Name { get; set; }
        [Index(2)]
        public string RepositoryId { get; set; }
        [Index(3)]
        public string actualSize { get; set; }

    }
}
