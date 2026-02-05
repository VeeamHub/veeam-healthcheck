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

        public CsvReader FileFinder(string token, string outpath)
        {
            try
            {
                var files = Directory.GetFiles(outpath, "*.csv", SearchOption.AllDirectories);

                string wanted1 = "_" + token + ".csv";   // localhost_Servers.csv
                string wanted2 = token + ".csv";         // Servers.csv (if ever)

                var match = files.FirstOrDefault(p =>
                    p.EndsWith(wanted1, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(p), wanted2, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    return null;

                this.log.Info($"looking for VBR CSV at: {match}");
                return this.CReader(match);
            }
            catch (Exception e)
            {
                this.log.Error($"File or Directory {outpath} not found!\n{e.Message}");
                return null;
            }
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
