using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CCsvReader
    {
        private readonly CLogger log = CGlobals.Logger;
        private readonly CsvConfiguration csvConfig;

        // private string _outPath;// = CVariables.vbrDir;
        public CCsvReader()
        {
            this.csvConfig = this.GetCsvConfig();

            // _outPath = vbrOrVboPath;
        }

        public CsvReader VbrCsvReader(string file)
        {
            return this.FileFinder(file, CVariables.vbrDir);
        }

        public CsvReader VboCsvReader(string file)
        {
            return this.FileFinder(file, CVariables.vb365dir);
        }

        public CsvReader FileFinder(string file, string outpath)
        {
            try
            {
                // Search recursively to find CSV files in server/timestamp subdirectories
                string[] files = Directory.GetFiles(outpath, "*.*", SearchOption.AllDirectories);
                
                // Try to find exact match first
                foreach (var f in files)
                {
                    FileInfo fi = new(f);
                    if (fi.Name.Contains(file))
                    {
                        this.log.Info($"looking for VBR CSV at: {f}");
                        var cr = this.CReader(f);
                        return cr;
                    }
                }
            }
            catch (Exception e)
            {
                string s = string.Format("File or Directory {0} not found!", outpath + "\n" + e.Message);
                this.log.Error(s);
                return null;
            }

            return null;
        }

        private CsvReader CReader(string csvToRead)
        {
            TextReader reader = new StreamReader(csvToRead);
            var csvReader = new CsvReader(reader, this.csvConfig);
            return csvReader;
        }

        private CsvConfiguration GetCsvConfig()
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header.ToLower()
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace("?", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("/", string.Empty)
                .Replace("#", string.Empty),
                MissingFieldFound = null,
            };
            return config;
        }
    }
}
