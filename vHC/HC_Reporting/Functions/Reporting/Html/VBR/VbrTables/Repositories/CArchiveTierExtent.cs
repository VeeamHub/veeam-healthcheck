// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    public class CArchiveTierExtent
    {
        public string SobrName { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public string Status { get; set; }

        public string OffloadPeriod { get; set; }

        public bool ArchiveTierEnabled { get; set; }

        public bool ImmutableEnabled { get; set; }

        public string ImmutablePeriod { get; set; }

        public bool SizeLimitEnabled { get; set; }

        public string SizeLimit { get; set; }

        public bool CostOptimizedEnabled { get; set; }

        public bool FullBackupModeEnabled { get; set; }

        public bool EncryptionEnabled { get; set; }
    }
}
