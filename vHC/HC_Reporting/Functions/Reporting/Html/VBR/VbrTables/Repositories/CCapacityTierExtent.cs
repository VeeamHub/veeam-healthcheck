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
        public new bool SizeLimitEnabled { get; set; }

        /// <summary>
        /// Maximum size limit for capacity tier storage
        /// </summary>
        public new string SizeLimit { get; set; }

        public CCapacityTierExtent()
        {
            this.TierType = "Capacity";
        }
    }
}
