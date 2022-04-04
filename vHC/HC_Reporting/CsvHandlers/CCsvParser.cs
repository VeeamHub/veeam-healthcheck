// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using VeeamHealthCheck;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Shared.Logging;


namespace VeeamHealthCheck.CsvHandlers
{
    class CCsvParser
    {
        private readonly CsvConfiguration _csvConfig;
        private CLogger log = MainWindow.log;

        //CSV Paths.
        private readonly string _sessionPath = CVariables.unsafeDir + @"\Raw_Data\VeeamSessionReport.csv";
        private readonly string _outPath = CVariables.unsafeDir + "\\Raw_Data";
        private readonly string _sobrExtReportName = "SOBRExtents";
        private readonly string _jobReportName = "Jobs";
        private readonly string _proxyReportName = "Proxies";
        private readonly string _repoReportName = "Repositories";
        private readonly string _serverReportName = "Servers";
        private readonly string _sobrReportName = "SOBRs";
        private readonly string _licReportName = "LicInfo";
        private readonly string _wanReportName = "WanAcc";
        private readonly string _cdpProxReportName = "CdpProxy";
        private readonly string _hvProxReportName = "HvProxy";
        private readonly string _nasProxReportName = "NasProxy";
        private readonly string _bnrInfoName = "vbrinfo";
        private readonly string _piReportName = "pluginjobs";
        private readonly string _bjobs = "bjobs";
        private readonly string _capTier = "capTier";
        private readonly string _trafficRules = "trafficRules";
        private readonly string _configBackup = "configBackup";
        private readonly string _regOptions = "regkeys";
        private readonly string _waits = "waits";
        private readonly string _ViProtected = "ViProtected";
        private readonly string _viUnprotected = "ViUnprotected";
        private readonly string _physProtected = "PhysProtected";
        private readonly string _physNotProtected = "PhysNotProtected";

        public CCsvParser()
        {
            _csvConfig =  GetCsvConfig();
        }
        public void Dispose() { }

        public IEnumerable<dynamic> GetDynamicRecs()
        {
            return FileFinder(_licReportName).GetRecords<dynamic>();
        }
        private IEnumerable<dynamic> GetDynamicCsvRecs(string file)
        {
            return FileFinder(file).GetRecords<dynamic>();
        }
        public IEnumerable<dynamic> GetPhysProtected()
        {
            return GetDynamicCsvRecs(_physProtected);
        }
        public IEnumerable<dynamic> GetPhysNotProtected()
        {
            return GetDynamicCsvRecs (_physNotProtected);
        }
        public IEnumerable<CJobSessionCsvInfos> SessionCsvParser()
        {
            try
            {
                var records = CReader(_sessionPath).GetRecords<CJobSessionCsvInfos>();
                return records;
            }
            catch(Exception e) { return null; }
        }
        public IEnumerable<CBnRCsvInfo> BnrCsvParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_bnrInfoName).GetRecords<CBnRCsvInfo>();
            return records;
        }
        public IEnumerable<CSobrExtentCsvInfos> SobrExtParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_sobrExtReportName).GetRecords<CSobrExtentCsvInfos>();
            return records;
        }
        public IEnumerable<CWaitsCsv> WaitsCsvReader()
        {
            return FileFinder(_waits).GetRecords<CWaitsCsv>();
        }

        public IEnumerable<CViProtected> ViProtectedReader()
        {
            return FileFinder(_ViProtected).GetRecords<CViProtected>();
        }
        public IEnumerable<CViProtected> ViUnProtectedReader()
        {
            return FileFinder(_viUnprotected).GetRecords<CViProtected>();
        }
        public IEnumerable<CRegOptionsCsv> RegOptionsCsvParser()
        {
            return FileFinder(_regOptions).GetRecords<CRegOptionsCsv>();

        }
        public IEnumerable<CConfigBackupCsv> ConfigBackupCsvParser()
        {
            return FileFinder(_configBackup).GetRecords<CConfigBackupCsv>();
            //return records;
        }
        public IEnumerable<CNetTrafficRulesCsv> NetTrafficCsvParser()
        {
            return FileFinder(_trafficRules).GetRecords<CNetTrafficRulesCsv>();
        }
        public IEnumerable<CCapTierCsv> CapTierCsvParser()
        {
            var records = FileFinder(_capTier).GetRecords<CCapTierCsv>();
            return records;
        }
        public IEnumerable<CPluginCsvInfo> PluginCsvParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_piReportName).GetRecords<CPluginCsvInfo>();
            return records;
        }
        public IEnumerable<CWanCsvInfos> WanParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_wanReportName).GetRecords<CWanCsvInfos>();
            return records;
        }
        public IEnumerable<CJobCsvInfos> JobCsvParser()
        {
            var r = FileFinder(_jobReportName).GetRecords<CJobCsvInfos>();
            return r;
        }
        public IEnumerable<CBjobCsv> BJobCsvParser()
        {
            var r = FileFinder(_bjobs).GetRecords<CBjobCsv>();
            return r;
        }
        public IEnumerable<CServerCsvInfos> ServerCsvParser()
        {
            var r = FileFinder(_serverReportName).GetRecords<CServerCsvInfos>();
            return r;
        }
        public IEnumerable<CProxyCsvInfos> ProxyCsvParser()
        {
            var r = FileFinder(_proxyReportName).GetRecords<CProxyCsvInfos>();
            return r;
        }
        public IEnumerable<CSobrCsvInfo> SobrCsvParser()
        {
            var r = FileFinder(_sobrReportName).GetRecords<CSobrCsvInfo>();
            return r;
        }

        public IEnumerable<CRepoCsvInfos> RepoCsvParser()
        {
            var r = FileFinder(_repoReportName).GetRecords<CRepoCsvInfos>();
            return r;
        }

        public IEnumerable<CCdpProxyCsvInfo> CdpProxCsvParser()
        {
            var r = FileFinder(_cdpProxReportName).GetRecords<CCdpProxyCsvInfo>();
            return r;
        }
        public IEnumerable<CHvProxyCsvInfo> HvProxCsvParser()
        {
            var r = FileFinder(_hvProxReportName).GetRecords<CHvProxyCsvInfo>();
            return r;
        }
        public IEnumerable<CFileProxyCsvInfo> NasProxCsvParser()
        {
            var r = FileFinder(_nasProxReportName).GetRecords<CFileProxyCsvInfo>();
            return r;
        }

        private CsvReader FileFinder(string file)
        {
            try
            {
                string[] files = Directory.GetFiles(_outPath);
                foreach (var f in files)
                {
                    FileInfo fi = new(f);
                    if (fi.Name.Contains(file))
                    {
                        var cr = CReader(f);
                        return cr;
                    }
                }
                //HardExit();
            }
            catch(Exception e)
            {
                string s = String.Format("File or Directory {0} not found!", _outPath + "\n" + e.Message);
                log.Error(s);
                MessageBox.Show(s);
               // HardExit();
            }
            //return HardExit();
            return null;
        }

        private CsvReader HardExit()
        {
            string msg = "Required files not found. Please use 'RUN' option. If issues persist, please contact your SE for assistance";
            log.Error(msg);
            MessageBox.Show(msg);
            Environment.Exit(0);
            return null;
        }
        private string FileFinders(string file)
        {
            try
            {
                string[] files = Directory.GetFiles(_outPath);
                foreach (var f in files)
                {
                    FileInfo fi = new(f);
                    if (fi.Name.Contains(file))
                    {
                        return f;
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                string s = String.Format("File or Directory {0} not found!", _outPath + "\n" + e.Message);
                log.Error(s);
                MessageBox.Show(s);
                return null;
            }

        }


        private CsvReader CReader(string csvToRead)
        {
            TextReader reader = new StreamReader(csvToRead);
            var csvReader = new CsvReader(reader, _csvConfig);
            return csvReader;
        }
        private CsvConfiguration GetCsvConfig()
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header.ToLower(),
                MissingFieldFound = null,
            };
            return config;
        }
    }
}
