// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CProxyTypeInfos
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Info { get; set; }

        public string HostId { get; set; }

        public string Host { get; set; }

        public string Type { get; set; }

        public string IsDisabled { get; set; }

        public string Options { get; set; }

        public int MaxTasksCount { get; set; }

        public string UseSsl { get; set; }

        public string FailoverToNetwork { get; set; }

        public string TransportMode { get; set; }

        public string ChosenVm { get; set; }

        public string ChassisType { get; set; }

        public int Cores { get; set; }

        public int Ram { get; set; }

        public string CacheSize { get; set; }

        public string CachePath { get; set; }

        public string Provisioning { get; set; }


        public CProxyTypeInfos()
        {

        }

    }
}
