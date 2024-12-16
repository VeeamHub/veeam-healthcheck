using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CProtectedWorkloads
    {
        public VirtualWorkloads vmWareWorkloads { get; set; }
        public VirtualWorkloads hyperVWorkloads { get; set; }
        public List<NasWorkloads> nasWorkloads { get; set; }
        public PhysicalWorkloads physicalWorkloads { get; set; }
        public List<EntraWorkloads> entraWorkloads { get; set; }

        public CProtectedWorkloads()
        {
            vmWareWorkloads = new VirtualWorkloads();
            hyperVWorkloads = new VirtualWorkloads();
            nasWorkloads = new List<NasWorkloads>();
            physicalWorkloads = new PhysicalWorkloads();
            entraWorkloads = new List<EntraWorkloads>();
        }

    }

    public class VirtualWorkloads
    {
        public int TotalVMs { get; set; }
        public int TotalProtectedVMs { get; set; }
        public int TotalNotProtectedVMs { get; set; }
        public int Dupes { get; set; }
    }
    public class NasWorkloads
    {
        public string FileShareType { get; set; }
        public string TotalShareSize { get; set; }
        public double TotalFilesCount { get; set; }
        public double TotalFoldersCount { get; set; }

    }
    public class PhysicalWorkloads
    {
        public int ProtectedAsPhysical { get; set; }
        public int Total { get; set; }
        public int NotProtected { get; set; }
        public int Protected { get; set; }
    }
    public class EntraWorkloads
    {
        public string TenantName { get; set; }
        public string CacheRepoName { get; set; }

    }
}