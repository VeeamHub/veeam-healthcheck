using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    class JobSessionSummaryRow
    {
        public JobSessionSummaryRow()
        {

        }
        public string Name { get; set; }
        public string Repository { get; set; }
        public decimal SourceSize { get; set; }
        public decimal RestorePoints { get; set; }
    }
}
