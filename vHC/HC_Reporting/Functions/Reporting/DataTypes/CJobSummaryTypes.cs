// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CJobSummaryTypes
    {
        public int sessionCount { get; set; }

        public int Fails { get; set; }

        public int Retries { get; set; }

        public double SuccessRate { get; set; }

        public string MinJobTime { get; set; }

        public string MaxJobTime { get; set; }

        public string AvgJobTime { get; set; }

        public string JobName { get; set; }

        public int ItemCount { get; set; }

        public double MinBackupSize { get; set; }

        public double MaxBackupSize { get; set; }

        public double AvgBackupSize { get; set; }

        public double MinDataSize { get; set; }

        public double MaxDataSize { get; set; }

        public double UsedVmSizeTB { get; set; }

        public double AvgDataSize { get; set; }

        public double AvgChangeRate { get; set; }

        public string JobType { get; set; }

        public int waitCount { get; set; }

        public string maxWait { get; set; }

        public string avgwait { get; set; }

        public CJobSummaryTypes()
        {

        }
    }
}
