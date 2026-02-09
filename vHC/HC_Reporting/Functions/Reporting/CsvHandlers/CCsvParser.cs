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

// using VeeamHealthCheck.Functions.Reporting.CsvHandlers.Proxies;
// using VeeamHealthCheck.Functions.Reporting.CsvHandlers.Repositories;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.DataTypes.NAS;
using VeeamHealthCheck.Functions.Reporting.DataTypes.Tape;

namespace VeeamHealthCheck.Functions.Reporting.CsvHandlers
{
    public class CCsvParser
    {
        private readonly CLogger log = CGlobals.Logger;
        private readonly CCsvReader vbrReader = new();
        private static readonly CCsvReader vbrReaderStatic = new();
        private readonly CCsvReader vboReader = new();

        // CSV Paths.
        public string sessionPath = "VeeamSessionReport";
        public string outPath;// = CVariables.vbrDir;
        public readonly string sobrExtReportName = "SOBRExtents";
        public readonly string sobrReportName = "SOBRs";
        public static readonly string proxyReportName = "Proxies";
        public readonly string repoReportName = "Repositories";
        public readonly string serverReportName = "Servers";
        public readonly string licReportName = "LicInfo";
        public readonly string wanReportName = "WanAcc";
        public static readonly string cdpProxReportName = "CdpProxy";
        public static readonly string hvProxReportName = "HvProxy";
        public static readonly string nasProxReportName = "NasProxy";
        public readonly string bnrInfoName = "vbrinfo";
        public readonly string bjobs = "bjobs";
        public readonly string capTier = "capTier";
        public readonly string trafficRules = "trafficRules";
        public readonly string configBackup = "configBackup";
        public readonly string regOptions = "regkeys";
        public readonly string waits = "waits";
        public readonly string ViProtected = "ViProtected";
        public readonly string viUnprotected = "ViUnprotected";
        public readonly string physProtected = "PhysProtected";
        public readonly string physNotProtected = "PhysNotProtected";
        public readonly string HvProtected = "HvProtected";
        public readonly string HvUnprotected = "HvUnprotected";
        public readonly string malware = "malware_settings";
        public readonly string malwareObjects = "malware_infectedobject";
        public readonly string malwareEvents = "malware_events";
        public readonly string malwareExclusions = "malware_exclusions";
        public readonly string nasFileData = "NasFileData";
        public readonly string nasShareSize = "NasSharesize";
        public readonly string nasObjectSize = "NasObjectSourceStorageSize";
        public readonly string compliance = "SecurityCompliance";
        public readonly string allServersRequirements = "AllServersRequirementsComparison";

        // Job files
        public readonly string piReportName = "pluginjobs";
        public readonly string jobReportName = "Jobs";

        // make string for these job types: "AgentBackupJob.csv", "catalystJob", "cdpjobs", EndpointJob, nasBackup, nasBCJ, SureBackupJob 
        public readonly string agentBackupJob = "AgentBackupJob";
        public readonly string catalystJob = "catalystJob";
        public readonly string cdpjobs = "cdpjobs";
        public readonly string EndpointJob = "EndpointJob";
        public readonly string nasBackup = "nasBackup";
        public readonly string nasBCJ = "nasBCJ";
        public readonly string SureBackupJob = "SureBackupJob";

        public readonly string tapeJobInfo = "TapeJobs";

        // VBO Files
        private readonly string vboGlobalCsv = "Global";
        private readonly string vboProxies = "Proxies";
        private readonly string vboRBAC = "RBACRoles";
        private readonly string vboRepositories = "LocalRepositories";
        private readonly string vboSecurity = "Security";
        private readonly string vboController = "Controller";
        private readonly string vboControllerDriver = "ControllerDrives";
        private readonly string vboJobSessions = "JobSessions";
        private readonly string vboJobStats = "JobStats";
        private readonly string vboObjectRepos = "ObjectRepositories";
        private readonly string vboOrganizations = "Organizations";
        private readonly string vboPermissions = "Permissions";
        private readonly string vboProtectionStatus = "ProtectionStatus";
        private readonly string vboLiceOverView = "LicenseOverviewReport";
        private readonly string vboMailboxProtection = "MailboxProtection";
        private readonly string StorageConsumption = "StorageConsumption";
        private readonly string vboJobs = "Jobs";
        private readonly string vboProcStat = "ProcessingStats";

        public CCsvParser()
        {
            this.Start(null);
        }

        public CCsvParser(string csvRepo)
        {
            this.Start(csvRepo);
        }

        private void Start(string csvRepo)
        {
            if (string.IsNullOrEmpty(csvRepo))
            {
                this.outPath = CVariables.vbrDir;
            }
            else
            {
                this.outPath = csvRepo;
            }
        }

        private IEnumerable<dynamic> VbrGetDynamicCsvRecs(string file, string vbrOrVboPath)
        {
            var res = this.VbrFileReader(file);
            if (res != null)
            {

                return res.GetRecords<dynamic>();
            }


            return Enumerable.Empty<dynamic>();
        }

        private static IEnumerable<dynamic> VbrGetDynamicCsvRecsStatic(string file, string vbrOrVboPath)
        {
            var res = VbrFileReaderStatic(file);
            if (res != null)
            {

                return res.GetRecords<dynamic>();
            }


            return Enumerable.Empty<dynamic>();
        }

        private IEnumerable<dynamic> VboGetDynamicCsvRecs(string file, string vbrOrVboPath)
        {
            var res = this.VboFileReader(file);
            if (res != null)
            {

                return res.GetRecords<dynamic>();
            }


            return Enumerable.Empty<dynamic>();
        }
        #region m365CsvParser

        public IEnumerable<CGlobalCsv> GetDynamicVboGlobal()
        {
            var res = this.VboFileReader(this.vboGlobalCsv);
            if (res != null)
            {

                return res.GetRecords<CGlobalCsv>();
            }


            return null;
        }

        public IEnumerable<dynamic> GetDynamicVboProxies()
        {
            return this.VboGetDynamicCsvRecs(this.vboProxies, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynamicVboRbac()
        {
            return this.VboGetDynamicCsvRecs(this.vboRBAC, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynamicVboJobs()
        {
            return this.VboGetDynamicCsvRecs(this.vboJobs, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynamicVboProcStat()
        {
            return this.VboGetDynamicCsvRecs(this.vboProcStat, CVariables.vb365dir);
        }

        public IEnumerable<CLocalRepos> GetDynamicVboRepo()
        {
            // return VboGetDynamicCsvRecs(_vboRepositories, CVariables.vb365dir);
            var res = this.VboFileReader(this.vboRepositories);
            if (res != null)
            {

                return res.GetRecords<CLocalRepos>();
            }


            return null;

            // return FileFinder(_vboRepositories, CVariables.vb365dir).GetRecords<CLocalRepos>();
        }

        public IEnumerable<CSecurityCsv> GetDynamicVboSec()
        {
            // return FileFinder(_vboSecurity, CVariables.vb365dir).GetRecords<CSecurityCsv>();
            var res = this.VboFileReader(this.vboSecurity);
            if (res != null)
            {

                return res.GetRecords<CSecurityCsv>();
            }


            return null;
        }

        public IEnumerable<dynamic> GetDynVboController()
        {
            return this.VboGetDynamicCsvRecs(this.vboController, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboControllerDriver()
        {
            return this.VboGetDynamicCsvRecs(this.vboControllerDriver, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboJobSess()
        {
            return this.VboGetDynamicCsvRecs(this.vboJobSessions, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboJobStats()
        {
            return this.VboGetDynamicCsvRecs(this.vboJobStats, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboObjRepo()
        {
            return this.VboGetDynamicCsvRecs(this.vboObjectRepos, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboOrg()
        {
            return this.VboGetDynamicCsvRecs(this.vboOrganizations, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboPerms()
        {
            return this.VboGetDynamicCsvRecs(this.vboPermissions, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboProtStat()
        {
            return this.VboGetDynamicCsvRecs(this.vboProtectionStatus, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboLicOver()
        {
            return this.VboGetDynamicCsvRecs(this.vboLiceOverView, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboMbProtRep()
        {
            return this.VboGetDynamicCsvRecs(this.vboMailboxProtection, CVariables.vb365dir);
        }

        public IEnumerable<dynamic> GetDynVboMbStgConsumption()
        {
            return this.VboGetDynamicCsvRecs(this.StorageConsumption, CVariables.vb365dir);
        }

        #endregion

        #region DynamicCsvParsers-VBR

        public IEnumerable<dynamic> GetDynamicLicenseCsv()
        {
            return this.VbrGetDynamicCsvRecs(this.licReportName, CVariables.vbrDir);

            // return FileFinder(_licReportName).GetRecords<dynamic>();
        }

        public IEnumerable<dynamic> GetDynamicVbrInfo()
        {
            return this.VbrGetDynamicCsvRecs(this.bnrInfoName, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicConfigBackup()
        {
            return this.VbrGetDynamicCsvRecs(this.configBackup, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetPhysProtected()
        {
            return this.VbrGetDynamicCsvRecs(this.physProtected, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetPhysNotProtected()
        {
            return this.VbrGetDynamicCsvRecs(this.physNotProtected, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicJobInfo()
        {
            return this.VbrGetDynamicCsvRecs(this.jobReportName, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicPluginJobs()
        {
            return this.VbrGetDynamicCsvRecs(this.piReportName, CVariables.vbrDir);
        }

        // make similar method as line 244 for the newly added file names
        public IEnumerable<dynamic> GetDynamicAgentBackupJob()
        {
            return this.VbrGetDynamicCsvRecs(this.agentBackupJob, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicCatalystJob()
        {
            return this.VbrGetDynamicCsvRecs(this.catalystJob, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicCdpJobs()
        {
            return this.VbrGetDynamicCsvRecs(this.cdpjobs, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicEndpointJob()
        {
            return this.VbrGetDynamicCsvRecs(this.EndpointJob, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicNasBackup()
        {
            return this.VbrGetDynamicCsvRecs(this.nasBackup, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicNasBCJ()
        {
            return this.VbrGetDynamicCsvRecs(this.nasBCJ, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicSureBackupJob()
        {
            return this.VbrGetDynamicCsvRecs(this.SureBackupJob, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicBjobs()
        {
            return this.VbrGetDynamicCsvRecs(this.bjobs, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamincConfigBackup()
        {
            return this.VbrGetDynamicCsvRecs(this.configBackup, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamincNetRules()
        {
            return this.VbrGetDynamicCsvRecs(this.trafficRules, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicRepo()
        {
            return this.VbrGetDynamicCsvRecs(this.repoReportName, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicSobrExt()
        {
            return this.VbrGetDynamicCsvRecs(this.sobrExtReportName, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicSobr()
        {
            return this.VbrGetDynamicCsvRecs(this.sobrReportName, CVariables.vbrDir);
        }

        public IEnumerable<dynamic> GetDynamicCapTier()
        {
            return this.VbrGetDynamicCsvRecs(this.capTier, CVariables.vbrDir);
        }

        public static IEnumerable<dynamic> GetDynViProxy()
        {
            return VbrGetDynamicCsvRecsStatic(proxyReportName, CVariables.vbrDir);
        }

        public static IEnumerable<dynamic> GetDynHvProxy()
        {
            return VbrGetDynamicCsvRecsStatic(hvProxReportName, CVariables.vbrDir);
        }

        public static IEnumerable<dynamic> GetDynNasProxy()
        {
            return VbrGetDynamicCsvRecsStatic(nasProxReportName, CVariables.vbrDir);
        }

        public static IEnumerable<dynamic> GetDynCdpProxy()
        {
            return VbrGetDynamicCsvRecsStatic(cdpProxReportName, CVariables.vbrDir);
        }
        #endregion

        #region oldCsvParsers

        public IEnumerable<CJobSessionCsvInfos> SessionCsvParser()
        {
            var res = this.VbrFileReader(this.sessionPath);
            if (res != null)
            {

                return res.GetRecords<CJobSessionCsvInfos>();
            }


            return null;
        }

        public IEnumerable<CBnRCsvInfo> BnrCsvParser()
        {
            var res = this.VbrFileReader(this.bnrInfoName);
            if (res != null)
            {

                return res.GetRecords<CBnRCsvInfo>();
            }


            return null;
        }

        public IEnumerable<CSobrExtentCsvInfos> SobrExtParser()
        {
            this.log.Info($"[CCsvParser] Looking for SOBR Extent CSV file: {this.sobrExtReportName}");
            var res = this.VbrFileReader(this.sobrExtReportName);
            if (res != null)
            {
                this.log.Info($"[CCsvParser] SOBR Extent CSV file found and opened successfully");
                return res.GetRecords<CSobrExtentCsvInfos>();
            }

            this.log.Warning($"[CCsvParser] SOBR Extent CSV file not found: {this.sobrExtReportName}");
            return null;
        }

        public IEnumerable<CWaitsCsv> WaitsCsvReader()
        {
            var res = this.VbrFileReader(this.waits);
            if (res != null)
            {

                return res.GetRecords<CWaitsCsv>();
            }


            return null;
        }

        public IEnumerable<CViProtected> ViProtectedReader()
        {
            var res = this.VbrFileReader(this.ViProtected);
            if (res != null)
            {

                return res.GetRecords<CViProtected>();
            }


            return null;
        }

        public IEnumerable<CViProtected> ViUnProtectedReader()
        {
            var res = this.VbrFileReader(this.viUnprotected);
            if (res != null)
            {

                return res.GetRecords<CViProtected>();
            }


            return null;
        }

        public IEnumerable<CViProtected> HvProtectedReader()
        {
            var res = this.VbrFileReader(this.HvProtected);
            if (res != null)
            {

                return res.GetRecords<CViProtected>();
            }


            return null;
        }

        public IEnumerable<CViProtected> HvUnProtectedReader()
        {
            var res = this.VbrFileReader(this.HvUnprotected);
            if (res != null)
            {

                return res.GetRecords<CViProtected>();
            }


            return null;
        }

        public IEnumerable<CMalwareObject> MalwareSettings()
        {
            var res = this.VbrFileReader(this.malware);
            if (res != null)
            {
                res.Context.RegisterClassMap<CMalwareObjectMap>();
                return res.GetRecords<CMalwareObject>().ToList();
            }

            return null;
        }

        public IEnumerable<CMalwareInfectedObjects> MalwareInfectedObjects()
        {
            var res = this.VbrFileReader(this.malwareObjects);
            if (res != null)
            {

                return res.GetRecords<CMalwareInfectedObjects>();
            }


            return null;
        }

        public IEnumerable<CMalwareEvents> MalwareEvents()
        {
            var res = this.VbrFileReader(this.malwareEvents);
            if (res != null)
            {

                return res.GetRecords<CMalwareEvents>();
            }


            return null;
        }

        public IEnumerable<CComplianceCsv> ComplianceCsv()
        {
            var res = this.VbrFileReader(this.compliance);
            if (res != null)
            {

                return res.GetRecords<CComplianceCsv>().ToList();
            }


            return null;
        }

        public IEnumerable<CMalwareExcludedItem> MalwareExclusions()
        {
            var res = this.VbrFileReader(this.malwareExclusions);
            if (res != null)
            {

                return res.GetRecords<CMalwareExcludedItem>();
            }


            return null;
        }

        // public IEnumerable<CNasFileDataVmc> GetDynamicNasFileData()
        // {
        //    var res = VbrFileReader(_nasFileData);
        //    if (res != null)
        //        return res.GetRecords<CNasFileDataVmc>();
        //    return null;
        // }
        public IEnumerable<CNasVmcInfo> GetDynamicNasShareSize()
        {
            var res = this.VbrFileReader(this.nasShareSize);
            if (res != null)
            {

                return res.GetRecords<CNasVmcInfo>().ToList();
            }


            return null;
        }

        public IEnumerable<CEntraTenant> GetDynamicEntraTenants()
        {
            var res = this.VbrFileReader("entraTenants");
            if (res != null)
            {

                return res.GetRecords<CEntraTenant>();
            }


            return null;
        }

        public IEnumerable<CEntraLogJobs> GetDynamicEntraLogJobs()
        {
            var res = this.VbrFileReader("entraLogJob");
            if (res != null)
            {

                return res.GetRecords<CEntraLogJobs>();
            }


            return null;
        }

        public IEnumerable<CEntraTenantJobs> GetDynamicEntraTenantJobs()
        {
            var res = this.VbrFileReader("entraTenantJob");
            if (res != null)
            {

                return res.GetRecords<CEntraTenantJobs>();
            }


            return null;
        }

        public IEnumerable<CObjectShareVmcInfo> GetDynamicNasObjectSize()
        {
            var res = this.VbrFileReader(this.nasObjectSize);
            if (res != null)
            {

                return res.GetRecords<CObjectShareVmcInfo>();
            }


            return null;
        }

        public IEnumerable<CTapeJobInfo> GetTapeJobInfoFromCsv()
        {
            var res = this.VbrFileReader(this.tapeJobInfo);
            if (res != null)
            {

                return res.GetRecords<CTapeJobInfo>().ToList();
            }


            return null;
        }

        public IEnumerable<CRegOptionsCsv> RegOptionsCsvParser()
        {
            var res = this.VbrFileReader(this.regOptions);
            if (res != null)
            {

                return res.GetRecords<CRegOptionsCsv>();
            }


            return null;
        }

        public IEnumerable<CConfigBackupCsv> ConfigBackupCsvParser()
        {
            var res = this.VbrFileReader(this.configBackup);
            if (res != null)
            {

                return res.GetRecords<CConfigBackupCsv>();
            }


            return null;
        }

        public IEnumerable<CNetTrafficRulesCsv> NetTrafficCsvParser()
        {
            var res = this.VbrFileReader(this.trafficRules);
            if (res != null)
            {

                return res.GetRecords<CNetTrafficRulesCsv>();
            }


            return null;
        }

        public IEnumerable<CCapTierCsv> CapTierCsvParser()
        {
            var res = this.VbrFileReader(this.capTier);
            if (res != null)
            {

                return res.GetRecords<CCapTierCsv>();
            }


            return null;
        }

        public IEnumerable<CPluginCsvInfo> PluginCsvParser()
        {
            var res = this.VbrFileReader(this.piReportName);
            if (res != null)
            {

                return res.GetRecords<CPluginCsvInfo>();
            }


            return null;
        }

        public IEnumerable<CWanCsvInfos> WanParser()
        {
            var res = this.VbrFileReader(this.wanReportName);
            if (res != null)
            {

                return res.GetRecords<CWanCsvInfos>();
            }


            return null;
        }

        public IEnumerable<CJobCsvInfos> JobCsvParser()
        {
            var res = this.VbrFileReader(this.jobReportName);
            if (res != null)
            {

                return res.GetRecords<CJobCsvInfos>();
            }


            return null;
        }

        public IEnumerable<CBjobCsv> BJobCsvParser()
        {
            var res = this.VbrFileReader(this.bjobs);
            if (res != null)
            {

                return res.GetRecords<CBjobCsv>();
            }


            return null;
        }

        public IEnumerable<CServerCsvInfos> ServerCsvParser()
        {
            var res = this.VbrFileReader(this.serverReportName);
            if (res != null)
            {

                return res.GetRecords<CServerCsvInfos>();
            }


            return null;
        }

        public IEnumerable<CProxyCsvInfos> ProxyCsvParser()
        {
            var res = this.VbrFileReader(proxyReportName);
            if (res != null)
            {

                return res.GetRecords<CProxyCsvInfos>();
            }


            return null;
        }

        // Requirements comparison CSVs 
        private IEnumerable<CRequirementsCsvInfo> RequirementsCsvParserInternal(string reportName)
        {
            var res = this.VbrFileReader(reportName);
            if (res != null)
            {
                return res.GetRecords<CRequirementsCsvInfo>();
            }

            return null; // keep consistent with existing parsers in this class
        }

        public IEnumerable<CRequirementsCsvInfo> ServersRequirementsCsvParser()
        {
            return this.RequirementsCsvParserInternal(this.allServersRequirements);
        }

        public IEnumerable<CSobrCsvInfo> SobrCsvParser()
        {
            this.log.Info($"[CCsvParser] Looking for SOBR CSV file: {this.sobrReportName}");
            var res = this.VbrFileReader(this.sobrReportName);
            if (res != null)
            {
                this.log.Info($"[CCsvParser] SOBR CSV file found and opened successfully");
                return res.GetRecords<CSobrCsvInfo>();
            }

            this.log.Warning($"[CCsvParser] SOBR CSV file not found: {this.sobrReportName}");
            return null;
        }

        public IEnumerable<dynamic> RepoCsvParser()
        {
            var res = this.VbrFileReader(this.repoReportName);
            if (res != null)
            {

                return res.GetRecords<CRepoCsvInfos>();
            }


            return null;

            // if (String.IsNullOrEmpty(reportName))
            // {
            //    var r = FileFinder(_repoReportName, CVariables.vbrDir).GetRecords<CRepoCsvInfos>();
            //    return r;
            // }
            // else
            // {
            //    var reader = FileFinder(reportName, CVariables.vbrDir);
            //    if (reader != null)
            //        return reader.GetRecords<CRepoCsvInfos>().ToList();
            //    else throw new FileNotFoundException("Repo Csv Missing");
            // }
        }

        public IEnumerable<CCdpProxyCsvInfo> CdpProxCsvParser()
        {
            var res = this.VbrFileReader(cdpProxReportName);
            if (res != null)
            {

                return res.GetRecords<CCdpProxyCsvInfo>();
            }


            return null;
        }

        public IEnumerable<CHvProxyCsvInfo> HvProxCsvParser()
        {
            var res = this.VbrFileReader(hvProxReportName);
            if (res != null)
            {

                return res.GetRecords<CHvProxyCsvInfo>();
            }


            return null;
        }

        public IEnumerable<CFileProxyCsvInfo> NasProxCsvParser()
        {
            var res = this.VbrFileReader(nasProxReportName);
            if (res != null)
            {

                return res.GetRecords<CFileProxyCsvInfo>();
            }


            return null;
        }

        #endregion

        #region localFunctions
        public void Dispose() { }

        private CsvReader VbrFileReader(string file)
        {
            var fileResult = this.vbrReader.FileFinder(file, this.outPath);
            if (fileResult != null)
            {

                return fileResult;
            }
            else
            {
                return null;
            }
        }

        private static CsvReader VbrFileReaderStatic(string file)
        {
            var fileResult = vbrReaderStatic.VbrCsvReader(file);
            if (fileResult != null)
            {

                return fileResult;
            }
            else
            {
                return null;
            }
        }

        private CsvReader VboFileReader(string file)
        {
            var fileResult = this.vbrReader.VboCsvReader(file);
            if (fileResult != null)
            {

                return fileResult;
            }
            else
            {
                return null;
            }
        }

        #endregion
    }
}
