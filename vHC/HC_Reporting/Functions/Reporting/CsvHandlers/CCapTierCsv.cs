// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CCapTierCsv
    {
        // Status	Type	Immute	immutabilityperiod	SizeLimitEnabled	SizeLimit	RepoId
        [Index(0)]
        public string Status { get; set; }

        [Index(1)]
        public string Type { get; set; }

        [Index(2)]
        public string Immute { get; set; }

        [Index(3)]
        public string ImmutePeriod { get; set; }

        [Index(4)]
        public string SizeLimitEnabled { get; set; }

        [Index(5)]
        public string SizeLimit { get; set; }

        [Index(6)]
        public string RepoId { get; set; }

        [Index(7)]
        public string ParentId { get; set; }
    }
}
