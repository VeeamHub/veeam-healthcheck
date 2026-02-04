// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CProxyCsvInfos
    {
        [Index(0)]

        // [ColumnDetails()]
        public string Id { get; set; }

        [Index(1)]
        public string Name { get; set; }

        [Index(2)]
        public string Description { get; set; }

        [Index(3)]
        public string Info { get; set; }

        [Index(4)]
        public string HostId { get; set; }

        [Index(5)]
        public string Host { get; set; }

        [Index(6)]
        public string Type { get; set; }

        [Index(7)]
        public string IsDisabled { get; set; }

        [Index(8)]
        public string Options { get; set; }

        [Index(9)]
        public string MaxTasksCount { get; set; }

        [Index(10)]
        public string UseSsl { get; set; }

        [Index(11)]
        public string FailoverToNetwork { get; set; }

        [Index(12)]
        public string TransportMode { get; set; }

        [Index(13)]
        public string IsVbrProxy{get;set;}

        [Index(14)]
        public string ChosenVm { get; set; }

        [Index(15)]
        public string ChassisType { get; set; }

        public string ProxyName()
        {
            return null;
        }
    }
}
