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
//using VeeamHealthCheck.Functions.Reporting.CsvHandlers.Proxies;
//using VeeamHealthCheck.Functions.Reporting.CsvHandlers.Repositories;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.DataTypes.NAS;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CCsvParser
    {
        private CLogger log = CGlobals.Logger;
        private readonly CCsvReader _vbrReader = new();
        private readonly CCsvReader _vboReader = new();

        //CSV Paths.
        public  string _sessionPath = "VeeamSessionReport.csv";
        public string _outPath;// = CVariables.vbrDir;
        public readonly string _sobrExtReportName = "SOBRExtents";
        public readonly string _jobReportName = "_Jobs";
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
        public readonly string _malware = "malware";
        public readonly string _nasFileData = "NasFileData";
        public readonly string _nasShareSize = "NasSharesize";
        public readonly string _nasObjectSize = "NasObjectSourceStorageSize";

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
            Start(null);
        }
        public CCsvParser(string csvRepo)
        {
            Start(csvRepo);
        }
        private void Start(string csvRepo)
        {
            if (string.IsNullOrEmpty(csvRepo))
                _outPath = CVariables.vbrDir;
            else
                _outPath = csvRepo;
        }
        private IEnumerable<dynamic> VbrGetDynamicCsvRecs(string file, string vbrOrVboPath)
        {
            var res = VbrFileReader(file);
            if (res != null)
                return res.GetRecords<dynamic>();
            return null;
        }
        private IEnumerable<dynamic> VboGetDynamicCsvRecs(string file, string vbrOrVboPath)
        {
            var res = VboFileReader(file);
            if (res != null)
                return res.GetRecords<dynamic>();
            return null;
        }
        #region m365CsvParser

        public IEnumerable<CGlobalCsv> GetDynamicVboGlobal()
        {
            var res = VboFileReader(_vboGlobalCsv);
            if (res != null)
                return res.GetRecords<CGlobalCsv>();
            return null;
        }
        public IEnumerable<dynamic> GetDynamicVboProxies()
        {
            return VboGetDynamicCsvRecs(_vboProxies, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynamicVboRbac()
        {
            return VboGetDynamicCsvRecs(_vboRBAC, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynamicVboJobs()
        {
            return VboGetDynamicCsvRecs(_vboJobs, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynamicVboProcStat()
        {
            return VboGetDynamicCsvRecs(_vboProcStat, CVariables.vb365dir);
        }
        public IEnumerable<CLocalRepos> GetDynamicVboRepo()
        {
            //return VboGetDynamicCsvRecs(_vboRepositories, CVariables.vb365dir);
            var res = VboFileReader(_vboRepositories);
            if (res != null)
                return res.GetRecords<CLocalRepos>();
            return null;
            //return FileFinder(_vboRepositories, CVariables.vb365dir).GetRecords<CLocalRepos>();
        }
        public IEnumerable<CSecurityCsv> GetDynamicVboSec()
        {
            //return FileFinder(_vboSecurity, CVariables.vb365dir).GetRecords<CSecurityCsv>();
            var res = VboFileReader(_vboSecurity);
            if (res != null)
                return res.GetRecords<CSecurityCsv>();
            return null;
        }
        public IEnumerable<dynamic> GetDynVboController()
        {
            return VboGetDynamicCsvRecs(_vboController, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboControllerDriver()
        {
            return VboGetDynamicCsvRecs(_vboControllerDriver, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboJobSess()
        {
            return VboGetDynamicCsvRecs(_vboJobSessions, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboJobStats()
        {
            return VboGetDynamicCsvRecs(_vboJobStats, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboObjRepo()
        {
            return VboGetDynamicCsvRecs(_vboObjectRepos, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboOrg()
        {
            return VboGetDynamicCsvRecs(_vboOrganizations, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboPerms()
        {
            return VboGetDynamicCsvRecs(_vboPermissions, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboProtStat()
        {
            return VboGetDynamicCsvRecs(_vboProtectionStatus, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboLicOver()
        {
            return VboGetDynamicCsvRecs(_vboLiceOverView, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboMbProtRep()
        {
            return VboGetDynamicCsvRecs(_vboMailboxProtection, CVariables.vb365dir);
        }
        public IEnumerable<dynamic> GetDynVboMbStgConsumption()
        {
            return VboGetDynamicCsvRecs(_StorageConsumption, CVariables.vb365dir);
        }

        #endregion

        #region DynamicCsvParsers-VBR



        public IEnumerable<dynamic> GetDynamicLicenseCsv()
        {
            return VbrGetDynamicCsvRecs(_licReportName, CVariables.vbrDir);
            //return FileFinder(_licReportName).GetRecords<dynamic>();
        }
        public IEnumerable<dynamic> GetDynamicVbrInfo()
        {
            return VbrGetDynamicCsvRecs(_bnrInfoName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicConfigBackup()
        {
            return VbrGetDynamicCsvRecs(_configBackup, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetPhysProtected()
        {
            return VbrGetDynamicCsvRecs(_physProtected, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetPhysNotProtected()
        {
            return VbrGetDynamicCsvRecs(_physNotProtected, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicJobInfo()
        {
            return VbrGetDynamicCsvRecs(_jobReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicBjobs()
        {
            return VbrGetDynamicCsvRecs(_bjobs, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamincConfigBackup()
        {
            return VbrGetDynamicCsvRecs(_configBackup, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamincNetRules()
        {
            return VbrGetDynamicCsvRecs(_trafficRules, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicRepo()
        {
            return VbrGetDynamicCsvRecs(_repoReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicSobrExt()
        {
            return VbrGetDynamicCsvRecs(_sobrExtReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicSobr()
        {
            return VbrGetDynamicCsvRecs(_sobrReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynamicCapTier()
        {
            return VbrGetDynamicCsvRecs(_capTier, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynViProxy()
        {
            return VbrGetDynamicCsvRecs(_proxyReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynHvProxy()
        {
            return VbrGetDynamicCsvRecs(_hvProxReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynNasProxy()
        {
            return VbrGetDynamicCsvRecs(_nasProxReportName, CVariables.vbrDir);
        }
        public IEnumerable<dynamic> GetDynCdpProxy()
        {
            return VbrGetDynamicCsvRecs(_cdpProxReportName, CVariables.vbrDir);
        }
        #endregion

        #region oldCsvParsers

        public IEnumerable<CJobSessionCsvInfos> SessionCsvParser()
        {
            var res = VbrFileReader(_sessionPath);
            if (res != null)
                return res.GetRecords<CJobSessionCsvInfos>();
            return null;
        }
        public IEnumerable<CBnRCsvInfo> BnrCsvParser()
        {
            var res = VbrFileReader(_bnrInfoName);
            if (res != null)
                return res.GetRecords<CBnRCsvInfo>();
            return null;
        }
        public IEnumerable<CSobrExtentCsvInfos> SobrExtParser()
        {
            var res = VbrFileReader(_sobrExtReportName);
            if (res != null)
                return res.GetRecords<CSobrExtentCsvInfos>();
            return null;
        }
        public IEnumerable<CWaitsCsv> WaitsCsvReader()
        {

            var res = VbrFileReader(_waits);
            if (res != null)
                return res.GetRecords<CWaitsCsv>();
            return null;
        }

        public IEnumerable<CViProtected> ViProtectedReader()
        {
            var res = VbrFileReader(_ViProtected);
            if (res != null)
                return res.GetRecords<CViProtected>();
            return null;
        }
        public IEnumerable<CViProtected> ViUnProtectedReader()
        {
            var res = VbrFileReader(_viUnprotected);
            if (res != null)
                return res.GetRecords<CViProtected>();
            return null;
        }
        public IEnumerable<CViProtected> HvProtectedReader()
        {
            var res = VbrFileReader(_HvProtected);
            if (res != null)
                return res.GetRecords<CViProtected>();
            return null;
        }

        public IEnumerable<CViProtected> HvUnProtectedReader()
        {
            var res = VbrFileReader(_HvUnprotected);
            if (res != null)
                return res.GetRecords<CViProtected>();
            return null;
        }
        public IEnumerable<CMalwareObject> MalwareInfo()
        {
            var res = VbrFileReader(_malware);
            if (res != null)
                return res.GetRecords<CMalwareObject>();
            return null;
        }
        //public IEnumerable<CNasFileDataVmc> GetDynamicNasFileData()
        //{
        //    var res = VbrFileReader(_nasFileData);
        //    if (res != null)
        //        return res.GetRecords<CNasFileDataVmc>();
        //    return null;
        //}

        public IEnumerable<CNasVmcInfo> GetDynamicNasShareSize()
        {
            var res = VbrFileReader(_nasShareSize);
            if (res != null)
                return res.GetRecords<CNasVmcInfo>().ToList();
            return null;
        }

        public IEnumerable<CObjectShareVmcInfo> GetDynamicNasObjectSize()
        {
            var res = VbrFileReader(_nasObjectSize);
            if (res != null)
                return res.GetRecords<CObjectShareVmcInfo>();
            return null;
        }
        public IEnumerable<CRegOptionsCsv> RegOptionsCsvParser()
        {
            var res = VbrFileReader(_regOptions);
            if (res != null)
                return res.GetRecords<CRegOptionsCsv>();
            return null;
        }

        public IEnumerable<CConfigBackupCsv> ConfigBackupCsvParser()
        {
            var res = VbrFileReader(_configBackup);
            if (res != null)
                return res.GetRecords<CConfigBackupCsv>();
            return null;
        }
        public IEnumerable<CNetTrafficRulesCsv> NetTrafficCsvParser()
        {
            var res = VbrFileReader(_trafficRules);
            if (res != null)
                return res.GetRecords<CNetTrafficRulesCsv>();
            return null;
        }
        public IEnumerable<CCapTierCsv> CapTierCsvParser()
        {
            var res = VbrFileReader(_capTier);
            if (res != null)
                return res.GetRecords<CCapTierCsv>();
            return null;
        }


        public IEnumerable<CPluginCsvInfo> PluginCsvParser()
        {
            var res = VbrFileReader(_piReportName);
            if (res != null)
                return res.GetRecords<CPluginCsvInfo>();
            return null;
        }

        public IEnumerable<CWanCsvInfos> WanParser()
        {
            var res = VbrFileReader(_wanReportName);
            if (res != null)
                return res.GetRecords<CWanCsvInfos>();
            return null;
        }
        public IEnumerable<CJobCsvInfos> JobCsvParser()
        {
            var res = VbrFileReader(_jobReportName);
            if (res != null)
                return res.GetRecords<CJobCsvInfos>();
            return null;
        }


        public IEnumerable<CBjobCsv> BJobCsvParser()
        {
            var res = VbrFileReader(_bjobs);
            if (res != null)
                return res.GetRecords<CBjobCsv>();
            return null;
        }

        public IEnumerable<CServerCsvInfos> ServerCsvParser()
        {
            var res = VbrFileReader(_serverReportName);
            if (res != null)
                return res.GetRecords<CServerCsvInfos>();
            return null;
        }
        public IEnumerable<CProxyCsvInfos> ProxyCsvParser()
        {
            var res = VbrFileReader(_proxyReportName);
            if (res != null)
                return res.GetRecords<CProxyCsvInfos>();
            return null;
        }

        public IEnumerable<CSobrCsvInfo> SobrCsvParser()
        {
            var res = VbrFileReader(_sobrReportName);
            if (res != null)
                return res.GetRecords<CSobrCsvInfo>();
            return null;
        }


        public IEnumerable<dynamic> RepoCsvParser()
        {
            var res = VbrFileReader(_repoReportName);
            if (res != null)
                return res.GetRecords<CRepoCsvInfos>();
            return null;


            //if (String.IsNullOrEmpty(reportName))
            //{
            //    var r = FileFinder(_repoReportName, CVariables.vbrDir).GetRecords<CRepoCsvInfos>();
            //    return r;
            //}
            //else
            //{
            //    var reader = FileFinder(reportName, CVariables.vbrDir);
            //    if (reader != null)
            //        return reader.GetRecords<CRepoCsvInfos>().ToList();
            //    else throw new FileNotFoundException("Repo Csv Missing");
            //}
        }





        public IEnumerable<CCdpProxyCsvInfo> CdpProxCsvParser()
        {
            var res = VbrFileReader(_cdpProxReportName);
            if (res != null)
                return res.GetRecords<CCdpProxyCsvInfo>();
            return null;

        }

        public IEnumerable<CHvProxyCsvInfo> HvProxCsvParser()
        {
            var res = VbrFileReader(_hvProxReportName);
            if (res != null)
                return res.GetRecords<CHvProxyCsvInfo>();
            return null;

        }

        public IEnumerable<CFileProxyCsvInfo> NasProxCsvParser()
        {
            var res = VbrFileReader(_nasProxReportName);
            if (res != null)
                return res.GetRecords<CFileProxyCsvInfo>();
            return null;

        }

        #endregion

        #region localFunctions
        public void Dispose() { }
        private CsvReader VbrFileReader(string file)
        {
            var fileResult = _vbrReader.VbrCsvReader(file);
            if (fileResult != null)
                return fileResult;
            else return null;
        }

        private CsvReader VboFileReader(string file)
        {
            var fileResult = _vbrReader.VboCsvReader(file);
            if (fileResult != null)
                return fileResult;
            else return null;
        }

        #endregion
    }
}
