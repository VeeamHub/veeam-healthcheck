// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CsvHelper;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Job_Session_Summary;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Registry;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;

//using VeeamHealthCheck.Functions.Reporting.Html.VBR.VBR_Tables.Repositories;
using VeeamHealthCheck.Functions.Reporting.RegSettings;
using VeeamHealthCheck.Reporting.Html.VBR;
//using VeeamHealthCheck.Reporting.Html.VBR.Managed_Server_Table;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
//using VeeamHealthCheck.Common;
using VeeamHealthCheck.Shared.Logging;
//using static VeeamHealthCheck.Functions.Collection.DB.CModel;
using static VeeamHealthCheck.Functions.Collection.DB.CModel;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    public class CDataFormer
    {
        private string logStart = "[DataFormer]\t";

        private bool _isBackupServerProxy;
        private bool _isBackupServerRepo;
        private bool _isBackupServerWan;
        private Dictionary<string, int> _repoJobCount;
        private CScrubHandler _scrubber = CGlobals.Scrubber;

        private readonly CCsvParser _csvParser = new();
        private readonly CLogger log = CGlobals.Logger;

        private Dictionary<string, string> _repoPaths = new();

        private IEnumerable<dynamic> _viProxy = CCsvParser.GetDynViProxy().ToList();
        private IEnumerable<dynamic> _hvProxy = CCsvParser.GetDynHvProxy().ToList();
        private IEnumerable<dynamic> _nasProxy = CCsvParser.GetDynNasProxy().ToList();
        private IEnumerable<dynamic> _cdpProxy = CCsvParser.GetDynCdpProxy().ToList();

        public CDataFormer() // add string mode input
        {
            //_csv = _dTypeParser.ServerInfo();

            //CheckXmlFile();

        }

        public void Dispose() { }


        #region XML Conversions


        public List<string> ParseNonProtectedTypes()
        {
            List<string> notProtectedTypes = new();

            //var bTypes = _dTypeParser.JobInfos;
            var csv = new CCsvParser();
            var jobInfos = csv.GetDynamicJobInfo();
            var bjobInfos = csv.GetDynamicBjobs();


            if (null != bjobInfos)
            {
                List<int> pTypes = new();
                foreach (var bjob in bjobInfos)
                {
                    int.TryParse(bjob.type, out int typeId);
                    if (!pTypes.Contains(typeId))
                    {
                        pTypes.Add(typeId);

                    }
                }
                foreach (EDbJobType jt2 in Enum.GetValues(typeof(EDbJobType)))
                {
                    if (!pTypes.Contains((int)jt2))
                    {
                        notProtectedTypes.Add(jt2.ToString());
                    }
                }
            }
            else
            {
                List<string> pTypes = new();
                foreach (var job in jobInfos)
                {
                    var t = job.jobtype;
                    pTypes.Add(job.jobtype);
                }

                foreach (string jt2 in Enum.GetNames(typeof(EDbJobType)))
                {
                    if (!pTypes.Contains(jt2))
                    {
                        notProtectedTypes.Add(jt2.ToString());
                    }
                }
            }



            notProtectedTypes.Add("Kubernetes");
            notProtectedTypes.Add("Microsoft 365");

            return notProtectedTypes;

        }
        public CSecuritySummaryTable SecSummary()
        {
            CSecuritySummaryTable t = new();
            List<int> secSummary = new List<int>();
            var csv = new CCsvParser();
            try
            {
                var vbrInfo = csv.GetDynamicVbrInfo();
                if (vbrInfo.Any(x => x.mfa == "True"))
                    t.MFAEnabled = true;
                else t.MFAEnabled = false;
            }
            catch (Exception ex)
            {

            }
            try
            {
                var sobrRepo = csv.GetDynamicCapTier().ToList();
                var extRepo = csv.GetDynamicSobrExt().ToList();
                var onPremRepo = csv.GetDynamicRepo().ToList();
                if (onPremRepo.Any(x => x.isimmutabilitysupported == "True"))
                {
                    t.ImmutabilityEnabled = true;
                    //return secSummary;
                }

                else if (sobrRepo.Any(x => x.immute == "True"))
                {
                    t.ImmutabilityEnabled = true;

                    //return secSummary;
                }


                else if (extRepo.Any(x => x.isimmutabilitysupported == "True"))
                {
                    t.ImmutabilityEnabled = true;
                    //return secSummary;
                }
                else
                {
                    t.ImmutabilityEnabled = false;
                }

            }
            catch (Exception)
            {
                log.Error(logStart + "Unable to find immutability. Marking false");
                t.ImmutabilityEnabled = false;
            }
            try
            {
                var netTraffic = csv.GetDynamincNetRules();
                if (netTraffic.Any(x => x.encryptionenabled == "True"))
                    t.TrafficEncrptionEnabled = true;
                else t.TrafficEncrptionEnabled = false;
            }
            catch (Exception)
            {
                log.Info(logStart + "Traffic encryption not detected. Marking false");
                t.TrafficEncrptionEnabled = false;
            }
            try
            {
                var backupEnc = csv.GetDynamicJobInfo();
                if (backupEnc.Any(x => x.pwdkeyid != "00000000-0000-0000-0000-000000000000" && !string.IsNullOrEmpty(x.pwdkeyid)))
                    t.BackupFileEncrptionEnabled = true;
                else t.BackupFileEncrptionEnabled = false;
            }
            catch (Exception)
            {
                log.Error(logStart + "Unable to detect backup encryption. Marking false");
                t.BackupFileEncrptionEnabled = false;
            }

            try
            {
                var cBackup = csv.GetDynamincConfigBackup();
                if (cBackup.Any(x => x.encryptionoptions == "True"))
                    t.ConfigBackupEncrptionEnabled = true;
                else t.ConfigBackupEncrptionEnabled = false;
            }
            catch (Exception)
            {
                log.Error(logStart + "Config backup not detected. Marking false");
                //log.Info(e.Message);
                t.ConfigBackupEncrptionEnabled = false;
            }

            return t;


        }

        public Dictionary<string, int> ServerSummaryToXml()
        {
            log.Info(logStart + "converting server summary to xml");
            //Dictionary<string, int> di = _dTypeParser.ServerSummaryInfo;
            // using (CDataTypesParser dt = new())
            // {
            //     return dt.ServerSummaryInfo;
            // }
            return CGlobals.DtParser.ServerSummaryInfo;
        }
        public int ProtectedWorkloadsToXml()
        {
            try
            {

                //customize the log line:
                log.Info(logStart + "Converting protected workloads data to xml...");


                // gather data needed for input
                CCsvParser csvp = new();

                #region viProtected
                var protectedVms = csvp.ViProtectedReader().ToList();
                var unProtectedVms = csvp.ViUnProtectedReader().ToList();

                var HvProtectedVms = csvp.HvProtectedReader();
                var HvUnProtectedVms = csvp.HvUnProtectedReader(); // test
                #endregion

                #region hv logic
                List<string> hvNames = new();
                List<string> hvProtectedNames = new();
                List<string> hvNotProtectedNames = new();
                int hvDupes = 0;

                if (HvProtectedVms != null || HvUnProtectedVms != null)
                {
                    HvProtectedVms = HvProtectedVms.ToList();
                    HvUnProtectedVms = HvUnProtectedVms.ToList();
                    foreach (var p in HvProtectedVms)
                    {

                        hvNames.Add(p.Name);
                        hvProtectedNames.Add(p.Name);


                    }
                    foreach (var un in HvUnProtectedVms)
                    {
                        if (un.Type == "Vm")
                        {
                            hvNotProtectedNames.Add(un.Name);
                            hvNames.Add(un.Name);

                        }

                    }

                    hvDupes = hvNames.Count - (hvProtectedNames.Distinct().Count() + hvNotProtectedNames.Distinct().Count());
                }
                else
                {
                    //_hvDupes = 0;
                    //_hvNotProtectedNames = 0;
                    //_hvProtectedNames = List<string>;
                }


                #endregion

                #region physProtected
                //var physProtected = csvp.PhysProtectedReader().ToList();
                //var physNotProtected = csvp.PhysNotProtectedReader().ToList();
                var physProtected = csvp.GetPhysProtected().ToList();
                var physNotProtected = csvp.GetPhysNotProtected().ToList();

                #endregion

                List<string> vmNames = new();
                List<string> viProtectedNames = new();
                List<string> viNotProtectedNames = new();
                int viDupes = 0;


                foreach (var p in protectedVms)
                {

                    vmNames.Add(p.Name);
                    viProtectedNames.Add(p.Name);


                }
                foreach (var un in unProtectedVms)
                {
                    if (un.Type == "Vm")
                    {
                        viNotProtectedNames.Add(un.Name);
                        vmNames.Add(un.Name);

                    }

                }

                viDupes = vmNames.Count - (viProtectedNames.Distinct().Count() + viNotProtectedNames.Distinct().Count());

                List<string> physNames = new();
                List<string> physProtNames = new();
                List<string> physNotProtNames = new();

                foreach (var u in physNotProtected)
                {
                    if (u.type == "Computer")
                    {
                        physNames.Add(u.name);
                        physNotProtNames.Add(u.name);
                    }

                }
                List<string> vmProtectedByPhys = new();
                foreach (var p in physProtected)
                {
                    foreach (var v in protectedVms)
                        if (p.name.Contains(v.Name))
                            vmProtectedByPhys.Add(v.Name);
                    foreach (var w in unProtectedVms)
                    {
                        if (p.name.Contains(w.Name))
                            vmProtectedByPhys.Add(w.Name);
                    }
                }

                //var xml2 = XML.AddXelement(protectedVms.Count.ToString(), "ViProtected");

                _viProtectedNames = viProtectedNames;
                _viNotProtectedNames = viNotProtectedNames;
                _viDupes = viDupes;
                _vmProtectedByPhys = vmProtectedByPhys;
                _physNotProtNames = physNotProtNames;
                _physProtNames = physProtNames;

                _hvProtectedNames = hvProtectedNames;
                _hvNotProtectedNames = hvNotProtectedNames;
                _hvDupes = hvDupes;


                log.Info(logStart + "Converting protected workloads data to xml..done!");
                return 0;
            }
            catch (Exception ex)
            {
                return 1;
            }
        }
        public int _viDupes;
        public List<string> _vmProtectedByPhys;
        public List<string> _viProtectedNames;
        //public List<string> _vmNotProtectedNames;

        public int _hvDupes;
        public List<string> _hvProtectedNames;
        public List<string> _hvNotProtectedNames;

        public List<string> _viNotProtectedNames;
        public List<string> _physNotProtNames;
        public List<string> _physProtNames;
        private void NewXmlNodeTemplate(bool scrub)
        {
            //customize the log line:
            log.Info(logStart + "xml node template start...");


            // gather data needed for input
            //CServerTypeInfos backupServer = _csv.Find(x => (x.Id == _backupServerId));
            CCsvParser config = new();



            // Check for items needing scrubbed
            if (scrub)
            {
                // set items to scrub
            }

            log.Info(logStart + "xml template..done!");
        }
        public BackupServer BackupServerInfoToXml(bool scrub)
        {

            log.Info(logStart + "converting backup server info to xml");
            List<string> list = new List<string>();
            CBackupServerTableHelper bt = new(scrub);
            BackupServer b = bt.SetBackupServerData();



            CheckServerRoles(CGlobals._backupServerId);

            b.HasProxyRole = _isBackupServerProxy;
            b.HasRepoRole = _isBackupServerRepo;
            b.HasWanAccRole = _isBackupServerWan;



            log.Info(logStart + "converting backup server info to xml..done!");
            return b;
        }
        private string ParseString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            else return input;
        }
        public List<CSobrTypeInfos> SobrInfoToXml(bool scrub)
        {
            PreCalculations();
            log.Info(logStart + "Starting SOBR conversion to xml..");
            List<string[]> list = new();

            List<CSobrTypeInfos> csv = CGlobals.DtParser.SobrInfo;
            List<CRepoTypeInfos> repos = CGlobals.DtParser.ExtentInfo;
            csv = csv.OrderBy(x => x.Name).ToList();

            List<CSobrTypeInfos> outList = new();
            foreach (var c in csv)
            {
                string[] s = new string[30];
                int repoCount = repos.Count(x => x.SobrName == c.Name);

                string newName = c.Name;
                if (scrub)
                    newName = _scrubber.ScrubItem(c.Name, ScrubItemType.SOBR);
                _repoJobCount.TryGetValue(c.Name, out int jobCount);

                //s[0] += newName;
                //s[1] += repoCount;
                //s[2] += jobCount;
                //s[3] += c.PolicyType;
                //s[4] += c.EnableCapacityTier;
                //s[5] += c.CapacityTierCopyPolicyEnabled;
                //s[6] += c.CapacityTierMovePolicyEnabled;
                //s[7] += c.ArchiveTierEnabled;
                //s[8] += c.UsePerVMBackupFiles;
                //s[9] += c.CapTierType;
                //s[10] += c.ImmuteEnabled;
                //s[11] += c.ImmutePeriod;
                //s[12] += c.SizeLimitEnabled;
                //s[13] += c.SizeLimit;


                CSobrTypeInfos sobr = new()
                {
                    Name = newName,
                    Extents = c.Extents,
                    ExtentCount = repoCount,
                    JobCount = jobCount,
                    PolicyType = c.PolicyType,
                    EnableCapacityTier = c.EnableCapacityTier,
                    CapacityTierCopyPolicyEnabled = c.CapacityTierCopyPolicyEnabled,
                    CapacityTierMovePolicyEnabled = c.CapacityTierMovePolicyEnabled,
                    ArchiveTierEnabled = c.ArchiveTierEnabled,
                    UsePerVMBackupFiles = c.UsePerVMBackupFiles,
                    CapTierType = c.CapTierType,
                    ImmuteEnabled = c.ImmuteEnabled,
                    ImmutePeriod = c.ImmutePeriod,
                    SizeLimitEnabled = c.SizeLimitEnabled,
                    SizeLimit = c.SizeLimit

                };


                outList.Add(sobr);
            }
            log.Info(logStart + "Starting SOBR conversion to xml..done!");
            return outList;
        }
        private string SetGateHosts(string original, bool scrub)
        {
            string[] hosts = original.Split(' ');
            if (hosts.Count() == 1 && String.IsNullOrEmpty(hosts[0]))
            {
                return hosts[0];
            }
            string r = "";
            int counter = 1;
            int end = hosts.Length;
            foreach (string host in hosts)
            {
                string newhost = host;
                if (scrub)
                    newhost = CGlobals.Scrubber.ScrubItem(host, ScrubItemType.Server);
                if (counter == end)
                    r += newhost + "<br>";
                else
                    r += newhost + ",<br>";
                counter++;
            }
            return r;
        }
        public List<CRepository> ExtentXmlFromCsv(bool scrub)
        {
            log.Info(logStart + "converting extent info to xml");
            List<string[]> list = new List<string[]>();
            List<CRepoTypeInfos> csv = CGlobals.DtParser.ExtentInfo;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.SobrName).ToList();
            List<CRepository> repoList = new();

            if (csv != null)
                foreach (var c in csv)
                {

                    string newName = c.RepoName;
                    string sobrName = c.SobrName;
                    string hostName = c.Host;
                    string path = c.Path;
                    string gates = SetGateHosts(c.GateHosts, scrub);

                    if (scrub)
                    {
                        newName = CGlobals.Scrubber.ScrubItem(newName, ScrubItemType.Repository);
                        sobrName = CGlobals.Scrubber.ScrubItem(sobrName, ScrubItemType.SOBR);
                        hostName = CGlobals.Scrubber.ScrubItem(hostName, ScrubItemType.Server);
                        path = CGlobals.Scrubber.ScrubItem(path, ScrubItemType.Path);
                    }
                    string type;
                    if (c.TypeDisplay == null)
                        type = c.Type;
                    else
                        type = c.TypeDisplay;

                    var freePercent = FreePercent(c.FreeSPace, c.TotalSpace);



                    string gateHosts = "";
                    if (c.IsAutoGateway)
                    {
                        gateHosts = "";
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(c.GateHosts))
                            gateHosts = hostName;
                        else
                        {
                            gateHosts = gates;
                        }
                    }
                    bool immutability = false;
                    if(c.ObjectLockEnabled || c.IsImmutabilitySupported){
                        immutability = true;
                    }

                    CRepository repo = new()
                    {
                        Name = newName,
                        SobrName = sobrName,
                        MaxTasks = c.MaxTasks,
                        Cores = c.Cores,
                        Ram = c.Ram,
                        IsAutoGate = c.IsAutoGateway,
                        Host = gateHosts,
                        Path = path,
                        FreeSpace = Math.Round((decimal)c.FreeSPace / 1024, 2),
                        TotalSpace = Math.Round((decimal)c.TotalSpace / 1024, 2),
                        FreeSpacePercent = freePercent,
                        IsDecompress = c.IsDecompress,
                        AlignBlocks = c.AlignBlocks,
                        IsRotatedDrives = c.IsRotatedDriveRepository,
                        
                        IsImmutabilitySupported = immutability,
                        Type = type,
                        Provisioning = c.Povisioning

                    };



                    repoList.Add(repo);
                }
            log.Info(logStart + "converting extent info to xml..done!");
            return repoList;
        }
        private bool AddRepoPathToDict(string host, string path)
        {
            _repoPaths.TryGetValue(host, out var list);
            if (list == null)
            {
                _repoPaths.Add(host, path);
                return true;
            }
            else
                return false;
        }
        public List<CRepository> RepoInfoToXml(bool scrub)
        {
            PreCalculations();
            log.Info(logStart + "converting repository info to xml");
            List<CRepository> list = new();


            List<CRepoTypeInfos> csv = CGlobals.DtParser.RepoInfos.ToList();
            csv = csv.OrderBy(x => x.Name).ToList();
            if (csv != null)
                foreach (var c in csv)
                {

                    string[] s = new string[18];
                    string name = c.Name;
                    string host = c.Host;
                    string path = c.Path;
                    string gates = SetGateHosts(c.GateHosts, scrub);
                    if (scrub)
                    {
                        name = _scrubber.ScrubItem(c.Name, ScrubItemType.Repository);
                        host = _scrubber.ScrubItem(c.Host, ScrubItemType.Server);
                        path = _scrubber.ScrubItem(c.Path, ScrubItemType.Path);
                    }

                    decimal free = Math.Round((decimal)c.FreeSPace / 1024, 2);
                    decimal total = Math.Round((decimal)c.TotalSpace / 1024, 2);
                    decimal freePercent = FreePercent(c.FreeSPace, c.TotalSpace);
                    string freeSpace = free.ToString();
                    string totalSpace = total.ToString();
                    string percentFree = freePercent.ToString();
                    if (c.TotalSpace == 0)
                    {
                        freeSpace = FilterZeros(free);
                        totalSpace = FilterZeros(total);
                        percentFree = "";
                    }


                    _repoJobCount.TryGetValue(c.Name, out int jobCount);

                    string hosts = "";
                    s[0] += name;
                    s[1] += c.MaxTasks;
                    s[2] += jobCount;
                    s[3] += c.Cores;
                    s[4] += c.Ram;
                    s[5] += c.IsAutoGateway;
                    if (c.IsAutoGateway)
                        hosts = "";
                    else if (String.IsNullOrEmpty(c.GateHosts))
                        hosts = host;
                    else
                        hosts = gates;
                    CRepository repo = new()
                    {
                        Name = name,
                        MaxTasks = c.MaxTasks,
                        JobCount = jobCount,
                        Cores = c.Cores,
                        Ram = c.Ram,
                        IsAutoGate = c.IsAutoGateway,
                        Host = hosts,
                        Path = path,
                        FreeSpace = Math.Round((decimal)c.FreeSPace / 1024, 2),
                        TotalSpace = Math.Round((decimal)c.TotalSpace / 1024, 2),
                        FreeSpacePercent = freePercent,
                        IsDecompress = c.IsDecompress,
                        AlignBlocks = c.AlignBlocks,
                        IsRotatedDrives = c.IsRotatedDriveRepository,
                        IsImmutabilitySupported = c.IsImmutabilitySupported,
                        Type = c.Type,
                        Provisioning = c.Povisioning,
                        IsPerVmBackupFiles = c.SplitStoragesPerVm


                    };


                    list.Add(repo);
                }
            log.Info(logStart + "converting repository info to xml..done!");
            return list;
        }
        public List<string[]> ProxyXmlFromCsv(bool scrub)
        {
            log.Info("converting proxy info to xml");
            List<string[]> list = new();

            List<CProxyTypeInfos> csv = CGlobals.DtParser.ProxyInfos;

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(y => y.Type).ToList();

            if (csv != null)
                foreach (var c in csv)
                {
                    string[] s = new string[13];
                    if (scrub)
                    {
                        c.Name = CGlobals.Scrubber.ScrubItem(c.Name, ScrubItemType.Server);
                        c.Host = CGlobals.Scrubber.ScrubItem(c.Host, ScrubItemType.Server);
                    }
                    s[0] += c.Name;
                    s[1] += c.MaxTasksCount;
                    s[2] += c.Cores.ToString();
                    s[3] += c.Ram.ToString();
                    s[4] += c.Type;
                    s[5] += c.TransportMode;
                    s[6] += c.FailoverToNetwork;
                    s[7] += c.ChassisType;
                    s[8] += c.CachePath;
                    s[9] += c.CacheSize;
                    s[10] += c.Host;
                    s[11] += c.IsDisabled;
                    s[12] += c.Provisioning;

                    list.Add(s);
                }
            log.Info(logStart + "converting proxy info to xml..done!");

            return list;
        }

        public List<CManagedServer> ServerXmlFromCsv(bool scrub)    // managed servers protect vm count
        {
            log.Info(logStart + "converting server info to xml");
            List<CManagedServer> list = new();
            List<CServerTypeInfos> csv = CGlobals.ServerInfo;

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(x => x.Type).ToList();

            CCsvParser csvp = new();
            List<CViProtected> protectedVms = new();
            List<CViProtected> unProtectedVms = new();
            try
            {
                protectedVms = csvp.ViProtectedReader().ToList();
                unProtectedVms = csvp.ViUnProtectedReader().ToList();
            }
            catch (Exception ex)
            {
                log.Error(logStart + "Failed to populate VI Protected objects..");
                log.Error(logStart + ex.Message);
            }

            //list to ensure we only count unique VMs
            List<string> countedVMs = new();

            if (csv != null)
                foreach (var c in csv)
                {
                    //string[] s = new string[13];
                    CManagedServer server = new();

                    //match server and VM count
                    int vmCount = 0;
                    int protectedCount = 0;
                    int unProtectedCount = 0;

                    foreach (var p in protectedVms)
                    {
                        if (!countedVMs.Contains(p.Name))
                        {
                            if (p.Path.StartsWith(c.Name))
                            {
                                vmCount++;
                                protectedCount++;
                                countedVMs.Add(p.Name);
                            }
                        }

                    }
                    foreach (var u in unProtectedVms)
                    {
                        if (u.Type == "Vm")
                        {
                            if (!countedVMs.Contains(u.Name))
                            {
                                if (u.Path.StartsWith(c.Name))
                                {
                                    vmCount++;
                                    unProtectedCount++;
                                    countedVMs.Add(u.Name);
                                }
                            }
                        }
                    }

                    //check for VBR Roles
                    CheckServerRoles(c.Id);


                    //scrub name if selected
                    string newName = c.Name;
                    if (scrub)
                        newName = CGlobals.Scrubber.ScrubItem(newName, ScrubItemType.Server);
                    //s[0] = newName;
                    server.Name = newName;
                    server.Cores = c.Cores;
                    server.Ram = c.Ram;
                    server.Type = c.Type;
                    server.ApiVersion = c.ApiVersion;
                    server.ProtectedVms = protectedCount;
                    server.NotProtectedVms = unProtectedCount;
                    server.TotalVms = vmCount;
                    server.IsProxy = _isBackupServerProxy;
                    server.IsRepo = _isBackupServerRepo;
                    server.IsWan = _isBackupServerWan;
                    server.OsInfo = c.OSInfo;
                    server.IsUnavailable = c.IsUnavailable;

                    list.Add(server);
                }
            log.Info(logStart + "converting server info to xml..done!");
            return list;
        }
        public Dictionary<string, int> JobSummaryInfoToXml()
        {
            log.Info(logStart + "converting job summary info to xml");
            List<CJobTypeInfos> csv = CGlobals.DtParser.JobInfos;

            //CQueries cq = _cq;

            List<CModel.EDbJobType> types = new();

            List<string> types2 = csv.Select(x => x.JobType).ToList();
            //foreach (var c in csv)
            //{
            //    types2.Add(c.JobType);
            //}


            Dictionary<string, int> typeSummary = new();

            foreach (var type in types2.Distinct())
            {
                typeSummary.Add(type, types2.Count(x => x == type));
            }
            //foreach (var t in types2)
            //{
            //    int typeCount = 0;
            //    foreach (var t2 in types2)
            //    {
            //        if (t == t2)
            //        {
            //            typeCount++;
            //        }
            //    }
            //    if (!typeSummary.ContainsKey(t))
            //        typeSummary.Add(t, typeCount);
            //}

            //sum of all jobs:


            log.Info(logStart + "converting job summary info to xml..done!");
            return typeSummary;
        }


        public Dictionary<int, string[]> JobConcurrency(bool isJob)
        {
            log.Info(logStart + "calculating concurrency");
            CConcurrencyHelper helper = new();


            List<CJobSessionInfo> trimmedSessionInfo = new();
            CGlobals.Logger.Info(logStart + "Loading Job Sessions for Concurrency...");
            if (CGlobals.DEBUG)
            {
                log.Debug("DEBUG MODE: Loading all Job Sessions for Concurrency...");
                log.Debug("Job Sessions TOTAL = " + CGlobals.DtParser.JobSessions.Count);
                log.Debug("Report interval: " + CGlobals.ReportDays);
            }
            var v = CGlobals.DtParser.JobSessions.Where(c => c.CreationTime >= CGlobals.GetToolStart.AddDays(CGlobals.ReportDays));
            if (CGlobals.DEBUG)
            {
                log.Debug("Job Sessions after filter: " + v.Count());
            }
            trimmedSessionInfo = CGlobals.DtParser.JobSessions.Where(c => c.CreationTime >= CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays)).ToList();
            CGlobals.Logger.Info(logStart + $"Loaded {trimmedSessionInfo.Count} Job Sessions for Concurrency...");

            List<ConcurentTracker> ctList = new();

            if (isJob)
            {
                ctList = helper.JobCounter(trimmedSessionInfo);
                log.Info(logStart + "Jobs to be counted: " + ctList.Count);
            }


            else if (!isJob)
            {
                ctList = helper.TaskCounter(trimmedSessionInfo);
                log.Info(logStart + "Tasks to be counted: " + ctList.Count);

            }





            log.Info(logStart + "calculating concurrency...done!");

            return helper.FinalConcurrency(ctList);
        }

        public Dictionary<string, string> RegOptions()
        {
            Dictionary<string, string> returnDict = new();

            var reg = new CCsvParser();
            //var RegOptions = reg.RegOptionsCsvParser();
            CDefaultRegOptions defaults = new();

            var RegOptions2 = CGlobals.DEFAULTREGISTRYKEYS;

            foreach (var r in RegOptions2)
            {
                string workingValue = "";
                if (r.Value.GetType() == typeof(string[]))
                {
                    var values = r.Value as IEnumerable;
                    List<string> valueArray = new();
                    foreach (var v in values)
                    {
                        valueArray.Add(v.ToString());
                    }

                    workingValue = string.Join("<br>", valueArray);
                }
                else
                    workingValue = r.Value.ToString();


                if (defaults._defaultKeys.ContainsKey(r.Key))
                {
                    string[] skipKeys = CRegistrySkipKeys.SkipKeys;
                    if (skipKeys.Contains(r.Key))
                        continue;
                    defaults._defaultKeys.TryGetValue(r.Key, out string setValue);
                    if (setValue != workingValue)
                    {
                        returnDict.Add(r.Key, workingValue);
                    }
                }
                if (!defaults._defaultKeys.ContainsKey(r.Key))
                {
                    defaults._defaultKeys.TryGetValue(r.Key, out string setValue);
                    returnDict.Add(r.Key, workingValue);
                }
            }


            return returnDict;
        }
        public List<List<string>> JobInfoToXml(bool scrub)
        {
            List<List<string>> sendBack = new();
            log.Info(logStart + "converting job info to xml");
            List<CJobTypeInfos> csv = CGlobals.DtParser.JobInfos;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.JobType).ToList();
            csv = csv.OrderBy(x => x.Name).ToList();


            decimal totalsize = 0;
            if (csv != null)
                foreach (var c in csv)
                {
                    List<string> job = new();
                    string jname = c.Name;
                    string repo = c.RepoName;
                    //if (c.EncryptionEnabled == "True")
                    //_backupsEncrypted = true;
                    if (scrub)
                    {
                        jname = _scrubber.ScrubItem(c.Name, ScrubItemType.Job);
                        repo = _scrubber.ScrubItem(c.RepoName, ScrubItemType.Repository);
                    }
                    decimal.TryParse(c.ActualSize, out decimal actualSize);
                    var trueSize = Math.Round(actualSize / 1024 / 1024 / 1024, 2);
                    totalsize += trueSize;
                    job.Add(jname);
                    job.Add(repo);
                    job.Add(trueSize.ToString());
                    job.Add(c.RestorePoints.ToString());
                    job.Add(c.EncryptionEnabled);
                    job.Add(c.JobType);
                    job.Add(c.Algorithm);
                    job.Add(c.SheduleEnabledTime);
                    job.Add(c.FullBackupDays);
                    //job.Add(c.ScheduleOptions);
                    job.Add(c.FullBackupScheduleKind);
                    job.Add(c.TransformFullToSyntethic);
                    job.Add(c.TransformIncrementsToSyntethic);
                    job.Add(c.TransformToSyntethicDays);

                    sendBack.Add(job);
                }

            // add summary line;
            List<string> summaryline = new() {
            "TOTALS",
            "",
            totalsize.ToString() + " GB",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",

            };
            sendBack.Add(summaryline);


            //doc.Save(_testFile);
            log.Info(logStart + "converting job info to xml..done!");
            return sendBack;
        }
        public List<CJobSummaryTypes> ConvertJobSessSummaryToXml(bool scrub)
        {
            CJobSessSummary jss = new(log, scrub, _scrubber, CGlobals.DtParser);
            return jss.JobSessionSummaryToXml(scrub);

        }



        public int JobSessionInfoToXml(bool scrub)
        {
            try
            {
                string logStart = "[JobSessions]\t";
                IndividualJobSessionsHelper helper = new();

                log.Info(logStart + "converting job session info to xml");

                helper.ParseIndividualSessions(scrub);


                log.Info(logStart + "converting job session info to xml..done!");
                return 0;
            }
            catch (Exception ex)
            {
                return 1;
            }

        }




        #endregion

        #region localFunctions
        private string FilterZeros(decimal value)
        {
            string s = "";

            if (value != 0)
                s = value.ToString();

            return s;
        }


        private void PreCalculations()
        {
            // calc all the things prior to adding XML entries... such as job count per repo....
            List<CJobTypeInfos> jobs = CGlobals.DtParser.JobInfos;
            Dictionary<string, int> repoJobCount = new();
            _repoJobCount = new();
            foreach (var j in jobs)
            {
                if (!repoJobCount.ContainsKey(j.RepoName))
                {
                    repoJobCount.Add(j.RepoName, 1);
                }
                else if (repoJobCount.ContainsKey(j.RepoName))
                {
                    repoJobCount.TryGetValue(j.RepoName, out int current);
                    repoJobCount[j.RepoName] = current + 1;
                }
            }
            _repoJobCount = repoJobCount;
        }

        private void ResetRoles()
        {
            _isBackupServerWan = false;
            _isBackupServerRepo = false;
            _isBackupServerProxy = false;
            //_isBackupServerProxyDisabled = false;
        }
        private void CheckServerRoles(string serverId)
        {
            log.Info("Checking server roles.. for server: " + serverId);
            ResetRoles();

            List<CProxyTypeInfos> proxy = CGlobals.DtParser.ProxyInfos;
            List<CRepoTypeInfos> extents = CGlobals.DtParser.ExtentInfo;
            List<CRepoTypeInfos> repos = CGlobals.DtParser.RepoInfos;
            List<CWanTypeInfo> wans = CGlobals.DtParser.WanInfos;

            _isBackupServerProxy = CheckProxyRole(serverId);

            // if(proxy != null){
            //     log.Debug("Proxy count: " + proxy.Count);
            // }
            // if(extents != null){
            //     log.Debug("Extent count: " + extents.Count);
            // }
            // if(repos != null){
            //     log.Debug("Repo count: " + repos.Count);
            // }
            // if(wans != null){
            //     log.Debug("Wan count: " + wans.Count);
            // }


            foreach (var e in extents)
            {
                if (e.HostId == serverId)
                    _isBackupServerRepo = true;
            }
            foreach (var r in repos)
            {
                if (r.HostId == serverId)
                    _isBackupServerRepo = true;
            }
            foreach (var w in wans)
            {
                if (w.HostId == serverId)
                    _isBackupServerWan = true;
            }
            log.Info("Checking server roles..done!");
        }
        private bool CheckProxyRole(string serverId)
        {
            // var viProxy = _csvParser.GetDynViProxy();
            // var hvProxy = _csvParser.GetDynHvProxy();
            // var nasProxy = _csvParser.GetDynNasProxy();
            // var cdpProxy = _csvParser.GetDynCdpProxy();
            if (_viProxy != null)
                foreach (var v in _viProxy.ToList())
                {
                    if (v.hostid == serverId)
                        return true;
                }
            if (_hvProxy != null)
                foreach (var h in _hvProxy)
                {
                    if (h.id == serverId)
                        return true;
                }
            if (_nasProxy != null)
                foreach (var n in _nasProxy)
                {
                    if (n.hostid == serverId)
                        return true;
                }
            if (_cdpProxy != null)
                foreach (var c in _cdpProxy)
                {
                    if (c.serverid == serverId)
                        return true;
                }
            return false;
        }

        private decimal FreePercent(int freespace, int totalspace)
        {
            if (totalspace != 0)
            {
                double n = freespace / (double)totalspace * 100;
                return Math.Round((decimal)n, 1);
            }
            return 0;

        }
        #endregion

    }
}
