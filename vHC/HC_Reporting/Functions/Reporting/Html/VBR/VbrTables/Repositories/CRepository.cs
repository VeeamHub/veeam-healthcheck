
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VBR_Tables.Repositories
{
    internal class CRepository
    {
        public string Name { get; set; }
        public string SobrName { get; set; }
        public int MaxTasks { get; set; }
        public int Cores { get; set; }
        public int Ram { get; set; }
        public bool IsAutoGate { get; set; }
        public string Host { get; set; }
        public string Path { get; set; }
        public decimal FreeSpace { get; set; }
        public decimal TotalSpace { get; set; }
        public decimal FreeSpacePercent { get; set; }
        public bool IsDecompress { get; set; }
        public bool AlignBlocks { get; set; }
        public bool IsRotatedDrives { get; set; }
        public bool IsImmutabilitySupported { get; set; }
        public string Type { get; set; }
        public string Provisioning { get; set; }
        //public string GateHosts { get; set; }

    }
}
