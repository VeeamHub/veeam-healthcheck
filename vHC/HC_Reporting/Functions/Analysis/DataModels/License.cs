using System;

namespace VeeamHealthCheck.Functions.Analysis.DataModels
{
    internal class License
    {
        public string LicensedTo { get; set; }

        public string Edition { get; set; }

        public string Status { get; set; }

        public string Type { get; set; }

        public string LicensedInstances { get; set; }

        public string UsedInstances { get; set; }

        public string NewInstances { get; set; }

        public string RentalInstances { get; set; }

        public string LicensedSockets { get; set; }

        public string UsedSockets { get; set; }

        public string LicensedNas { get; set; }

        public string UsedNas { get; set; }

        public string ExpirationDate { get; set; }

        public string SupportExpirationDate { get; set; }

        public string CloudConnect { get; set; }
    }
}
