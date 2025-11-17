using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers.VBR
{
    public class HealthCheckOptions
    {
        public bool Enabled { get; set; }
        public int ScheduleType { get; set; } // e.g., 1 for Monthly
        public List<int> SelectedDays { get; set; } // e.g., [6] for Saturday
        public int DayNumber { get; set; } // e.g., 4 for First
        public int DayOfWeek { get; set; } // e.g., 6 for Monday
        public string DayOfMonth { get; set; } // e.g., "1"
        public List<int> SelectedMonths { get; set; } // e.g., [0,1,2,...] for January, February, etc.
    }

    public class ScriptOptions
    {
        public bool PreScriptEnabled { get; set; }
        public string PreCommand { get; set; }
        public bool PostScriptEnabled { get; set; }
        public string PostCommand { get; set; }
        public List<string> Day { get; set; } // e.g., ["Saturday"]
        public string Periodicity { get; set; } // e.g., "Cycles"
        public int Frequency { get; set; } // e.g., 1
    }

    public class NotificationOptions
    {
        public bool EnableAdditionalNotification { get; set; }
        public List<string> AdditionalAddress { get; set; } // e.g., []
        public bool UseNotificationOptions { get; set; }
        public string NotificationSubject { get; set; }
        public bool NotifyOnSuccess { get; set; }
        public bool NotifyOnWarning { get; set; }
        public bool NotifyOnError { get; set; }
        public bool NotifyOnLastRetryOnly { get; set; }
        public bool EnableSnmpNotification { get; set; }
        public bool NotifyWhenWaitingForTape { get; set; }
        public bool EnableDailyNotification { get; set; }
        public string SendTime { get; set; } // e.g., "22:00:00"
    }

    public class StorageOptions
    {
        public string CompressionLevel { get; set; } // e.g., "High"
        public string StorageOptimizationType { get; set; } // e.g., "LocalTarget"
        public bool EncryptionEnabled { get; set; }
        public string EncryptionKey { get; set; } // e.g., null
    }

    public class NasBackupJob
    {
        public string JobType { get; set; }
        public double OnDiskGB { get; set; }
        public double SourceGB { get; set; }
        public string SecurityProcessingMode { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BackupObject { get; set; } // Note: CSV has "BakupObject" (typo)
        public string ShortTermBackupRepository { get; set; }
        public string ShortTermRetentionType { get; set; }
        public int ShortTermRetentionPeriod { get; set; }
        public bool LongTermRetentionPeriodEnabled { get; set; }
        public string LongTermBackupRepository { get; set; }
        public string LongTermRetentionType { get; set; }
        public int LongTermRetentionPeriod { get; set; }
        public string BackupArchivalOptions { get; set; }
        public bool EnableCopyMode { get; set; }
        public bool SecondaryTargetEnabled { get; set; }
        public string SecondaryTarget { get; set; }
        public string VersionRetentionOptions { get; set; } // JSON string
        public string StorageOptions { get; set; } // JSON string
        public string HealthCheckOptions { get; set; } // JSON string
        public string Extra { get; set; } // For the last column "\" in CSV
    }
}
