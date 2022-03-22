// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HC_Reporting.CsvHandlers
{
    class CJobCsvInfos
    {
        //											

        [Index(0)]
        public string Name { get; set; }
        [Index(1)]
        public string JobType { get; set; }
        [Index(2)]
        public string SheduleEnabledTime { get; set; }
        [Index(3)]
        public string ScheduleOptions { get; set; }
        [Index(4)]
        public string RestorePoints { get; set; }
        [Index(5)]
        public string RepoName { get; set; }
        [Index(6)]
        public string Algorithm { get; set; }
        [Index(7)]
        public string FullBackupScheduleKind { get; set; }
        [Index(8)]
        public string FullBackupDays { get; set; }
        [Index(9)]
        public string TransformFullToSyntethic { get; set; }
        [Index(10)]
        public string TransformIncrementsToSyntethic { get; set; }
        [Index(11)]
        public string TransformToSyntethicDays { get; set; }
        [Index(12)]
        public string PwdKeyId{ get; set; }
        [Index(13)]
        public string OriginalSize { get; set; }
    }
}
