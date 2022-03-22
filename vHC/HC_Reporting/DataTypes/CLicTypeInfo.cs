// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DataTypes
{
    class CLicTypeInfo
    {
        public string LicensedTo { get; set; }
        public string Edition { get; set; }
        public string ExpirationDate { get; set; }
        public string Type { get; set; }
        public string SupportId { get; set; }
        public string SupportExpirationDate { get; set; }
        public string AutoUpdateEnabled { get; set; }
        public string FreeAgentInstanceConsumptionEnabled { get; set; }
        public string CloudConnect { get; set; }
        public int LicensedSockets { get; set; }
        public int UsedSockets { get; set; }
        public int RemainingSockets { get; set; }
        public int LicensedInstances { get; set; }
        public int UsedInstances { get; set; }
        public int NewInstances { get; set; }
        public int RentalInstances { get; set; }
        public string LicensedCapacityTb { get; set; }
        public string UsedCapacityTb { get; set; }
        public string Status { get; set; }

        public CLicTypeInfo()
        {

        }
    }
}
