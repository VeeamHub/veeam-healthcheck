// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CNetTrafficRulesCsv
    {
        //		TargetIPStart	TargetIPEnd	EncryptionEnabled	ThrottlingEnabled	ThrottlingUnit	ThrottlingValue	ThrottlingWindowEnabled	ThrottlingWindowOptions	Name	Id

        [Index(0)]
        public string SourceIpStart { get; set; }

        [Index(1)]
        public string SourceIPEnd { get; set; }

        [Index(2)]
        public string TargetIpStart { get; set; }

        [Index(3)]
        public string TargetIpEnd { get; set; }

        [Index(4)]
        public string EncryptionEnabled { get; set; }

        [Index(5)]
        public string ThrottlingEnabled { get; set; }

        [Index(6)]
        public string ThrottlingUnit { get; set; }

        [Index(7)]
        public string ThrottlingValue { get; set; }

        [Index(8)]
        public string ThrottlingWindowEnabled { get; set; }

        [Index(9)]
        public string ThrottlingWindowOptions { get; set; }

        [Index(10)]
        public string Name { get; set; }

        [Index(11)]
        public string Id { get; set; }
    }
}
