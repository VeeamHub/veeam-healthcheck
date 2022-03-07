using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.CsvHandlers
{
    class CPhysProtected
    {
        //"State","AgentStatus","AgentVersion","DriverStatus","DriverVersion","RebootRequired","IPAddress","LastConnected","OperatingSystem",
        //"OperatingSystemPlatform","OperatingSystemVersion","OperatingSystemUpdateVersion","ObjectId","Type","ParentId","ProtectionGroupId","Name","Id"
        [Index(0)]
        public string State { get; set; }
        [Index(1)]
        public string AgentStatus { get; set; }
        [Index(2)]
        public string AgentVersion { get; set; }
        [Index(3)]
        public string DriverStatus { get; set; }
        [Index(4)]
        public string DriverVersion { get; set; }
        [Index(5)]
        public string RebootRequired { get; set; }
        [Index(6)]
        public string IPAddress { get; set; }
        [Index(7)]
        public string LastConnected { get; set; }
        [Index(8)]
        public string OperatingSystem { get; set; }
        [Index(9)]
        public string OperatingSystemPlatform { get; set; }
        [Index(10)]
        public string OperatingSystemVersion { get; set; }
        [Index(11)]
        public string OperatingSystemUpdateVersion { get; set; }
        [Index(12)]
        public string ObjectId { get; set; }
        [Index(13)]
        public string Type { get; set; }
        [Index(14)]
        public string ParentId { get; set; }
        [Index(15)]
        public string ProtectionGroupId { get; set; }
        [Index(16)]
        public string Name { get; set; }
        [Index(17)]
        public string Id { get; set; }

    }
}
