using CsvHelper.Configuration.Attributes;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CRequirementsCsvInfo
    {
        [Index(0)]
        public string Server { get; set; }

        [Index(1)]
        public string Type { get; set; }

        [Index(2)]
        public string RequiredCores { get; set; }

        [Index(3)]
        public string AvailableCores { get; set; }

        [Index(4)]
        public string RequiredRamGb { get; set; }

        [Index(5)]
        public string AvailableRamGb { get; set; }

        [Index(6)]
        public string ConcurrentTasks { get; set; }

        [Index(7)]
        public string SuggestedTasks { get; set; }

        [Index(8)]
        public string Names { get; set; }
    }
}