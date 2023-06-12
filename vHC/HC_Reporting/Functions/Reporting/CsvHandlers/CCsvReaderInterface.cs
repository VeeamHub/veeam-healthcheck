using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    internal interface ICCsvReaderInterface : IDisposable
    {

        CCsvReader Reader { get; }

        string FileName { get; }
    }
}
