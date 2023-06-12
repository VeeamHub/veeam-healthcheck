using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers.Repositories
{
    internal class CSobrCsvReader : ICCsvReaderInterface
    {
        private string _file;
        private CCsvReader _reader = new(CVariables.vbrDir);
        CCsvReader ICCsvReaderInterface.Reader => _reader = new(CVariables.vbrDir);
        public readonly string _sobrReportName = "SOBRs";



        string ICCsvReaderInterface.FileName => _file = "";

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public CSobrCsvReader(string fileName)
        {
            _file = fileName;
        }
        public IEnumerable<CSobrCsvInfo> SobrCsvParser()
        {
            return SobrCsvParser(null);
        }
        public IEnumerable<CSobrCsvInfo> SobrCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return _reader.FileFinder(_sobrReportName).GetRecords<CSobrCsvInfo>();
            }
            else
            {
                var reader = _reader.FileFinder(reportName);
                if (reader != null)
                    return reader.GetRecords<CSobrCsvInfo>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
