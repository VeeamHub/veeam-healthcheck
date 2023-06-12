using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers.Proxies
{
    internal class CProxyCsvReader
    {
        private CCsvReader _reader;
        public readonly string _proxyReportName = "Proxies";
        public CProxyCsvReader()
        {
            _reader = new CCsvReader(CVariables.vbrDir);
        }

        public IEnumerable<CProxyCsvInfos> ProxyCsvParser()
        {
            //var r = 
            return ProxyCsvParser(null);
        }
        public IEnumerable<CProxyCsvInfos> ProxyCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return _reader.FileFinder(_proxyReportName).GetRecords<CProxyCsvInfos>();
            }
            else
            {
                var reader = _reader.FileFinder(reportName);
                if (reader != null)
                    return reader.GetRecords<CProxyCsvInfos>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
    }
}
