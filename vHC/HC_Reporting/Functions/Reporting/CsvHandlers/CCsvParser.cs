// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.Proxies;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.Repositories;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;


namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CCsvParser
    {
        private readonly CsvConfiguration _csvConfig;
        private CLogger log = CGlobals.Logger;

        //CSV Paths.
        public readonly string _sessionPath = "VeeamSessionReport.csv";
        public string _outPath;// = CVariables.vbrDir;
        public readonly string _sobrExtReportName = "SOBRExtents";
        public readonly string _jobReportName = "Jobs"; 
        public readonly string _sobrReportName = "SOBRs";
        public readonly string _proxyReportName = "Proxies";
        public readonly string _repoReportName = "Repositories";
        public readonly string _serverReportName = "Servers";
        public readonly string _licReportName = "LicInfo";
        public readonly string _wanReportName = "WanAcc";
        public readonly string _cdpProxReportName = "CdpProxy";
        public readonly string _hvProxReportName = "HvProxy";
        public readonly string _nasProxReportName = "NasProxy";
        public readonly string _bnrInfoName = "vbrinfo";
        public readonly string _piReportName = "pluginjobs";
        public readonly string _bjobs = "bjobs";
        public readonly string _capTier = "capTier";
        public readonly string _trafficRules = "trafficRules";
        public readonly string _configBackup = "configBackup";
        public readonly string _regOptions = "regkeys";
        public readonly string _waits = "waits";
        public readonly string _ViProtected = "ViProtected";
        public readonly string _viUnprotected = "ViUnprotected";
        public readonly string _physProtected = "PhysProtected";
        public readonly string _physNotProtected = "PhysNotProtected";
        public readonly string _HvProtected = "HvProtected";
        public readonly string _HvUnprotected = "HvUnprotected";

        //VBO Files
        private readonly string _vboGlobalCsv = "Global";
        private readonly string _vboProxies = "Proxies";
        private readonly string _vboRBAC = "RBACRoles";
        private readonly string _vboRepositories = "LocalRepositories";
        private readonly string _vboSecurity = "Security";
        private readonly string _vboController = "Controller";
        private readonly string _vboControllerDriver = "ControllerDrives";
        private readonly string _vboJobSessions = "JobSessions";
        private readonly string _vboJobStats = "JobStats";
        private readonly string _vboObjectRepos = "ObjectRepositories";
        private readonly string _vboOrganizations = "Organizations";
        private readonly string _vboPermissions = "Permissions";
        private readonly string _vboProtectionStatus = "ProtectionStatus";
        private readonly string _vboLiceOverView = "LicenseOverviewReport";
        private readonly string _vboMailboxProtection = "MailboxProtection";
        private readonly string _StorageConsumption = "StorageConsumption";
        private readonly string _vboJobs = "Jobs";
        private readonly string _vboProcStat = "ProcessingStats";

        public CCsvParser()
        {
            _csvConfig = GetCsvConfig();
            Start(null);
        }
        public CCsvParser(string csvRepo)
        {
            _csvConfig = GetCsvConfig();
            Start(csvRepo);
        }
        private void Start(string csvRepo)
        {
            if (string.IsNullOrEmpty(csvRepo))
                _outPath = CVariables.vbrDir;
            else
                _outPath = csvRepo;
        }

        #region m365CsvParser
        public IEnumerable<CGlobalCsv> GetDynamicVboGlobal()
        {
            return GetDynamicVboGlobal(null);
        }
        public IEnumerable<CGlobalCsv> GetDynamicVboGlobal(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_vboGlobalCsv, CVariables.vb365dir).GetRecords<CGlobalCsv>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vb365dir);
                if (reader != null)
                    return reader.GetRecords<CGlobalCsv>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<dynamic> GetDynamicVboProxies()
        {
            return GetDynamicCsvRecs(_vboProxies, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynamicVboRbac()
        {
            return GetDynamicCsvRecs(_vboRBAC, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynamicVboJobs()
        {
            return GetDynamicCsvRecs(_vboJobs, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynamicVboProcStat()
        {
            return GetDynamicCsvRecs(_vboProcStat, CVariables.vb365dir);
        }
        public IEnumerable<CLocalRepos> GetDynamicVboRepo()
        {
            return FileFinder(_vboRepositories, CVariables.vb365dir).GetRecords<CLocalRepos>();
        }
        public IEnumerable<CSecurityCsv> GetDynamicVboSec()
        {
            return FileFinder(_vboSecurity, CVariables.vb365dir).GetRecords<CSecurityCsv>();
        }
        public IEnumerable<dynamic> GetDynVboController()
        {
            return GetDynamicCsvRecs(_vboController , CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboControllerDriver()
        {
            return GetDynamicCsvRecs(_vboControllerDriver, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboJobSess()
        {
            return GetDynamicCsvRecs(_vboJobSessions, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboJobStats()
        {
            return GetDynamicCsvRecs(_vboJobStats, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboObjRepo()
        {
            return GetDynamicCsvRecs(_vboObjectRepos    , CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboOrg()
        {
            return GetDynamicCsvRecs(_vboOrganizations, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboPerms()
        {
            return GetDynamicCsvRecs(_vboPermissions, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboProtStat()
        {
            return GetDynamicCsvRecs(_vboProtectionStatus, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboLicOver()
        {
            return GetDynamicCsvRecs(_vboLiceOverView, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboMbProtRep()
        {
            return GetDynamicCsvRecs(_vboMailboxProtection, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboMbStgConsumption()
        {
            return GetDynamicCsvRecs(_StorageConsumption, CVariables.vb365dir);
        }

        #endregion

        #region DynamicCsvParsers-VBR

        private IEnumerable<dynamic> GetDynamicCsvRecs(string file, string vbrOrVboPath)
        {
            var r = FileFinder(file, vbrOrVboPath);
            if (r == null) { return Enumerable.Empty<dynamic>(); }
            else
            {
                return FileFinder(file, vbrOrVboPath).GetRecords<dynamic>();

            }
        }
        public IEnumerable<dynamic> GetDynamicLicenseCsv()
        {
            return GetDynamicCsvRecs(_licReportName, CVariables.vbrDir);
            //return FileFinder(_licReportName).GetRecords<dynamic>();
        }
        public IEnumerable<dynamic> GetDynamicVbrInfo()
        {
            return GetDynamicCsvRecs(_bnrInfoName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicConfigBackup()
        {
            return GetDynamicCsvRecs(_configBackup, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetPhysProtected()
        {
            return GetDynamicCsvRecs(_physProtected, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetPhysNotProtected()
        {
            return GetDynamicCsvRecs(_physNotProtected, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicJobInfo()
        {
            return GetDynamicCsvRecs(_jobReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicBjobs()
        {
            try
            {
                return GetDynamicCsvRecs(_bjobs, CVariables.vbrDir);

            }
            catch(Exception ex) { return null; }
        }
        public IEnumerable<dynamic> GetDynamincConfigBackup()
        {
            return GetDynamicCsvRecs(_configBackup, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamincNetRules()
        {
            return GetDynamicCsvRecs(_trafficRules, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicRepo()
        {
            return GetDynamicCsvRecs(_repoReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicSobrExt()
        {
            return GetDynamicCsvRecs(_sobrExtReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicSobr()
        {
            return GetDynamicCsvRecs(_sobrReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicCapTier()
        {
            return GetDynamicCsvRecs(_capTier, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynViProxy()
        {
            return GetDynamicCsvRecs(_proxyReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynHvProxy()
        {
            return GetDynamicCsvRecs(_hvProxReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynNasProxy()
        {
            return GetDynamicCsvRecs(_nasProxReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynCdpProxy()
        {
            return GetDynamicCsvRecs(_cdpProxReportName, CVariables.vbrDir);
        }
        #endregion

        #region oldCsvParsers
        public IEnumerable<CJobSessionCsvInfos> SessionCsvParser()
        {
            return SessionCsvParser(null);
        }
        public IEnumerable<CJobSessionCsvInfos> SessionCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_sessionPath, CVariables.vbrDir).GetRecords<CJobSessionCsvInfos>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CJobSessionCsvInfos>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CBnRCsvInfo> BnrCsvParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_bnrInfoName, CVariables.vbrDir).GetRecords<CBnRCsvInfo>();
            return records;
        }
        public IEnumerable<CSobrExtentCsvInfos> SobrExtParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_sobrExtReportName, CVariables.vbrDir).GetRecords<CSobrExtentCsvInfos>();
            return records;
        }
        public IEnumerable<CWaitsCsv> WaitsCsvReader()
        {

            return FileFinder(_waits, CVariables.vbrDir).GetRecords<CWaitsCsv>();
        }

        public IEnumerable<CViProtected> ViProtectedReader()
        {
            return FileFinder(_ViProtected, CVariables.vbrDir).GetRecords<CViProtected>();
        }
        public IEnumerable<CViProtected> ViUnProtectedReader()
        {
            return FileFinder(_viUnprotected, CVariables.vbrDir).GetRecords<CViProtected>();
        }
        public IEnumerable<CViProtected> HvProtectedReader()
        {
            try
            {
                return FileFinder(_HvProtected, CVariables.vbrDir).GetRecords<CViProtected>();

            }
            catch(Exception e) { return null; }
        }
        public IEnumerable<CViProtected> HvUnProtectedReader()
        {
            return HvUnProtectedReader(null);
        }
        public IEnumerable<CViProtected> HvUnProtectedReader(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_HvUnprotected, CVariables.vbrDir).GetRecords<CViProtected>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CViProtected>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CRegOptionsCsv> RegOptionsCsvParser()
        {
            return FileFinder(_regOptions, CVariables.vbrDir).GetRecords<CRegOptionsCsv>();

        }
        public IEnumerable<CConfigBackupCsv> ConfigBackupCsvParser()
        {
            return FileFinder(_configBackup, CVariables.vbrDir).GetRecords<CConfigBackupCsv>();
            //return records;
        }
        public IEnumerable<CConfigBackupCsv> ConfigBackupCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_configBackup, CVariables.vbrDir).GetRecords<CConfigBackupCsv>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CConfigBackupCsv>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CNetTrafficRulesCsv> NetTrafficCsvParser()
        {
            return FileFinder(_trafficRules, CVariables.vbrDir).GetRecords<CNetTrafficRulesCsv>();
        }
        public IEnumerable<CCapTierCsv> CapTierCsvParser()
        {
            var records = FileFinder(_capTier, CVariables.vbrDir).GetRecords<CCapTierCsv>();
            return records;
        }
        public IEnumerable<CCapTierCsv> CapTierCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_capTier, CVariables.vbrDir).GetRecords<CCapTierCsv>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CCapTierCsv>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CPluginCsvInfo> PluginCsvParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_piReportName, CVariables.vbrDir).GetRecords<CPluginCsvInfo>();
            return records;
        }
        public IEnumerable<CPluginCsvInfo> PluginCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_piReportName, CVariables.vbrDir).GetRecords<CPluginCsvInfo>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CPluginCsvInfo>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CWanCsvInfos> WanParser()
        {
            //ExportAllCsv();
            var records = FileFinder(_wanReportName, CVariables.vbrDir).GetRecords<CWanCsvInfos>();
            return records;
        }
        public IEnumerable<CWanCsvInfos> WanParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_wanReportName, CVariables.vbrDir).GetRecords<CWanCsvInfos>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CWanCsvInfos>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CJobCsvInfos> JobCsvParser()
        {
            return JobCsvParser(null);
        }
        public IEnumerable<CJobCsvInfos> JobCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_jobReportName, CVariables.vbrDir).GetRecords<CJobCsvInfos>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CJobCsvInfos>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CBjobCsv> BJobCsvParser()
        {
            var r = FileFinder(_bjobs, CVariables.vbrDir).GetRecords<CBjobCsv>();
            return r;
        }
        public IEnumerable<CBjobCsv> BJobCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_bjobs, CVariables.vbrDir).GetRecords<CBjobCsv>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CBjobCsv>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CServerCsvInfos> ServerCsvParser()
        {
            return ServerCsvParser(null);
        }
        public IEnumerable<CServerCsvInfos> ServerCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_serverReportName, CVariables.vbrDir).GetRecords<CServerCsvInfos>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CServerCsvInfos>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        public IEnumerable<CProxyCsvInfos> ProxyCsvParser()
        {
            return ProxyCsvParser(null);
        }
        public IEnumerable<CProxyCsvInfos> ProxyCsvParser(string reportName)
        {
            CProxyCsvReader p = new();

            if (String.IsNullOrEmpty(reportName))
            {
                return p.ProxyCsvParser();
            }
            else
            {
                return p.ProxyCsvParser(reportName);
            }
        }
        public IEnumerable<CSobrCsvInfo> SobrCsvParser()
        {
            return SobrCsvParser(null);
        }
        public IEnumerable<CSobrCsvInfo> SobrCsvParser(string reportName)
        {
            CSobrCsvReader p = new(reportName);
            if (String.IsNullOrEmpty(reportName))
            {
                return p.SobrCsvParser();
            }
            else
            {
                return p.SobrCsvParser(reportName);
            }
        }

        public IEnumerable<CRepoCsvInfos> RepoCsvParser()
        {
            return RepoCsvParser(null);
        }
        public IEnumerable<CRepoCsvInfos> RepoCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName)){
                var r = FileFinder(_repoReportName, CVariables.vbrDir).GetRecords<CRepoCsvInfos>();
                return r;
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CRepoCsvInfos>().ToList();
                else throw new FileNotFoundException("Repo Csv Missing");
            }
        }
        public IEnumerable<dynamic> Test2()
        {
            return Test2(null);
        }
        public IEnumerable<dynamic> Test2(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_proxyReportName, CVariables.vbrDir).GetRecords<dynamic>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<dynamic>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }

        public IEnumerable<CCdpProxyCsvInfo> CdpProxCsvParser()
        {
            var r = FileFinder(_cdpProxReportName, CVariables.vbrDir).GetRecords<CCdpProxyCsvInfo>();
            return r;
        }
        public IEnumerable<CCdpProxyCsvInfo> CdpProxCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                var r = FileFinder(_cdpProxReportName, CVariables.vbrDir).GetRecords<CCdpProxyCsvInfo>();
                return r;
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CCdpProxyCsvInfo>().ToList();
                else throw new FileNotFoundException("CDP Csv Missing");
            }
        }
        public IEnumerable<CHvProxyCsvInfo> HvProxCsvParser()
        {
            var r = FileFinder(_hvProxReportName, CVariables.vbrDir).GetRecords<CHvProxyCsvInfo>();
            return r;
        }
        public IEnumerable<CHvProxyCsvInfo> HvProxCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                var r = FileFinder(_hvProxReportName, CVariables.vbrDir).GetRecords<CHvProxyCsvInfo>();
                return r;
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CHvProxyCsvInfo>().ToList();
                else throw new FileNotFoundException("HV Proxy Csv Missing");
            }
        }
        public IEnumerable<CFileProxyCsvInfo> NasProxCsvParser()
        {
            return NasProxCsvParser(null);
        }
        public IEnumerable<CFileProxyCsvInfo> NasProxCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_nasProxReportName, CVariables.vbrDir).GetRecords<CFileProxyCsvInfo>();
            }
            else
            {
                var reader = FileFinder(reportName, CVariables.vbrDir);
                if (reader != null)
                    return reader.GetRecords<CFileProxyCsvInfo>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
        #endregion

        #region localFunctions
        public void Dispose() { }

        private CsvReader FileFinder(string file, string vbrOrVboPath)
        {
            try
            {
                string[] files = Directory.GetFiles(vbrOrVboPath);
                foreach (var f in files)
                {
                    FileInfo fi = new(f);
                    if (fi.Name.Contains(file))
                    {
                        var cr = CReader(f);
                        return cr;
                    }
                }
            }
            catch (Exception e)
            {
                string s = string.Format("File or Directory {0} not found!", _outPath + "\n" + e.Message);
                log.Error(s);
                return null;
            }
            return null;
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
        #endregion
    }
}
