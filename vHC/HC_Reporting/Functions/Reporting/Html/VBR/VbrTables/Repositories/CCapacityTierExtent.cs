// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// Represents a Capacity Tier extent within a SOBR
    /// </summary>
    public class CCapacityTierExtent : CRepository
    {
        /// <summary>
        /// Parent SOBR ID for linking to SOBR configuration
        /// </summary>
        public string ParentSobrId { get; set; }

        /// <summary>
        /// Capacity tier specific maximum archive task count
        /// </summary>
        public int MaxArchiveTaskCount { get; set; }

        /// <summary>
        /// Whether size limiting is enabled on capacity tier
        /// </summary>
        public bool SizeLimitEnabled { get; set; }

        /// <summary>
        /// Maximum size limit for capacity tier storage
        /// </summary>
        public string SizeLimit { get; set; }

        /// <summary>
        /// Whether copy mode is enabled for capacity tier
        /// </summary>
        public bool CopyModeEnabled { get; set; }

        /// <summary>
        /// Whether move mode is enabled for capacity tier
        /// </summary>
        public bool MoveModeEnabled { get; set; }

        /// <summary>
        /// Move period in days for capacity tier
        /// </summary>
        public int MovePeriodDays { get; set; }

        public CCapacityTierExtent()
        {
            this.TierType = "Capacity";
        }
    }
}
