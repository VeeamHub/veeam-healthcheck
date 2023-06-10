// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CFileProxyCsvInfo
    {
        //"Id","Description","Server","ConcurrentTaskNumber"
        [Index(0)]
        public string ConcurrentTaskNumber { get; set; }

        [Index(1)]
        public string Host { get; set; }
        [Index(2)]
        public string HostId { get; set; }
    }
}
