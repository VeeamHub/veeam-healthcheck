// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    class CExtentInfo
    {
        public string sobrName { get; set; }

        public string RepoName { get; set; }
        public int maxTasks { get; set; }
        public int maxArchiveTasks { get; set; }
        public bool isUnlimitedTaks { get; set; }
        public int dataRateLimit { get; set; }
        public bool unCompress { get; set; }
        public bool perVm { get; set; }
        public bool autoDetectAffinity { get; set; }

        public string path { get; set; }
        public bool isUnavailable { get; set; }
        public string HostName { get; set; }
        public string type { get; set; }
        public bool isRotated { get; set; }
        public bool dedupe { get; set; }
        public bool immute { get; set; }
        public int Ram { get; set; }
        public int Cores { get; set; }
        public string HostId { get; set; }
        public string IsAutoGateway { get; set; }
        public string Povisioning { get; set; }

        public int FreeSPace { get; set; }
        public int TotalSpace { get; set; }
        public CExtentInfo()
        {

        }
    }
}
