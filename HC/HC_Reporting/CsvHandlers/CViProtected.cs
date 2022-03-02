using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.CsvHandlers
{
    class CViProtected
    {
        //Name,PowerState,ProvisionedSize,UsedSize,Path,Type
        [Index(0)]
        public string Name { get; set; }
        [Index(1)]
        public string PowerState { get; set; }
        [Index(2)]
        public string ProvisionedSize { get; set; }
        [Index(3)]
        public string UsedSize { get; set; }
        [Index(4)]
        public string Path { get; set; }
        [Index(5)]
        public string Type { get; set; }

    }
}
