using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.vsac.VbrHost;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Reporting.vsac
{
    internal class Class1
    {
        public Class1() { }
        public void init()
        {
            ScanVbrHost();

        }
        private void ScanVbrHost()
        {
            CVbrHostInfo hi = new();
            
        }

    }
}
