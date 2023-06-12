// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CJobTypeInfos
    {
        public string Name { get; set; }
        public string JobType { get; set; }
        public string JobId { get; set; }
        public string SheduleEnabledTime { get; set; }
        public string ScheduleOptions { get; set; }
        public int RestorePoints { get; set; }
        public string RepoName { get; set; }
        public string Algorithm { get; set; }
        public string FullBackupScheduleKind { get; set; }
        public string FullBackupDays { get; set; }
        public string TransformFullToSyntethic { get; set; }
        public string TransformIncrementsToSyntethic { get; set; }
        public string TransformToSyntethicDays { get; set; }
        public string ActualSize { get; set; }
        public string EncryptionEnabled { get; set; }
        public CJobTypeInfos()
        {

        }
    }
}
