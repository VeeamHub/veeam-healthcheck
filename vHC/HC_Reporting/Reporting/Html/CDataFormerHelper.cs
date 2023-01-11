using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Reporting.Html
{
    internal class CDataFormerHelper
    {
        public static string IsProxy(bool proxybool)
        {
            if (proxybool) return "True";

            return "False";
        }
    }
}
