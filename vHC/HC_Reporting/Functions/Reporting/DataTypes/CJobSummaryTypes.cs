// <copyright file="CJobSummaryTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    /// <summary>
    /// Represents summary information for a Veeam job including statistics about sessions, performance, and data sizes.
    /// </summary>
    public class CJobSummaryTypes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CJobSummaryTypes"/> class.
        /// </summary>
        public CJobSummaryTypes()
        {
        }

        public int SessionCount { get; set; }

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

        public int WaitCount { get; set; }

        public string MaxWait { get; set; }

        public string AvgWait { get; set; }
    }
}