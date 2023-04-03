// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VBR_Tables.Concurrency_Tables;
using VeeamHealthCheck.Functions.Reporting.RegSettings;
using VeeamHealthCheck.Reporting.Html.VBR.Managed_Server_Table;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
//using VeeamHealthCheck.Common;
using VeeamHealthCheck.Shared.Logging;
//using static VeeamHealthCheck.Functions.Collection.DB.CModel;
using static VeeamHealthCheck.Functions.Collection.DB.CModel;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    class CDataFormer
    {
        private readonly string _testFile = "xml\\vbr.xml";
        private string logStart = "[DataFormer]\t";

        //private string _backupServerName;
        private bool _isBackupServerProxy;
        private bool _isBackupServerRepo;
        private bool _isBackupServerWan;
        //private CQueries _cq = new();
        private Dictionary<string, int> _repoJobCount;
        private CScrubHandler _scrubber = CGlobals.Scrubber;
        //private bool _scrub;

        //Security Summary parts
        //private bool _backupsEncrypted = false;
        //private bool _immuteFound = false;
        //private bool _trafficEncrypted = false;
        //private bool _configBackupEncrypted = false;

        //private bool _isSqlLocal;
        //private int _cores;
        //private int _ram;
        private CDataTypesParser _dTypeParser;
        private List<CServerTypeInfos> _csv;
        private readonly CCsvParser _csvParser = new();
        private readonly CLogger log = CGlobals.Logger;
        private CHtmlExporter exporter;



        public CDataFormer() // add string mode input
        {
            _dTypeParser = new();
            _csv = _dTypeParser.ServerInfo();

            //CheckXmlFile();
        }
        private void CheckXmlFile()
        {
            if (!File.Exists(_testFile))
                File.Create(_testFile).Dispose();
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

            notProtectedTypes.Add("Kubernetes");
            notProtectedTypes.Add("Microsoft 365");

            return notProtectedTypes;

        }
        public List<int> SecSummary()
        {
            List<int> secSummary = new List<int>();
            var csv = new CCsvParser();
            try
            {
                var sobrRepo = csv.GetDynamicCapTier().ToList();
                var extRepo = csv.GetDynamicSobrExt().ToList();
                var onPremRepo = csv.GetDynamicRepo().ToList();
                if (onPremRepo.Any(x => x.isimmutabilitysupported == "True"))
                {
                    secSummary.Add(1);
                    //return secSummary;
                }

                else if (sobrRepo.Any(x => x.immute == "True"))
                {
                    secSummary.Add(1);

                    //return secSummary;
                }


                else if (extRepo.Any(x => x.isimmutabilitysupported == "True"))
                {
                    secSummary.Add(1);
                    //return secSummary;
                }
                else
                {
                    secSummary.Add(0);
                }

            }
            catch (Exception)
            {
                log.Error("Unable to find immutability. Marking false");
                secSummary.Add(0);
            }
            try
            {
                var netTraffic = csv.GetDynamincNetRules();
                if (netTraffic.Any(x => x.encryptionenabled == "True"))
                    secSummary.Add(1);
                else secSummary.Add(0);
            }
            catch (Exception)
            {
                log.Info("Traffic encryption not detected. Marking false");
                secSummary.Add(0);
            }
            try
            {
                var backupEnc = csv.GetDynamicJobInfo();
                if (backupEnc.Any(x => x.pwdkeyid != "00000000-0000-0000-0000-000000000000" && !string.IsNullOrEmpty(x.pwdkeyid)))
                    secSummary.Add(1);
                else secSummary.Add(0);
            }
            catch (Exception)
            {
                log.Error("Unable to detect backup encryption. Marking false");
                secSummary.Add(0);
            }

            try
            {
                var cBackup = csv.GetDynamincConfigBackup();
                if (cBackup.Any(x => x.encryptionoptions == "True"))
                    secSummary.Add(1);
                else secSummary.Add(0);
            }
            catch (Exception)
            {
                log.Error("Config backup not detected. Marking false");
                //log.Info(e.Message);
                secSummary.Add(0);
            }

            return secSummary;


        }

        public Dictionary<string, int> ServerSummaryToXml()
        {
            log.Info("converting server summary to xml");
            Dictionary<string, int> di = _dTypeParser.ServerSummaryInfo;

            log.Info("converting server summary to xml.done!");
            return di;
        }
        public void ProtectedWorkloadsToXml()
        {
            //customize the log line:
            log.Info("Converting protected workloads data to xml...");


            // gather data needed for input
            CCsvParser csvp = new();

            #region viProtected
            var protectedVms = csvp.ViProtectedReader().ToList();
            var unProtectedVms = csvp.ViUnProtectedReader().ToList();

            var HvProtectedVms = csvp.HvProtectedReader().ToList();
            var HvUnProtectedVms = csvp.HvUnProtectedReader().ToList();
            #endregion

            #region hv logic
            List<string> hvNames = new();
            List<string> hvProtectedNames = new();
            List<string> hvNotProtectedNames = new();
            int hvDupes = 0;


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


            log.Info("Converting protected workloads data to xml..done!");
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
            log.Info("xml node template start...");


            // gather data needed for input
            //CServerTypeInfos backupServer = _csv.Find(x => (x.Id == _backupServerId));
            CCsvParser config = new();



            // Check for items needing scrubbed
            if (scrub)
            {
                // set items to scrub
            }

            log.Info("xml template..done!");
        }
        public BackupServer BackupServerInfoToXml(bool scrub)
        {

            log.Info("converting backup server info to xml");
            List<string> list = new List<string>();
            CBackupServerTableHelper bt = new(scrub);
            BackupServer b = bt.SetBackupServerData();



            CheckServerRoles(CGlobals._backupServerId);

            b.HasProxyRole = _isBackupServerProxy;
            b.HasRepoRole = _isBackupServerRepo;
            b.HasWanAccRole = _isBackupServerWan;



            log.Info("converting backup server info to xml..done!");
            return b;
        }
        private string ParseString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            else return input;
        }
        public List<string[]> SobrInfoToXml(bool scrub)
        {
            PreCalculations();
            log.Info("Starting SOBR conversion to xml..");
            List<string[]> list = new();

            List<CSobrTypeInfos> csv = _dTypeParser.SobrInfo;
            List<CRepoTypeInfos> repos = _dTypeParser.ExtentInfo;
            csv = csv.OrderBy(x => x.Name).ToList();


            foreach (var c in csv)
            {
                string[] s = new string[30];
                int repoCount = repos.Count(x => x.SobrName == c.Name);

                string newName = c.Name;
                if (scrub)
                    newName = _scrubber.ScrubItem(c.Name, "sobr");
                _repoJobCount.TryGetValue(c.Name, out int jobCount);

                s[0] += newName;
                s[1] += repoCount;
                s[2] += jobCount;
                s[3] += c.PolicyType;
                s[4] += c.EnableCapacityTier;
                s[5] += c.CapacityTierCopyPolicyEnabled;
                s[6] += c.CapacityTierMovePolicyEnabled;
                s[7] += c.ArchiveTierEnabled;
                s[8] += c.UsePerVMBackupFiles;
                s[9] += c.CapTierType;
                s[10] += c.ImmuteEnabled;
                s[11] += c.ImmutePeriod;
                s[12] += c.SizeLimitEnabled;
                s[13] += c.SizeLimit;
                //s[14] += c.
                //s[15] += c.ArchiveExtent;
                //s[16] += c.CostOptimizedArchiveEnabled;
                //s[17] += c.ArchiveFullBackupModeEnabled;
                //s[18] += c.PluginBackupsOffloadEnabled;
                //s[19] += c.CopyAllMachineBackupsEnabled;
                //s[20] += c.CopyAllPluginBackupsEnabled;
                //s[21] += c.Id;
                //s[22] += c.Description;
                //s[23] += c.OverridePolicyEnabled;
                //s[24] += c.CapTierType;
                //s[25] += c.CapTierName;
                //s[26] += c.ImmuteEnabled;
                //s[27] += c.ImmutePeriod;
                //s[28] += c.SizeLimit;
                //s[29] += c.SizeLimitEnabled;
                //s[30] += 

                list.Add(s);
            }
            log.Info("Starting SOBR conversion to xml..done!");
            return list;
        }
        public List<string[]> ExtentXmlFromCsv(bool scrub)
        {
            log.Info("converting extent info to xml");
            List<string[]> list = new List<string[]>();
            List<CRepoTypeInfos> csv = _dTypeParser.ExtentInfo;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.SobrName).ToList();


            foreach (var c in csv)
            {
                string[] s = new string[17];
                string newName = c.RepoName;
                string sobrName = c.SobrName;
                string hostName = c.Host;
                string path = c.Path;

                if (scrub)
                {
                    newName = Scrub(newName);
                    sobrName = Scrub(sobrName);
                    hostName = Scrub(hostName);
                    path = Scrub(path);
                }
                string type;
                if (c.TypeDisplay == null)
                    type = c.Type;
                else
                    type = c.TypeDisplay;

                var freePercent = FreePercent(c.FreeSPace, c.TotalSpace);

                s[0] += newName;
                s[1] += sobrName;
                s[2] += c.MaxTasks;
                s[3] += c.Cores;
                s[4] += c.Ram;
                s[5] += c.IsAutoGateway;
                s[6] += hostName;
                s[7] += path;
                s[8] += Math.Round((decimal)c.FreeSPace / 1024, 2);
                s[9] += Math.Round((decimal)c.TotalSpace / 1024, 2);
                s[10] += freePercent;
                s[11] += c.IsDecompress;
                s[12] += c.AlignBlocks;
                s[13] += c.IsRotatedDriveRepository;
                s[14] += c.IsImmutabilitySupported;
                s[15] += c.Type;
                s[16] += c.Povisioning;


                list.Add(s);
            }
            log.Info("converting extent info to xml..done!");
            return list;
        }
        public List<string[]> RepoInfoToXml(bool scrub)
        {
            PreCalculations();
            log.Info("converting repository info to xml");
            List<string[]> list = new();

            List<CRepoTypeInfos> csv = _dTypeParser.RepoInfos;
            csv = csv.OrderBy(x => x.Name).ToList();
            //csv = csv.OrderBy(y => y.sobrName).ToList();

            //XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("repositories");
            //doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            foreach (var c in csv)
            {
                string[] s = new string[18];
                string name = c.Name;
                string host = c.Host;
                string path = c.Path;

                if (scrub)
                {
                    name = _scrubber.ScrubItem(c.Name, "repo");
                    host = _scrubber.ScrubItem(c.Host, "server");
                    path = _scrubber.ScrubItem(c.Path, "repoPath");
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
                s[0] += name;
                s[1] += c.MaxTasks;
                s[2] += jobCount;
                s[3] += c.Cores;
                s[4] += c.Ram;
                s[5] += c.IsAutoGateway;
                s[6] += host;
                s[7] += path;
                s[8] += Math.Round((decimal)c.FreeSPace / 1024, 2);
                s[9] += Math.Round((decimal)c.TotalSpace / 1024, 2);
                s[10] += freePercent;
                s[11] += c.SplitStoragesPerVm;
                s[12] += c.IsDecompress;
                s[13] += c.AlignBlocks;
                s[14] += c.IsRotatedDriveRepository;
                s[15] += c.IsImmutabilitySupported;
                s[16] += c.Type;
                s[17] += c.Povisioning;

                list.Add(s);
            }
            log.Info("converting repository info to xml..done!");
            return list;
        }
        public List<string[]> ProxyXmlFromCsv(bool scrub)
        {
            log.Info("converting proxy info to xml");
            List<string[]> list = new();

            List<CProxyTypeInfos> csv = _dTypeParser.ProxyInfo();

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(y => y.Type).ToList();


            foreach (var c in csv)
            {
                string[] s = new string[13];
                if (scrub)
                {
                    c.Name = Scrub(c.Name);
                    c.Host = Scrub(c.Host);
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
            log.Info("converting proxy info to xml..done!");

            return list;
        }

        public List<CManagedServer> ServerXmlFromCsv(bool scrub)    // managed servers protect vm count
        {
            log.Info("converting server info to xml");
            List<CManagedServer> list = new();
            List<CServerTypeInfos> csv = _csv;

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(x => x.Type).ToList();

            CCsvParser csvp = new();
            var protectedVms = csvp.ViProtectedReader().ToList();
            var unProtectedVms = csvp.ViUnProtectedReader().ToList();

            //XDocument doc = XDocument.Load(_testFile);
            //XElement serverRoot = new XElement("servers");
            //doc.Root.Add(serverRoot);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            //list to ensure we only count unique VMs
            List<string> countedVMs = new();

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

                //string pVmStr = "";
                //if (protectedCount != 0)
                //    pVmStr = protectedCount.ToString();

                //string upVmStr = "";
                //if (unProtectedCount != 0)
                //    upVmStr = unProtectedCount.ToString();
                //string tVmStr = "";
                //if (vmCount != 0)
                //    tVmStr = vmCount.ToString();



                //check for VBR Roles
                CheckServerRoles(c.Id);
                //string repoRole = "";
                //string proxyRole = "";
                //string wanRole = "";
                //if (_isBackupServerProxy)
                //    proxyRole = "True";
                //if (_isBackupServerRepo)
                //    repoRole = "True";
                //if (_isBackupServerWan)
                //    wanRole = "True";

                //scrub name if selected
                string newName = c.Name;
                if (scrub)
                    newName = Scrub(newName);
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

                //s[1] += c.Cores;
                //s[2] += c.Ram;
                //s[3] += c.Type;
                //s[4] += c.ApiVersion;
                //s[5] += pVmStr;// proxyRole;
                //s[6] += upVmStr;// repoRole;
                //s[7] += tVmStr;// wanRole;
                //s[8] += proxyRole;// c.IsUnavailable;
                //s[9] += repoRole;// pVmStr;
                //s[10] += wanRole;// upVmStr;
                //s[11] += c.IsUnavailable;// tVmStr;
                //s[12] += ParseString(c.OSInfo);


                //doc.Add(xml);

                list.Add(server);
            }
            log.Info("converting server info to xml..done!");
            return list;
        }
        public Dictionary<string, int> JobSummaryInfoToXml()
        {
            log.Info("converting job summary info to xml");
            List<CJobTypeInfos> csv = _dTypeParser.JobInfos;

            //CQueries cq = _cq;

            List<CModel.EDbJobType> types = new();

            List<string> types2 = new();
            foreach (var c in csv)
            {
                types2.Add(c.JobType);
            }


            Dictionary<string, int> typeSummary = new();
            foreach (var t in types2)
            {
                int typeCount = 0;
                foreach (var t2 in types2)
                {
                    if (t == t2)
                    {
                        typeCount++;
                    }
                }
                if (!typeSummary.ContainsKey(t))
                    typeSummary.Add(t, typeCount);
            }

            //sum of all jobs:
            int totalJobs = 0;
            foreach (var c in typeSummary)
            {
                totalJobs += c.Value;
            }

            //ParseNonProtectedTypes(notProtectedTypes);

            //XDocument doc = XDocument.Load(_testFile);

            //XElement extElement = new XElement("jobSummary");
            //doc.Root.Add(extElement);
            //foreach (var d in typeSummary)
            //{
            //    var xml = new XElement("summary",
            //        new XElement("type", d.Key),
            //        new XElement("typeCount", d.Value));

            //    extElement.Add(xml);

            //}
            //var totalElement = new XElement("summary",
            //    new XElement("type", "TotalJobs"),
            //    new XElement("typeCount", totalJobs));
            //extElement.Add(totalElement);


            log.Info("converting job summary info to xml..done!");
            return typeSummary;
        }


        public Dictionary<int, string[]> JobConcurrency(bool isJob, int days)
        {
            log.Info("calculating concurrency");
            CConcurrencyHelper helper = new();


            List<CJobSessionInfo> trimmedSessionInfo = new();
            using (CDataTypesParser dt = new())
            {
                trimmedSessionInfo = dt.JobSessions.Where(c => c.CreationTime >= CGlobals.TOOLSTART.AddDays(-7)).ToList();
            }
            List<CJobTypeInfos> jobInfo = _dTypeParser.JobInfos;

            List<ConcurentTracker> ctList = new();
            List<string> jobNameList = new();
            jobNameList.AddRange(trimmedSessionInfo.Select(y => y.JobName).Distinct());

            List<string> mirrorJobNamesList = new();
            List<string> nameDatesList = new();
            if (isJob)
            {
                ctList = helper.JobCounter(trimmedSessionInfo);
            }


            else if (!isJob)
            {
                ctList = helper.TaskCounter(trimmedSessionInfo);

            }





            log.Info("calculating concurrency...done!");

            return helper.FinalConcurrency((ctList));
        }

        public Dictionary<string, string> RegOptions()
        {
            Dictionary<string, string> returnDict = new();

            var reg = new CCsvParser();
            var RegOptions = reg.RegOptionsCsvParser();
            CDefaultRegOptions defaults = new();

            //XDocument doc = XDocument.Load(_testFile);
            //XElement extElement = new XElement("regOptions");
            //doc.Root.Add(extElement);

            foreach (var r in RegOptions)
            {
                if (defaults._defaultKeys.ContainsKey(r.KeyName))
                {
                    string[] skipKeys = new string[] { "SqlSecuredPassword", "SqlLogin", "SqlServerName", "SqlInstanceName", "SqlDatabaseName", "SqlLockInfo" };
                    if (skipKeys.Contains(r.KeyName))
                        continue;
                    defaults._defaultKeys.TryGetValue(r.KeyName, out string setValue);
                    if (setValue != r.Value)
                    {
                        returnDict.Add(r.KeyName, r.Value);
                    }
                }
                if (!defaults._defaultKeys.ContainsKey(r.KeyName))
                {
                    defaults._defaultKeys.TryGetValue(r.KeyName, out string setValue);
                    returnDict.Add(r.KeyName, r.Value);
                }
            }
            return returnDict;
        }
        public List<List<string>> JobInfoToXml(bool scrub)
        {
            List<List<string>> sendBack = new();
            log.Info("converting job info to xml");
            List<CJobTypeInfos> csv = _dTypeParser.JobInfos;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.JobType).ToList();
            csv = csv.OrderBy(x => x.Name).ToList();

            var cp = new CCsvParser();
            var csv2 = cp.ServerCsvParser().ToList();
            //XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("jobs");
            //doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
            decimal totalsize = 0;

            foreach (var c in csv)
            {
                List<string> job = new();
                string jname = c.Name;
                string repo = c.RepoName;
                //if (c.EncryptionEnabled == "True")
                //_backupsEncrypted = true;
                if (scrub)
                {
                    jname = _scrubber.ScrubItem(c.Name, "job");
                    repo = _scrubber.ScrubItem(c.RepoName, "repo");
                }
                decimal.TryParse(c.ActualSize, out decimal actualSize);

                job.Add(jname);
                job.Add(repo);
                job.Add(Math.Round(actualSize / 1024 / 1024 / 1024, 2).ToString());
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
            "",
            "",
            totalsize.ToString(),
            "",
            "",
            "",

            };


            //doc.Save(_testFile);
            log.Info("converting job info to xml..done!");
            return sendBack;
        }
        public List<List<string>> ConvertJobSessSummaryToXml(bool scrub)
        {
            CJobSessSummary jss = new(log, scrub, _scrubber, _dTypeParser);
            return jss.JobSessionSummaryToXml(scrub);

        }
        private List<CJobSessionInfo> ReturnJobSessionsList()
        {
            var targetDate = CGlobals.TOOLSTART.AddDays(-CGlobals.ReportDays);

            using (CDataTypesParser dt = new())
            {
                List<CJobSessionInfo> csv = dt.JobSessions.Where(c => c.CreationTime >= targetDate).ToList();
                csv = csv.OrderBy(x => x.Name).ToList();

                csv = csv.OrderBy(y => y.CreationTime).ToList();
                csv.Reverse();
                return csv;
            }
        }
        private void LogJobSessionParseProgress(double counter, int total)
        {
            double percentComplete = counter / total * 100;
            string msg = string.Format(logStart + "{0}%...", Math.Round(percentComplete, 2));
            log.Info(msg, false);
        }
        private void SetOutDir(bool scrub)
        {

        }
        public void JobSessionInfoToXml(bool scrub)
        {
            string logStart = "[JobSessions]\t";
            log.Info(logStart + "converting job session info to xml");
            CHtmlFormatting _form = new();

            List<CJobSessionInfo> csv = ReturnJobSessionsList();

            List<string> processedJobs = new();
            double percentCounter = 0;
            foreach (var cs in csv)
            {
                LogJobSessionParseProgress(percentCounter, csv.Count);

                if (!processedJobs.Contains(cs.JobName))
                {
                    processedJobs.Add(cs.JobName);

                    string outDir = "";// CVariables.desiredDir + "\\Original";
                    string folderName = "\\JobSessionReports";


                    if (scrub)
                    {
                        outDir = CGlobals._desiredPath + CVariables._safeSuffix + folderName;
                        //log.Warning("SAFE outdir = " + outDir, false);
                        CheckFolderExists(outDir);
                        outDir += "\\" + _scrubber.ScrubItem(cs.JobName) + ".html";
                    }
                    else
                    {
                        outDir = CGlobals._desiredPath + CVariables._unsafeSuffix + folderName;
                        CheckFolderExists(outDir);
                        outDir += "\\" + cs.JobName + ".html";
                    }
                    string docName = outDir + "\\";


                    string s = _form.FormHeader();
                    s += "<h2>" + cs.JobName + "</h2>";

                    s += "<table border=\"1\"><tr>";
                    s += _form.TableHeader("Job Name", "Name of job");
                    s += _form.TableHeader("VM Name", "Name of VM/Server within the job");
                    s += _form.TableHeader("Alg", "Job Algorithm");
                    s += _form.TableHeader("Primary Bottleneck", "Primary detected bottleneck");
                    s += _form.TableHeader("BottleNeck", "Detected bottleneck breakdown");
                    s += _form.TableHeader("CompressionRatio", "Calculated compression ratio");
                    s += _form.TableHeader("Start Time", "Start time of the backup job");
                    s += _form.TableHeader("BackupSize", "Detected size of backup file");
                    s += _form.TableHeader("DataSize", "Detected size of original VM/server (provisioned, not actual)");
                    s += _form.TableHeader("DedupRatio", "Calculated deduplication ratio");
                    s += _form.TableHeader("Is Retry", "Is this a retry run?");
                    s += _form.TableHeader("Job Duration", "Duration of job in minutes");
                    s += _form.TableHeader("Min Time", "Shorted detected job duration in minutes");
                    s += _form.TableHeader("Max Time", "Longest detected job duration in minutes");
                    s += _form.TableHeader("Avg Time", "Average job duration in minutes");
                    s += _form.TableHeader("Processing Mode", "Processing mode used in the job (blank = SAN)");
                    s += _form.TableHeader("Status", "Final status of the job");
                    s += _form.TableHeader("Task Duration", "Duration of the VM/server within the job in minutes");
                    s += "</tr>";

                    int counter = 1;
                    foreach (var c in csv)
                    {
                        string info = string.Format("Parsing {0} of {1} Job Sessions to HTML", counter, csv.Count);
                        counter++;
                        //log.Info(logStart + info, false);
                        try
                        {
                            if (cs.JobName == c.JobName)
                            {
                                string jname = c.JobName;
                                if (jname.Contains("\\"))
                                {
                                    jname = jname.Replace("\\", "--");
                                }
                                string vmName = c.VmName;
                                //string repo = _scrubber.ScrubItem(c.)
                                if (scrub)
                                {
                                    jname = _scrubber.ScrubItem(c.JobName, "job");
                                    vmName = _scrubber.ScrubItem(c.VmName, "vm");
                                }

                                s += "<tr>";
                                s += TableData(jname, "jobName");
                                s += TableData(vmName, "vmName");
                                s += TableData(c.Alg, "alg");
                                s += TableData(c.PrimaryBottleneck, "primBottleneck");
                                s += TableData(c.Bottleneck, "bottleneck");
                                s += TableData(c.CompressionRatio, "compression");
                                s += TableData(c.CreationTime.ToString(), "creationtime");
                                s += TableData(c.BackupSize.ToString(), "backupsize");
                                s += TableData(c.DataSize.ToString(), "datasize");
                                s += TableData(c.DedupRatio, "dedupratio");
                                s += TableData(c.IsRetry, "isretry");
                                s += TableData(c.JobDuration, "jobDuration");
                                s += TableData(c.minTime.ToString(), "minTime");
                                s += TableData(c.maxTime.ToString(), "maxtime");
                                s += TableData(c.avgTime.ToString(), "avgTime");
                                s += TableData(c.ProcessingMode, "processingmode");
                                s += TableData(c.Status, "status");
                                s += TableData(c.TaskDuration, "taskDuration");
                                s += "</tr>";

                            }
                        }
                        catch (Exception e) { log.Error(e.Message); }


                        //write HTML

                    }
                    try
                    {
                        //string dir = Path.GetDirectoryName(docName);
                        //if (!Directory.Exists(dir))
                        //    Directory.CreateDirectory(dir);
                        File.WriteAllText(outDir, s);
                    }
                    catch (Exception e)
                    {
                        log.Error(e.Message);
                    }

                }



                percentCounter++;
            }
            //doc.Save(_testFile);
            log.Info("converting job session info to xml..done!");
        }
        private void CheckFolderExists(string folder)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
        public string TableData(string data, string toolTip)
        {
            return string.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
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

        private List<ConcurentTracker> ParseBcjConcurrency(CJobSessionInfo session)
        {
            List<ConcurentTracker> ctL = new();
            int allMinutesOfWeek = 7 * 24 * 60;
            TimeSpan sevenDayMinutes = TimeSpan.FromSeconds(allMinutesOfWeek);


            foreach (DayOfWeek d in Enum.GetValues(typeof(DayOfWeek)))
            {
                ConcurentTracker ct = new();
                ct.Date = session.CreationTime.Date;
                ct.DayofTheWeeek = d;
                ct.Hour = 0;
                ct.hourMinute = 0;
                ct.Minutes = 0;
                ct.Duration = sevenDayMinutes;

                ctL.Add(ct);
            }




            return ctL;
        }



        private void PreCalculations()
        {
            // calc all the things prior to adding XML entries... such as job count per repo....
            List<CJobTypeInfos> jobs = _dTypeParser.JobInfos;
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
            //log.Info("Checking server roles..");
            ResetRoles();

            List<CProxyTypeInfos> proxy = _dTypeParser.ProxyInfo();
            List<CRepoTypeInfos> extents = _dTypeParser.ExtentInfo;
            List<CRepoTypeInfos> repos = _dTypeParser.RepoInfos;
            List<CWanTypeInfo> wans = _dTypeParser.WanInfos;

            _isBackupServerProxy = CheckProxyRole(serverId);



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
            //log.Info("Checking server roles..done!");
        }
        private bool CheckProxyRole(string serverId)
        {
            var viProxy = _csvParser.GetDynViProxy();
            var hvProxy = _csvParser.GetDynHvProxy();
            var nasProxy = _csvParser.GetDynNasProxy();
            var cdpProxy = _csvParser.GetDynCdpProxy();
            foreach (var v in viProxy.ToList())
            {
                if (v.hostid == serverId)
                    return true;
            }
            foreach (var h in hvProxy)
            {
                if (h.id == serverId)
                    return true;
            }
            foreach (var n in nasProxy)
            {
                if (n.hostid == serverId)
                    return true;
            }

            foreach (var c in cdpProxy)
            {
                if (c.serverid == serverId)
                    return true;
            }
            return false;
        }
        private string Scrub(string item)
        {
            return _scrubber.ScrubItem(item);
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
