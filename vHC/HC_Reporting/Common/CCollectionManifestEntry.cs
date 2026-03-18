// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
namespace VeeamHealthCheck.Shared
{
    /// <summary>
    /// Represents a single row from the _CollectionManifest.csv produced by Get-VBRConfig.ps1.
    /// Each row records the outcome of one collector function run.
    /// </summary>
    public class CCollectionManifestEntry
    {
        public string Name { get; set; }
        public bool Success { get; set; }
        public double DurationSeconds { get; set; }
        public string Error { get; set; }
        public string Timestamp { get; set; }
    }
}
