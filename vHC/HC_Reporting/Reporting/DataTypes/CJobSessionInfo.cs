// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck
{
    class CJobSessionInfo
    {
        public string Name { get; set; }
        public int minTime { get; set; }
        public int maxTime { get; set; }
        public int avgTime { get; set; }
        public double minTimeHr { get; set; }
        public double maxTimeHr { get; set; }
        public double avgTimeHr { get; set; }
        public string JobName { get; set; }
        public string VmName { get; set; }
        public string Status { get; set; }
        public string IsRetry { get; set; }
        public string ProcessingMode { get; set; }
        public string JobDuration { get; set; }
        public string TaskDuration { get; set; }
        public string Alg { get; set; }
        public DateTime CreationTime { get; set; }
        public double BackupSize { get; set; }
        public double DataSize { get; set; }
        public string DedupRatio { get; set; }
        public string CompressionRatio { get; set; }
        public string Bottleneck { get; set; }
        public string PrimaryBottleneck { get; set; }
        public string JobType { get; set; }
        public CJobSessionInfo(string name, int min, int max, int avg)
        {
            //Name = name;
            //minTime = min;
            //maxTime = max;
            //avgTime = avg;
        }
        public CJobSessionInfo()
        {

        }
        public void Dispose() { }

    }
}
