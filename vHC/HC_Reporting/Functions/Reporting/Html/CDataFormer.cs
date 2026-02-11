// <copyright file="CDataFormer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

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

// using VeeamHealthCheck.Functions.Reporting.Html.VBR.VBR_Tables.Repositories;
using VeeamHealthCheck.Functions.Reporting.RegSettings;
using VeeamHealthCheck.Reporting.Html.VBR;

// using VeeamHealthCheck.Reporting.Html.VBR.Managed_Server_Table;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

// using VeeamHealthCheck.Common;
using VeeamHealthCheck.Shared.Logging;

// using static VeeamHealthCheck.Functions.Collection.DB.CModel;
using static VeeamHealthCheck.Functions.Collection.DB.CModel;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    /// <summary>
    /// Handles data formation and conversion for HTML reporting functionality.
    /// </summary>
    public class CDataFormer
    {
        private readonly string logStart = "[DataFormer]\t";

        private bool isBackupServerProxy;
        private bool isBackupServerRepo;
        private bool isBackupServerWan;
        private Dictionary<string, int> repoJobCount;
        private readonly CScrubHandler scrubber = CGlobals.Scrubber;

        private readonly CLogger log = CGlobals.Logger;

        // Caches for expensive data conversions (avoid redundant CSV reads and re-computation)
        private List<string[]> _cachedProxyData;
        private bool _cachedProxyScrub;
        private List<CManagedServer> _cachedServerData;
        private bool _cachedServerScrub;

        // Use null-coalescing to ensure we always have a valid list, even if CSV files are missing
        private readonly IEnumerable<dynamic> viProxy;
        private readonly IEnumerable<dynamic> hvProxy;
        private readonly IEnumerable<dynamic> nasProxy;
        private readonly IEnumerable<dynamic> cdpProxy;

        public CDataFormer() // add string mode input
        {
            // Initialize proxy collections with null-safety
            viProxy = (CCsvParser.GetDynViProxy() ?? Enumerable.Empty<dynamic>()).ToList();
            hvProxy = (CCsvParser.GetDynHvProxy() ?? Enumerable.Empty<dynamic>()).ToList();
            nasProxy = (CCsvParser.GetDynNasProxy() ?? Enumerable.Empty<dynamic>()).ToList();
            cdpProxy = (CCsvParser.GetDynCdpProxy() ?? Enumerable.Empty<dynamic>()).ToList();
            
            // Log if any proxy data is missing
            if (!viProxy.Any() && !hvProxy.Any() && !nasProxy.Any() && !cdpProxy.Any())
            {
                log.Warning($"{logStart}No proxy CSV data found. Proxy-related sections may be empty in the report.");
            }
        }


        #region XML Conversions

        public CSecuritySummaryTable SecSummary()
        {
            CSecuritySummaryTable t = new();
            List<int> secSummary = new List<int>();
            var csv = new CCsvParser();
            try
            {
                var vbrInfo = csv.GetDynamicVbrInfo();
                if (vbrInfo.Any(x => x.mfa == "True"))
                {
                    t.MFAEnabled = true;
                }
                else
                {
                    t.MFAEnabled = false;
                }

                // Four Eyes Authorization from vbrinfo.csv
                if (vbrInfo.Any(x => x.foureyes == "True"))
                {
                    t.FourEyesEnabled = true;
                }
                else if (vbrInfo.Any(x => x.foureyes == "False"))
                {
                    t.FourEyesEnabled = false;
                }
                else
                {
                    t.FourEyesEnabled = false; // default when not present (older versions)
                }
            }
            catch (Exception)
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

                    // return secSummary;
                }

                else if (sobrRepo.Any(x => x.immute == "True"))
                {
                    t.ImmutabilityEnabled = true;

                    // return secSummary;
                }

                else if (extRepo.Any(x => x.isimmutabilitysupported == "True"))
                {
                    t.ImmutabilityEnabled = true;

                    // return secSummary;
                }
                else
                {
                    t.ImmutabilityEnabled = false;
                }
            }
            catch (Exception)
            {
                this.log.Error(this.logStart + "Unable to find immutability. Marking false");
                t.ImmutabilityEnabled = false;
            }

            try
            {
                var netTraffic = csv.GetDynamincNetRules();
                if (netTraffic.Any(x => x.encryptionenabled == "True"))
                {
                    t.TrafficEncrptionEnabled = true;
                }
                else
                {
                    t.TrafficEncrptionEnabled = false;
                }
            }
            catch (Exception)
            {
                this.log.Info(this.logStart + "Traffic encryption not detected. Marking false");
                t.TrafficEncrptionEnabled = false;
            }

            try
            {
                var backupEnc = csv.GetDynamicJobInfo();
                if (backupEnc.Any(x => x.pwdkeyid != "00000000-0000-0000-0000-000000000000" && !string.IsNullOrEmpty(x.pwdkeyid)))
                {
                    t.BackupFileEncrptionEnabled = true;
                }
                else
                {
                    t.BackupFileEncrptionEnabled = false;
                }
            }
            catch (Exception)
            {
                this.log.Error(this.logStart + "Unable to detect backup encryption. Marking false");
                t.BackupFileEncrptionEnabled = false;
            }

            try
            {
                var cBackup = csv.GetDynamincConfigBackup();
                if (cBackup.Any(x => x.encryptionoptions == "True"))
                {
                    t.ConfigBackupEncrptionEnabled = true;
                }
                else
                {
                    t.ConfigBackupEncrptionEnabled = false;
                }
            }
            catch (Exception)
            {
                this.log.Error(this.logStart + "Config backup not detected. Marking false");

                // log.Info(e.Message);
                t.ConfigBackupEncrptionEnabled = false;
            }

            return t;
        }

        public Dictionary<string, int> ServerSummaryToXml()
        {
            this.log.Info(this.logStart + "converting server summary to xml");

            // Dictionary<string, int> di = _dTypeParser.ServerSummaryInfo;
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
                // customize the log line:
                this.log.Info(this.logStart + "Converting protected workloads data to xml...");

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
                    // _hvDupes = 0;
                    // _hvNotProtectedNames = 0;
                    // _hvProtectedNames = List<string>;
                }

                #endregion

                #region physProtected

                // var physProtected = csvp.PhysProtectedReader().ToList();
                // var physNotProtected = csvp.PhysNotProtectedReader().ToList();
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
                    {

                        if (p.name.Contains(v.Name))
                        {
                            vmProtectedByPhys.Add(v.Name);
                        }
                    }

                    foreach (var w in unProtectedVms)
                    {
                        if (p.name.Contains(w.Name))
                        {
                            vmProtectedByPhys.Add(w.Name);
                        }
                    }
                }

                // var xml2 = XML.AddXelement(protectedVms.Count.ToString(), "ViProtected");
                this.viProtectedNames = viProtectedNames;
                this.viNotProtectedNames = viNotProtectedNames;
                this.viDupes = viDupes;
                this.vmProtectedByPhys = vmProtectedByPhys;
                this.physNotProtNames = physNotProtNames;
                this.physProtNames = physProtNames;

                this.hvProtectedNames = hvProtectedNames;
                this.hvNotProtectedNames = hvNotProtectedNames;
                this.hvDupes = hvDupes;

                this.log.Info(this.logStart + "Converting protected workloads data to xml..done!");
                return 0;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        public int viDupes;
        public List<string> vmProtectedByPhys;
        public List<string> viProtectedNames;

        // public List<string> _vmNotProtectedNames;
        public int hvDupes;
        public List<string> hvProtectedNames;
        public List<string> hvNotProtectedNames;

        public List<string> viNotProtectedNames;
        public List<string> physNotProtNames;
        public List<string> physProtNames;

        public BackupServer BackupServerInfoToXml(bool scrub)
        {
            this.log.Info(this.logStart + "converting backup server info to xml");
            List<string> list = new List<string>();
            CBackupServerTableHelper bt = new(scrub);
            BackupServer b = bt.SetBackupServerData();

           this.CheckServerRoles(CGlobals.backupServerId);

            b.HasProxyRole = this.isBackupServerProxy;
            b.HasRepoRole = this.isBackupServerRepo;
            b.HasWanAccRole = this.isBackupServerWan;

            this.log.Info(this.logStart + "converting backup server info to xml..done!");
            return b;
        }

        public List<CSobrTypeInfos> SobrInfoToXml(bool scrub)
        {
            this.PreCalculations();
            this.log.Info(this.logStart + "Starting SOBR conversion to xml..");
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
                {
                    newName = this.scrubber.ScrubItem(c.Name, ScrubItemType.SOBR);
                }


                this.repoJobCount.TryGetValue(c.Name, out int jobCount);

                // s[0] += newName;
                // s[1] += repoCount;
                // s[2] += jobCount;
                // s[3] += c.PolicyType;
                // s[4] += c.EnableCapacityTier;
                // s[5] += c.CapacityTierCopyPolicyEnabled;
                // s[6] += c.CapacityTierMovePolicyEnabled;
                // s[7] += c.ArchiveTierEnabled;
                // s[8] += c.UsePerVMBackupFiles;
                // s[9] += c.CapTierType;
                // s[10] += c.ImmuteEnabled;
                // s[11] += c.ImmutePeriod;
                // s[12] += c.SizeLimitEnabled;
                // s[13] += c.SizeLimit;
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
                    SizeLimit = c.SizeLimit,
                };

                outList.Add(sobr);
            }

            this.log.Info(this.logStart + "Starting SOBR conversion to xml..done!");
            return outList;
        }

        private string SetGateHosts(string original, bool scrub)
        {
            string[] hosts = original.Split(' ');
            if (hosts.Count() == 1 && String.IsNullOrEmpty(hosts[0]))
            {
                return hosts[0];
            }

            string r = string.Empty;
            int counter = 1;
            int end = hosts.Length;
            foreach (string host in hosts)
            {
                string newhost = host;
                if (scrub)
                {
                    newhost = CGlobals.Scrubber.ScrubItem(host, ScrubItemType.Server);
                }


                if (counter == end)
                {
                    r += newhost + "<br>";
                }
                else
                {
                    r += newhost + ",<br>";
                }


                counter++;
            }

            return r;
        }

        public List<CRepository> ExtentXmlFromCsv(bool scrub)
        {
            this.log.Info(this.logStart + "converting extent info to xml");
            List<string[]> list = new List<string[]>();
            List<CRepoTypeInfos> csv = CGlobals.DtParser.ExtentInfo;
            
            if (csv == null || csv.Count == 0)
            {
                this.log.Warning(this.logStart + "ExtentInfo is null or empty. No SOBR extent data available.");
                return new List<CRepository>();
            }
            
            this.log.Info(this.logStart + $"Found {csv.Count} extent records to process");
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.SobrName).ToList();
            List<CRepository> repoList = new();

            if (csv != null)
            {

                foreach (var c in csv)
                {
                    string newName = c.RepoName;
                    string sobrName = c.SobrName;
                    string hostName = c.Host;
                    string path = c.Path;
                    string gates = this.SetGateHosts(c.GateHosts, scrub);

                    if (scrub)
                    {
                        newName = CGlobals.Scrubber.ScrubItem(newName, ScrubItemType.Repository);
                        sobrName = CGlobals.Scrubber.ScrubItem(sobrName, ScrubItemType.SOBR);
                        hostName = CGlobals.Scrubber.ScrubItem(hostName, ScrubItemType.Server);
                        path = CGlobals.Scrubber.ScrubItem(path, ScrubItemType.Path);
                    }

                    string type;
                    if (c.TypeDisplay == null)
                    {
                        type = c.Type;
                    }
                    else
                    {
                        type = c.TypeDisplay;
                    }


                    var freePercent = this.FreePercent(c.FreeSPace, c.TotalSpace);

                    string gateHosts = string.Empty;
                    if (c.IsAutoGateway)
                    {
                        gateHosts = string.Empty;
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
            }


            this.log.Info(this.logStart + "converting extent info to xml..done!");
            return repoList;
        }


        public List<CRepository> RepoInfoToXml(bool scrub)
        {
            this.PreCalculations();
            this.log.Info(this.logStart + "converting repository info to xml");
            List<CRepository> list = new();

            List<CRepoTypeInfos> csv = CGlobals.DtParser.RepoInfos.ToList();
            csv = csv.OrderBy(x => x.Name).ToList();
            if (csv != null)
            {

                foreach (var c in csv)
                {
                    string[] s = new string[18];
                    string name = c.Name;
                    string host = c.Host;
                    string path = c.Path;
                    string gates = this.SetGateHosts(c.GateHosts, scrub);
                    if (scrub)
                    {
                        name = this.scrubber.ScrubItem(c.Name, ScrubItemType.Repository);
                        host = this.scrubber.ScrubItem(c.Host, ScrubItemType.Server);
                        path = this.scrubber.ScrubItem(c.Path, ScrubItemType.Path);
                    }

                    decimal free = Math.Round((decimal)c.FreeSPace / 1024, 2);
                    decimal total = Math.Round((decimal)c.TotalSpace / 1024, 2);
                    decimal freePercent = this.FreePercent(c.FreeSPace, c.TotalSpace);
                    string freeSpace = free.ToString();
                    string totalSpace = total.ToString();
                    string percentFree = freePercent.ToString();
                    if (c.TotalSpace == 0)
                    {
                        freeSpace = this.FilterZeros(free);
                        totalSpace = this.FilterZeros(total);
                        percentFree = string.Empty;
                    }

                    this.repoJobCount.TryGetValue(c.Name, out int jobCount);

                    string hosts = string.Empty;
                    s[0] += name;
                    s[1] += c.MaxTasks;
                    s[2] += jobCount;
                    s[3] += c.Cores;
                    s[4] += c.Ram;
                    s[5] += c.IsAutoGateway;
                    if (c.IsAutoGateway)
                    {
                        hosts = string.Empty;
                    }

                    else if (String.IsNullOrEmpty(c.GateHosts))
                    {
                        hosts = host;
                    }
                    else
                    {
                        hosts = gates;
                    }


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
            }


            this.log.Info(this.logStart + "converting repository info to xml..done!");
            return list;
        }

        public List<string[]> ProxyXmlFromCsv(bool scrub)
        {
            // Return cached result if available for the same scrub mode
            if (_cachedProxyData != null && _cachedProxyScrub == scrub)
            {
                this.log.Info("converting proxy info to xml (cached)");
                return _cachedProxyData;
            }

            this.log.Info("converting proxy info to xml");
            List<string[]> list = new();

            List<CProxyTypeInfos> csv = CGlobals.DtParser.ProxyInfos;

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(y => y.Type).ToList();

            if (csv != null)
            {

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
            }


            this.log.Info(this.logStart + "converting proxy info to xml..done!");

            _cachedProxyData = list;
            _cachedProxyScrub = scrub;
            return list;
        }

        public List<CManagedServer> ServerXmlFromCsv(bool scrub)    // managed servers protect vm count
        {
            // Return cached result if available for the same scrub mode
            if (_cachedServerData != null && _cachedServerScrub == scrub)
            {
                this.log.Info(this.logStart + "converting server info to xml (cached)");
                return _cachedServerData;
            }

            this.log.Info(this.logStart + "converting server info to xml");
            List<CManagedServer> list = new();
            List<CServerTypeInfos> csv = CGlobals.ServerInfo;

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(x => x.Type).ToList();

            CCsvParser csvp = new();

            // Cache the requirements CSV once instead of re-reading per server
            List<CRequirementsCsvInfo> cachedReqRows = null;
            try
            {
                cachedReqRows = csvp.ServersRequirementsCsvParser()?.ToList();
            }
            catch (Exception ex)
            {
                this.log.Warning(this.logStart + "Failed to pre-load requirements CSV: " + ex.Message);
            }

            List<CViProtected> protectedVms = new();
            List<CViProtected> unProtectedVms = new();
            try
            {
                protectedVms = csvp.ViProtectedReader().ToList();
                unProtectedVms = csvp.ViUnProtectedReader().ToList();
            }
            catch (Exception ex)
            {
                this.log.Error(this.logStart + "Failed to populate VI Protected objects..");
                this.log.Error(this.logStart + ex.Message);
            }

            // list to ensure we only count unique VMs
            List<string> countedVMs = new();

            if (csv != null)
            {

                foreach (var c in csv)
                {
                    // string[] s = new string[13];
                    CManagedServer server = new();

                    // match server and VM count
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

                    // check for VBR Roles (using cached requirements CSV)
                   this.CheckServerRoles(c.Id, cachedReqRows);

                    // scrub name if selected
                    string newName = c.Name;
                    if (scrub)
                    {
                        newName = CGlobals.Scrubber.ScrubItem(newName, ScrubItemType.Server);
                    }

                    // s[0] = newName;
                    server.Name = newName;
                    server.Cores = c.Cores;
                    server.Ram = c.Ram;
                    server.Type = c.Type;
                    server.ApiVersion = c.ApiVersion;
                    server.ProtectedVms = protectedCount;
                    server.NotProtectedVms = unProtectedCount;
                    server.TotalVms = vmCount;
                    server.IsProxy = this.isBackupServerProxy;
                    server.IsRepo = this.isBackupServerRepo;
                    server.IsWan = this.isBackupServerWan;
                    server.OsInfo = c.OSInfo;
                    server.IsUnavailable = c.IsUnavailable;

                    list.Add(server);
                }
            }


            this.log.Info(this.logStart + "converting server info to xml..done!");
            _cachedServerData = list;
            _cachedServerScrub = scrub;
            return list;
        }

        public Dictionary<string, int> JobSummaryInfoToXml()
        {
            this.log.Info(this.logStart + "converting job summary info to xml");
            List<CJobTypeInfos> csv = CGlobals.DtParser.JobInfos;

            // CQueries cq = _cq;
            List<CModel.EDbJobType> types = new();

            List<string> types2 = csv.Select(x => x.JobType).ToList();

            // foreach (var c in csv)
            // {
            //    types2.Add(c.JobType);
            // }
            Dictionary<string, int> typeSummary = new();

            foreach (var type in types2.Distinct())
            {
                typeSummary.Add(type, types2.Count(x => x == type));
            }

            // foreach (var t in types2)
            // {
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
            // }

            // sum of all jobs:
            this.log.Info(this.logStart + "converting job summary info to xml..done!");
            return typeSummary;
        }

        public Dictionary<int, string[]> JobConcurrency(bool isJob)
        {
            this.log.Info(this.logStart + "calculating concurrency");
            CConcurrencyHelper helper = new();

            List<CJobSessionInfo> trimmedSessionInfo = new();
            CGlobals.Logger.Info(this.logStart + "Loading Job Sessions for Concurrency...");
            if (CGlobals.DEBUG)
            {
                this.log.Debug("DEBUG MODE: Loading all Job Sessions for Concurrency...");
                this.log.Debug("Job Sessions TOTAL = " + CGlobals.DtParser.JobSessions.Count);
                this.log.Debug("Report interval: " + CGlobals.ReportDays);
            }

            var v = CGlobals.DtParser.JobSessions.Where(c => c.CreationTime >= CGlobals.GetToolStart.AddDays(CGlobals.ReportDays));
            if (CGlobals.DEBUG)
            {
                this.log.Debug("Job Sessions after filter: " + v.Count());
            }

            trimmedSessionInfo = CGlobals.DtParser.JobSessions.Where(c => c.CreationTime >= CGlobals.GetToolStart.AddDays(-CGlobals.ReportDays)).ToList();
            CGlobals.Logger.Info(this.logStart + $"Loaded {trimmedSessionInfo.Count} Job Sessions for Concurrency...");

            List<ConcurentTracker> ctList = new();

            if (isJob)
            {
                ctList = helper.JobCounter(trimmedSessionInfo);
                this.log.Info(this.logStart + "Jobs to be counted: " + ctList.Count);
            }

            else if (!isJob)
            {
                ctList = helper.TaskCounter(trimmedSessionInfo);
                this.log.Info(this.logStart + "Tasks to be counted: " + ctList.Count);
            }

            this.log.Info(this.logStart + "calculating concurrency...done!");

            return helper.FinalConcurrency(ctList);
        }

        public Dictionary<string, string> RegOptions()
        {
            Dictionary<string, string> returnDict = new();

            var reg = new CCsvParser();

            // var RegOptions = reg.RegOptionsCsvParser();
            CDefaultRegOptions defaults = new();

            var RegOptions2 = CGlobals.DEFAULTREGISTRYKEYS;

            foreach (var r in RegOptions2)
            {
                string workingValue = string.Empty;
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

                if (defaults.defaultKeys.ContainsKey(r.Key))
                {
                    string[] skipKeys = CRegistrySkipKeys.SkipKeys;
                    if (skipKeys.Contains(r.Key))
                    {
                        continue;
                    }


                    defaults.defaultKeys.TryGetValue(r.Key, out string setValue);
                    if (setValue != workingValue)
                    {
                        returnDict.Add(r.Key, workingValue);
                    }
                }

                if (!defaults.defaultKeys.ContainsKey(r.Key))
                {
                    defaults.defaultKeys.TryGetValue(r.Key, out string setValue);
                    returnDict.Add(r.Key, workingValue);
                }
            }

            return returnDict;
        }


        public List<CJobSummaryTypes> ConvertJobSessSummaryToXml(bool scrub)
        {
            CJobSessSummary jss = new(this.log, scrub, this.scrubber, CGlobals.DtParser);
            return jss.JobSessionSummaryToXml(scrub);
        }

        public int JobSessionInfoToXml(bool scrub)
        {
            try
            {
                string logStart = "[JobSessions]\t";
                IndividualJobSessionsHelper helper = new();

                this.log.Info(logStart + "converting job session info to xml");

                helper.ParseIndividualSessions(scrub);

                this.log.Info(logStart + "converting job session info to xml..done!");
                return 0;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        public List<string[]> ServersRequirementsToXml(bool scrub)
        {
            var csvp = new CCsvParser();
            var rows = csvp.ServersRequirementsCsvParser(); 
            return RequirementsToStringArray(rows, scrub);
        }

        private List<string[]> RequirementsToStringArray(IEnumerable<CRequirementsCsvInfo> rows, bool scrub)
        {
            var list = new List<string[]>();
            if (rows == null) return list;

            foreach (var r in rows)
            {
                var server = r.Server;
                var names = r.Names;

                if (scrub)
                {
                    server = CGlobals.Scrubber.ScrubItem(server, ScrubItemType.Server);
                    names = CGlobals.Scrubber.ScrubItem(names, ScrubItemType.Server);
                }

                // Summarize repeated role types (e.g., "Gateway/ Gateway/ Repository" -> "Gateway ×2<br>Repository ×1")
                var summarizedType = SummarizeRoleTypes(r.Type);

                list.Add(new[]
                {
                    server,
                    summarizedType,
                    r.RequiredCores,
                    r.AvailableCores,
                    r.RequiredRamGb,
                    r.AvailableRamGb,
                    r.ConcurrentTasks,
                    r.SuggestedTasks,
                    names
                });
            }

            return list;
        }

        /// <summary>
        /// Summarizes repeated role types into a count format with friendly names.
        /// Example: "Gateway/ Gateway/ Gateway/ Repository/ Gateway" becomes "Gateway Server ×4<br>Repository ×1"
        /// </summary>
        private string SummarizeRoleTypes(string typeString)
        {
            if (string.IsNullOrWhiteSpace(typeString))
                return typeString;

            // Split by "/ " or "/" to get individual types
            var types = typeString.Split(new[] { "/ ", "/" }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim())
                                  .Where(t => !string.IsNullOrWhiteSpace(t))
                                  .Select(t => GetFriendlyRoleName(t)) // Convert to friendly names
                                  .ToList();

            if (types.Count == 0)
                return typeString;

            if (types.Count == 1)
                return types[0];

            // Count occurrences of each type
            var typeCounts = types.GroupBy(t => t)
                                  .OrderByDescending(g => g.Count())
                                  .ThenBy(g => g.Key)
                                  .Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key);

            // Join with HTML line breaks for vertical display
            return string.Join("<br>", typeCounts);
        }

        /// <summary>
        /// Maps technical role type names to user-friendly display names.
        /// </summary>
        private static readonly Dictionary<string, string> RoleNameMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "GPProxy", "File Proxy" },
            { "Proxy", "VMware Proxy" },
            { "CDPProxy", "CDP Proxy" },
            { "Gateway", "Gateway Server" },
            { "Repository", "Repository" },
            { "BackupServer", "Backup Server" },
            { "SQLServer", "SQL Server" }
        };

        private static string GetFriendlyRoleName(string technicalName)
        {
            if (string.IsNullOrWhiteSpace(technicalName))
                return technicalName;

            return RoleNameMappings.TryGetValue(technicalName.Trim(), out var friendlyName)
                ? friendlyName
                : technicalName; // Return original if no mapping found
        }

        #endregion

        #region localFunctions
        private string FilterZeros(decimal value)
        {
            string s = string.Empty;

            if (value != 0)
            {
                s = value.ToString();
            }


            return s;
        }

        private void PreCalculations()
        {
            // calc all the things prior to adding XML entries... such as job count per repo....
            List<CJobTypeInfos> jobs = CGlobals.DtParser.JobInfos;
            Dictionary<string, int> repoJobCount = new();
            this.repoJobCount = new();
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

            this.repoJobCount = repoJobCount;
        }

        private void ResetRoles()
        {
            this.isBackupServerWan = false;
            this.isBackupServerRepo = false;
            this.isBackupServerProxy = false;

            // _isBackupServerProxyDisabled = false;
        }

        private void CheckServerRoles(string serverId, List<CRequirementsCsvInfo> reqRows = null)
        {
            log.Info($"Checking server roles.. for server: {serverId}");
            ResetRoles();

            // Prefer using the AllServersRequirementsComparison.csv when it contains BackupServer entries.
            try
            {
                log.Info("Roles: checking requirements CSV for BackupServer roles...");

                // Does the CSV contain any BackupServer type entries?
                if (reqRows != null && reqRows.Any(r => !string.IsNullOrEmpty(r.Type) && r.Type.IndexOf("BackupServer", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    // Try to match CSV row by multiple strategies: serverId, server name lookup, partial matches
                    var serverName = CGlobals.ServerInfo?.FirstOrDefault(s => s.Id == serverId)?.Name;

                    CRequirementsCsvInfo matching = null;

                    // 1) exact match against serverId (CSV may contain IP or short name which sometimes equals id)
                    matching = reqRows.FirstOrDefault(r => string.Equals(r.Server?.Trim(), serverId?.Trim(), StringComparison.OrdinalIgnoreCase)
                                                          && !string.IsNullOrEmpty(r.Type));

                    // 2) exact match against serverName
                    if (matching == null && !string.IsNullOrEmpty(serverName))
                    {
                        matching = reqRows.FirstOrDefault(r => string.Equals(r.Server?.Trim(), serverName?.Trim(), StringComparison.OrdinalIgnoreCase)
                                                              && !string.IsNullOrEmpty(r.Type));
                    }

                    // 3) partial contains (either direction) to handle "Name / Something" entries or IP vs FQDN
                    if (matching == null && !string.IsNullOrEmpty(serverName))
                    {
                        matching = reqRows.FirstOrDefault(r => !string.IsNullOrEmpty(r.Server) && (
                            r.Server.IndexOf(serverName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            serverName.IndexOf(r.Server, StringComparison.OrdinalIgnoreCase) >= 0) && !string.IsNullOrEmpty(r.Type));
                    }

                    if (matching != null)
                    {
                        isBackupServerProxy = matching.Type.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0;
                        isBackupServerRepo = matching.Type.IndexOf("Repository", StringComparison.OrdinalIgnoreCase) >= 0;
                        log.Info($"Roles: determined from CSV - isProxy={isBackupServerProxy}, isRepo={isBackupServerRepo}");
                    }
                    else
                    {
                        // No matching CSV row for this server — do not mark proxy/repo
                        isBackupServerProxy = false;
                        isBackupServerRepo = false;
                        log.Info($"Roles: no matching requirements CSV row for serverId='{serverId}' serverName='{serverName}'; not marking proxy or repo.");
                    }
                }
                else
                {
                    // No BackupServer info in CSV -> do not mark as proxy
                    log.Info("Roles: requirements CSV does not contain BackupServer entries; not marking as proxy.");
                    isBackupServerProxy = false;
                }
            }
            catch (Exception ex)
            {
                log.Warning($"Roles: error reading requirements CSV; not marking proxy: {ex.Message}");
                isBackupServerProxy = false;
            }

            // Repositories and extents: if CSV provided repo info we already set isBackupServerRepo above, otherwise use legacy DT parser checks.
            var extents = CGlobals.DtParser.ExtentInfo ?? new List<CRepoTypeInfos>();
            var repos = CGlobals.DtParser.RepoInfos ?? new List<CRepoTypeInfos>();
            var wans = CGlobals.DtParser.WanInfos ?? new List<CWanTypeInfo>();

            if (!isBackupServerRepo)
            {
                log.Info($"Roles: extents loop start (count={extents.Count})");
                foreach (var e in extents)
                {
                    if (e.HostId == serverId)
                    {
                        isBackupServerRepo = true;
                        break;
                    }
                }
                log.Info("Roles: extents loop done");

                log.Info($"Roles: repos loop start (count={repos.Count})");
                foreach (var r in repos)
                {
                    if (r.HostId == serverId)
                    {
                        isBackupServerRepo = true;
                        break;
                    }
                }
                log.Info("Roles: repos loop done");
            }

            log.Info($"Roles: wans loop start (count={wans.Count})");
            foreach (var w in wans)
                if (w.HostId == serverId) isBackupServerWan = true;
            log.Info("Roles: wans loop done");

            log.Info("Checking server roles..done!");
        }


        private bool CheckProxyRole(string serverId)
        {
            // var viProxy = _csvParser.GetDynViProxy();
            // var hvProxy = _csvParser.GetDynHvProxy();
            // var nasProxy = _csvParser.GetDynNasProxy();
            // var cdpProxy = _csvParser.GetDynCdpProxy();
            if (this.viProxy != null)
            {

                foreach (var v in this.viProxy.ToList())
                {
                    if (v.hostid == serverId)
                    {

                        return true;
                    }
                }
            }


            if (this.hvProxy != null)
            {

                foreach (var h in this.hvProxy)
                {
                    if (h.id == serverId)
                    {
                        return true;
                    }
                }
            }

            if (this.nasProxy != null)
            {
                foreach (var n in this.nasProxy)
                {
                    if (n.hostid == serverId)
                    {
                        return true;
                    }
                }
            }

            if (this.cdpProxy != null)
            {
                foreach (var c in this.cdpProxy)
                {
                    if (c.serverid == serverId)
                    {
                        return true;
                    }
                }
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

        /// <summary>
        /// Extracts Capacity Tier extents from the parsed CSV data
        /// </summary>
        public List<CCapacityTierExtent> CapacityTierXmlFromCsv(bool scrub)
        {
            this.log.Info(this.logStart + "Converting capacity tier extent info to xml");
            List<CCapacityTierExtent> capacityExtents = new();

            try
            {
                var capacityTierCsvData = CGlobals.DtParser.SobrInfo;
                if (capacityTierCsvData == null || capacityTierCsvData.Count == 0)
                {
                    this.log.Info(this.logStart + "No SOBR data available for capacity tier extraction");
                    return capacityExtents;
                }

                foreach (var sobr in capacityTierCsvData)
                {
                    // Only process SOBR entries that have capacity tier enabled
                    if (!sobr.EnableCapacityTier)
                    {
                        continue;
                    }

                    string sobrName = sobr.Name;
                    string capacityTierName = sobr.CapacityExtent;

                    if (scrub)
                    {
                        sobrName = CGlobals.Scrubber.ScrubItem(sobrName, ScrubItemType.SOBR);
                        capacityTierName = CGlobals.Scrubber.ScrubItem(capacityTierName, ScrubItemType.Repository);
                    }

                    // Create capacity tier extent from SOBR settings
                    var capacityExtent = new CCapacityTierExtent
                    {
                        Name = capacityTierName,
                        SobrName = sobrName,
                        ParentSobrId = sobr.Id,
                        Type = sobr.CapTierType,
                        ImmutableEnabled = sobr.ImmuteEnabled,
                        ImmutablePeriod = sobr.ImmutePeriod,
                        SizeLimitEnabled = sobr.SizeLimitEnabled,
                        Status = "Enabled", // Capacity tier is enabled if we're processing it
                        TierType = "Capacity"
                    };

                    capacityExtents.Add(capacityExtent);
                }
            }
            catch (Exception e)
            {
                this.log.Error(this.logStart + "Failed to extract capacity tier extents: " + e.Message);
            }

            this.log.Info(this.logStart + "Converting capacity tier extent info to xml..done!");
            return capacityExtents;
        }

        /// <summary>
        /// Extracts Archive Tier extents from the parsed CSV data
        /// </summary>
        public List<CArchiveTierExtent> ArchiveTierXmlFromCsv(bool scrub)
        {
            this.log.Info(this.logStart + "Converting archive tier extent info to xml");
            List<CArchiveTierExtent> archiveExtents = new();

            try
            {
                var archiveTierCsvData = CGlobals.DtParser.SobrInfo;
                if (archiveTierCsvData == null || archiveTierCsvData.Count == 0)
                {
                    this.log.Info(this.logStart + "No SOBR data available for archive tier extraction");
                    return archiveExtents;
                }

                foreach (var sobr in archiveTierCsvData)
                {
                    // Only process SOBR entries that have archive tier enabled
                    if (!sobr.ArchiveTierEnabled)
                    {
                        continue;
                    }

                    string sobrName = sobr.Name;
                    string archiveExtentName = sobr.ArchiveExtent;

                    if (scrub)
                    {
                        sobrName = CGlobals.Scrubber.ScrubItem(sobrName, ScrubItemType.SOBR);
                        archiveExtentName = CGlobals.Scrubber.ScrubItem(archiveExtentName, ScrubItemType.Repository);
                    }

                    // Create archive tier extent from SOBR settings
                    var archiveExtent = new CArchiveTierExtent
                    {
                        SobrName = sobrName,
                        Name = archiveExtentName,
                        Type = "Archive", // Will be populated in Phase 2 when separate archive extent CSV is available
                        Status = "Enabled", // Archive tier is enabled if we're processing it
                        RetentionPeriod = sobr.ArchivePeriod,
                        ImmutableEnabled = false, // Will be populated in Phase 2
                        ImmutablePeriod = string.Empty, // Will be populated in Phase 2
                        SizeLimitEnabled = false, // Will be populated in Phase 2
                        SizeLimit = string.Empty, // Will be populated in Phase 2
                        CostOptimizedEnabled = sobr.CostOptimizedArchiveEnabled,
                        FullBackupModeEnabled = sobr.ArchiveFullBackupModeEnabled
                    };

                    archiveExtents.Add(archiveExtent);
                }
            }
            catch (Exception e)
            {
                this.log.Error(this.logStart + "Failed to extract archive tier extents: " + e.Message);
            }

            this.log.Info(this.logStart + "Converting archive tier extent info to xml..done!");
            return archiveExtents;
        }

        /// <summary>
        /// Extracts Performance Tier (primary) extents from the parsed CSV data
        /// </summary>
        public List<CPerformanceTierExtent> PerformanceTierXmlFromCsv(bool scrub)
        {
            this.log.Info(this.logStart + "Converting performance tier extent info to xml");
            List<CPerformanceTierExtent> perfExtents = new();

            try
            {
                var perfTierCsvData = CGlobals.DtParser.ExtentInfo;
                if (perfTierCsvData == null || perfTierCsvData.Count == 0)
                {
                    this.log.Warning(this.logStart + "No performance tier extent data available");
                    return perfExtents;
                }

                var orderedList = perfTierCsvData.OrderBy(x => x.RepoName).ThenBy(y => y.SobrName).ToList();

                foreach (var extent in orderedList)
                {
                    string extentName = extent.RepoName;
                    string sobrName = extent.SobrName;
                    string hostName = extent.Host;
                    string path = extent.Path;

                    if (scrub)
                    {
                        extentName = CGlobals.Scrubber.ScrubItem(extentName, ScrubItemType.Repository);
                        sobrName = CGlobals.Scrubber.ScrubItem(sobrName, ScrubItemType.SOBR);
                        hostName = CGlobals.Scrubber.ScrubItem(hostName, ScrubItemType.Server);
                        path = CGlobals.Scrubber.ScrubItem(path, ScrubItemType.Path);
                    }

                    var freePercent = this.FreePercent(extent.FreeSPace, extent.TotalSpace);

                    var perfExtent = new CPerformanceTierExtent
                    {
                        Name = extentName,
                        SobrName = sobrName,
                        MaxTasks = extent.MaxTasks,
                        Cores = extent.Cores,
                        Ram = extent.Ram,
                        IsAutoGate = extent.IsAutoGateway,
                        Host = hostName,
                        Path = path,
                        FreeSpace = Math.Round((decimal)extent.FreeSPace / 1024, 2),
                        TotalSpace = Math.Round((decimal)extent.TotalSpace / 1024, 2),
                        FreeSpacePercent = freePercent,
                        IsDecompress = extent.IsDecompress,
                        AlignBlocks = extent.AlignBlocks,
                        IsRotatedDrives = extent.IsRotatedDriveRepository,
                        IsImmutabilitySupported = extent.IsImmutabilitySupported || extent.ObjectLockEnabled,
                        Type = string.IsNullOrEmpty(extent.TypeDisplay) ? extent.Type : extent.TypeDisplay,
                        Provisioning = extent.Povisioning,
                        Status = extent.Status,
                        IsObjectLockEnabled = extent.ObjectLockEnabled,
                        TierType = "Performance"
                    };

                    perfExtents.Add(perfExtent);
                }
            }
            catch (Exception e)
            {
                this.log.Error(this.logStart + "Failed to extract performance tier extents: " + e.Message);
            }

            this.log.Info(this.logStart + "Converting performance tier extent info to xml..done!");
            return perfExtents;
        }

        #endregion

    }
}
