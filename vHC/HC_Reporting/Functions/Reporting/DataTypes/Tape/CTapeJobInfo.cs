using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.Tape
{
    public class CTapeJobInfo
    {
        [Index(0)]
        public string FullBackupPolicy { get; set; }

        [Index(1)]
        public string Object { get; set; }

        [Index(2)]
        public string ProcessIncrementalBackup { get; set; }

        [Index(3)]
        public string ScheduleOptions { get; set; }

        [Index(4)]
        public string WaitForBackupJobs { get; set; }

        [Index(5)]
        public string WaitPeriod { get; set; }

        [Index(6)]
        public string GFSScheduleOptions { get; set; }

        [Index(7)]
        public string AlwaysCopyFromLatestFull { get; set; }

        [Index(8)]
        public string ParallelDriveOptions { get; set; }

        [Index(9)]
        public string EjectCurrentMedium { get; set; }

        [Index(10)]
        public string ExportCurrentMediaSet { get; set; }

        [Index(11)]
        public string ExportDays { get; set; }

        [Index(12)]
        public string FullBackupMediaPool { get; set; }

        [Index(13)]
        public string IncrementalBackupMediaPool { get; set; }

        [Index(14)]
        public string UseHardwareCompression { get; set; }

        [Index(15)]
        public string NotificationOptions { get; set; }

        [Index(16)]
        public string JobScriptOptions { get; set; }

        [Index(17)]
        public string Enabled { get; set; }

        [Index(18)]
        public string NextRun { get; set; }

        [Index(19)]
        public string Target { get; set; }

        [Index(20)]
        public string Type { get; set; }

        [Index(21)]
        public string LastResult { get; set; }

        [Index(22)]
        public string LastState { get; set; }

        [Index(23)]
        public string Id { get; set; }

        [Index(24)]
        public string Name { get; set; }

        [Index(25)]
        public string Description { get; set; }
    }
}
