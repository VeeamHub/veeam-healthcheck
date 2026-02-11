// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// Represents an Archive Tier extent configuration within a SOBR
    /// </summary>
    public class CArchiveTierExtent
    {
        /// <summary>
        /// Parent SOBR name
        /// </summary>
        public string SobrName { get; set; }

        /// <summary>
        /// Archive tier extent name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Archive tier repository type (e.g., AmazonS3, AzureBlob, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Archive tier status
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Archive retention period in days before deletion
        /// </summary>
        public string RetentionPeriod { get; set; }

        /// <summary>
        /// Whether archive tier is enabled on the SOBR
        /// </summary>
        public bool ArchiveTierEnabled { get; set; }

        /// <summary>
        /// Whether immutability is enabled on archive tier
        /// </summary>
        public bool ImmutableEnabled { get; set; }

        /// <summary>
        /// Archive tier immutability period (how long data is locked)
        /// </summary>
        public string ImmutablePeriod { get; set; }

        /// <summary>
        /// Whether size limiting is enabled
        /// </summary>
        public bool SizeLimitEnabled { get; set; }

        /// <summary>
        /// Maximum size limit for archive tier
        /// </summary>
        public string SizeLimit { get; set; }

        /// <summary>
        /// Whether cost optimization is enabled
        /// </summary>
        public bool CostOptimizedEnabled { get; set; }

        /// <summary>
        /// Whether full backup mode is enabled for archive tier
        /// </summary>
        public bool FullBackupModeEnabled { get; set; }

        public CArchiveTierExtent()
        {
        }
    }
}
