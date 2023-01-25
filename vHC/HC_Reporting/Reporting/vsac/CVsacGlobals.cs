using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Reporting.vsac
{
    internal class CVsacGlobals
    {
        public static CLogger LOG = new("vsac.log");
    }
}
