// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// Represents a Performance Tier (primary) extent within a SOBR
    /// </summary>
    public class CPerformanceTierExtent : CRepository
    {
        public string Status { get; set; }

        public bool IsObjectLockEnabled { get; set; }

        public CPerformanceTierExtent()
        {
            this.TierType = "Performance";
        }
    }
}
