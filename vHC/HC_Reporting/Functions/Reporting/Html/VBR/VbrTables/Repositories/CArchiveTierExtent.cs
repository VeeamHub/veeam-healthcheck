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

        public bool CostOptimizedEnabled { get; set; }

        public bool FullBackupModeEnabled { get; set; }

        public bool EncryptionEnabled { get; set; }

        public string GatewayMode { get; set; }

        public string GatewayServer { get; set; }
    }
}
