// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    class CBnRCsvInfo
    {
        [Index(0)]
        public string Version { get; set; }
        [Index(1)]
        public string Fixes { get; set; }
        [Index(2)]
        public string SqlServer { get; set; }
        [Index(3)]
        public string Instance { get; set; }
        [Index(4)]
        public string PgHost { get; set; }
        [Index(5)]
        public string PgDb { get; set; }
        [Index(6)]
        public string MsHost { get; set; }
        [Index(7)]
        public string MsDb { get; set; }
        [Index(8)]
        public string DbType { get; set; }
    }
}
