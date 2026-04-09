// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Functions.Monitor
{
    public class MonitorLastRunStatus
    {
        public DateTime? Timestamp { get; set; }
        public string Summary { get; set; } = string.Empty;
        public int FindingCount { get; set; }
    }
}
