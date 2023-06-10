// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CWaitsCsv
    {
        //JobName	StartTime	EndTime	Duration

        [Index(0)]
        public string JobName { get; set; }
        [Index(1)]
        public string StartTime { get; set; }
        [Index(2)]
        public string EndTime { get; set; }
        [Index(3)]
        public string Duration { get; set; }
    }
}
