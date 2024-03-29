﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365
{
    public class CGlobalCsv
    {
        [Index(0)]
        public string LicenseStatus { get; set; }
        [Index(1)]
        public string LicenseExpiry { get; set; }
        [Index(2)]
        public string SupportExpiry { get; set; }
        [Index(3)]
        public string LicenseType { get; set; }
        [Index(4)]
        public string LicensedTo { get; set; }
        [Index(5)]
        public string LicenseContact { get; set; }
        [Index(6)]
        public string LicensedFor { get; set; }
        [Index(7)]
        public string LicensesUsed { get; set; }
        [Index(8)]
        public string GlobalFolderExclusions { get; set; }
        [Index(9)]
        public string GlobalRetExclusions { get; set; }
        [Index(10)]
        public string LogRetention { get; set; }
        [Index(11)]
        public string NotificationEnabled { get; set; }
        [Index(12)]
        public string NotifyOn { get; set; }
        [Index(13)]
        public string AutomaticUpdates { get; set; }
    }
}
