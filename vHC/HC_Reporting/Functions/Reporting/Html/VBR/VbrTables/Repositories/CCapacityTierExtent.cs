// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    public class CCapacityTierExtent : CRepository
    {
        public string ParentSobrId { get; set; }

        public bool SizeLimitEnabled { get; set; }

        public string SizeLimit { get; set; }

        public bool CopyModeEnabled { get; set; }

        public bool MoveModeEnabled { get; set; }

        public int MovePeriodDays { get; set; }

        public bool EncryptionEnabled { get; set; }
    }
}
