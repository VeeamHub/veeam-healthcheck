// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace VeeamHealthCheck.CsvHandlers
{
    class CWaitsCsv
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
