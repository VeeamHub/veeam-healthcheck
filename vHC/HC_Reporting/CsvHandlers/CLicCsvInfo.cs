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
    class CLicCsvInfo
    {
        [Index(0)]
        public string LicensedTo { get; set; }
        [Index(1)]
        public string Edition { get; set; }
        [Index(2)]
        public string ExpirationDate { get; set; }
        [Index(3)]
        public string Type { get; set; }
        [Index(4)]
        public string SupportId { get; set; }
        [Index(5)]
        public string SupportExpirationDate { get; set; }
        [Index(6)]
        public string AutoUpdateEnabled { get; set; }
        [Index(7)]
        public string FreeAgentInstanceConsumptionEnabled { get; set; }
        [Index(8)]
        public string CloudConnect { get; set; }
        [Index(9)]
        public string LicensedSockets { get; set; }
        [Index(10)]
        public string UsedSockets { get; set; }
        [Index(11)]
        public string RemainingSockets { get; set; }
        [Index(12)]
        public string LicensedInstances { get; set; }
        [Index(13)]
        public string UsedInstances { get; set; }
        [Index(14)]
        public string NewInstances { get; set; }
        [Index(15)]
        public string RentalInstances { get; set; }
        [Index(16)]
        public string LicensedCapacityTb { get; set; }

        [Index(17)]
        public string UsedCapacityTb { get; set; }
        [Index(18)]
        public string Status { get; set; }
    }
}
