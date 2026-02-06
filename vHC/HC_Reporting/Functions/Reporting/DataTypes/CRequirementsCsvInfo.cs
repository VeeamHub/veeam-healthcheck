namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CRequirementsTypeInfo
    {
        public string Server { get; set; }
        public string Type { get; set; }

        public int RequiredCores { get; set; }
        public int AvailableCores { get; set; }

        public int RequiredRamGb { get; set; }
        public int AvailableRamGb { get; set; }

        public int ConcurrentTasks { get; set; }
        public int SuggestedTasks { get; set; }

        public string Names { get; set; }

        public bool CoresOk => AvailableCores >= RequiredCores;
        public bool RamOk => AvailableRamGb >= RequiredRamGb;

        public bool CTOk => SuggestedTasks >= ConcurrentTasks;
    }
}