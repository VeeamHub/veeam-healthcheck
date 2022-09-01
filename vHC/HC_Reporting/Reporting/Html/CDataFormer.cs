// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.DataTypes;
using VeeamHealthCheck.DB;
using VeeamHealthCheck.Html;
using VeeamHealthCheck.RegSettings;
using VeeamHealthCheck.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared.Logging;
using static VeeamHealthCheck.DB.CModel;


namespace VeeamHealthCheck.Reporting.Html
{
    class CDataFormer
    {
        private readonly string _testFile = "xml\\vbr.xml";
        
        private readonly string _backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8";
        private string _backupServerName;
        private bool _isBackupServerProxy;
        private bool _isBackupServerRepo;
        private bool _isBackupServerWan;
        //private CQueries _cq = new();
        private Dictionary<string, int> _repoJobCount;
        private CScrubHandler _scrubber = new();
        private bool _scrub;
        private bool _checkLogs;
        private bool _isImport;

        //Security Summary parts
        private bool _backupsEncrypted = false;
        private bool _immuteFound = false;
        private bool _trafficEncrypted = false;
        private bool _configBackupEncrypted = false;

        private bool _isSqlLocal;
        private int _cores;
        private int _ram;
        private CDataTypesParser _dTypeParser;
        private readonly CCsvParser _csvParser = new();
        private readonly CLogger log = VhcGui.log;
        private CHtmlExporter exporter;
        private readonly CXmlFunctions XML = new("vbr");



        public CDataFormer(bool isImport) // add string mode input
        {
            _dTypeParser = new();
            _isImport = isImport;
            CheckXmlFile();
        }
        private void CheckXmlFile()
        {
            if (!File.Exists(_testFile))
                File.Create(_testFile).Dispose();
        }



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
                var sobrRepo = csv.GetDynamicCapTier();
                var extRepo = csv.GetDynamicSobrExt();
                var onPremRepo = csv.GetDynamicRepo();
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
                if (backupEnc.Any(x => (x.pwdkeyid != "00000000-0000-0000-0000-000000000000" && !String.IsNullOrEmpty(x.pwdkeyid))))
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


            log.Info("Converting protected workloads data to xml..done!");
        }
        public int _viDupes;
        public List<string> _vmProtectedByPhys;
        public List<string> _viProtectedNames;
        public List<string> _vmNotProtectedNames;

        public List<string> _viNotProtectedNames;
        public List<string> _physNotProtNames;
        public List<string> _physProtNames;
        private void NewXmlNodeTemplate()
        {
            //customize the log line:
            log.Info("xml node template start...");


            // gather data needed for input
            List<CServerTypeInfos> csv = _dTypeParser.ServerInfo();
            CServerTypeInfos backupServer = csv.Find(x => (x.Id == _backupServerId));
            CCsvParser config = new();

            

            // Check for items needing scrubbed
            if (VhcGui._scrub)
            {
                // set items to scrub
            }

            //set items to XML + save
            //var xml = new XElement("serverInfo",
            //    new XElement("name", backupServer.Name),
            //    new XElement("cores", backupServer.Cores),
            //    new XElement("ram", backupServer.Ram),
            //    new XElement("wanacc", _isBackupServerWan)
            //    );

            //extElement.Add(xml);
            //doc.Save(_testFile);

            log.Info("xml template..done!");
        }
        public List<string> BackupServerInfoToXml()
        {

            log.Info("converting backup server info to xml");
            List<string> list = new List<string>();

            CheckServerRoles(_backupServerId);

            List<CServerTypeInfos> csv = _dTypeParser.ServerInfo();
            CServerTypeInfos backupServer = csv.Find(x => (x.Id == _backupServerId));
            CCsvParser config = new();
            var cv = config.ConfigBackupCsvParser();
            string configBackupEnabled = "";
            string configBackupTarget = "";
            string configBackupEncryption = "";
            string configBackupLastResult = "";

            //if(cv.Count() == 0)
            //{
            //    configBackupEnabled = "False";
            //}
            foreach (var c in cv)
            {
                configBackupEnabled = c.Enabled;
                if (c.Enabled != "False")
                {
                    configBackupTarget = c.Target;
                    configBackupEncryption = c.EncryptionOptions;
                    configBackupLastResult = c.LastResult;
                }
            }

            log.Info("entering registry reader");
            string sqlHostName = "";
            string veeamVersion = "";
            string sqlCpu = "";
            string sqlRam = "";
            string edition = "";
            string version = "";
            DataTable si = new DataTable();

            if (!VhcGui._import)
            {
                try
                {
                    CRegReader regReader = new();
                    regReader.GetDbInfo();
                    sqlHostName = regReader.Host;
                }
                catch (Exception q)
                {
                    log.Error(q.Message);
                }
                log.Info("entering registry reader..done!");

                _isSqlLocal = true;
                try
                {
                    log.Info("starting sql queries");
                    CQueries cq = new();
                    si = cq.SqlServerInfo;
                    edition = cq.SqlEdition;
                    version = cq.SqlVerion;
                    log.Info("starting sql queries..done!");
                }
                catch (Exception e)
                {
                    log.Error(e.Message);

                }
            }

            if (veeamVersion == "" || sqlHostName == "")
            {
                try
                {
                    CCsvParser cp = new();
                    var records = cp.BnrCsvParser();
                    foreach (var r in records)
                    {
                        veeamVersion = r.Version;
                        sqlHostName = r.SqlServer;
                        if (VhcGui._scrub)
                            sqlHostName = Scrub(sqlHostName);
                    }
                }
                catch (Exception f)
                {
                    log.Error(f.Message);
                }
            }
            try
            {
                _backupServerName = backupServer.Name;
                if (!backupServer.Name.Contains(sqlHostName, StringComparison.OrdinalIgnoreCase)
                    && sqlHostName != "localhost" && sqlHostName != "LOCALHOST")
                {
                    _isSqlLocal = false;
                    foreach (DataRow row in si.Rows)
                    {
                        string cpu = row["cpu_count"].ToString();
                        string hyperthread = row["hyperthread_ratio"].ToString();
                        string memory = row["physical_memory_kb"].ToString();
                        int.TryParse(cpu, out int c);
                        int.TryParse(hyperthread, out int h);
                        int.TryParse(memory, out int mem);

                        sqlCpu = cpu;//(c * h).ToString();
                        sqlRam = ((mem / 1024 / 1024) + 1).ToString();

                    }
                    if (VhcGui._scrub)
                        sqlHostName = Scrub(sqlHostName);
                }
                else if (backupServer.Name == sqlHostName)
                    sqlHostName = "";
                _cores = backupServer.Cores;
                _ram = backupServer.Ram;
            }
            catch (Exception g)
            {
                log.Error("Error processing SQL resource data");
                log.Error(g.Message);
            }


            if (VhcGui._scrub)
            {
                backupServer.Name = Scrub(backupServer.Name);
                configBackupTarget = Scrub(configBackupTarget);
            }
            string proxyRole = "";
            
            if (_isBackupServerProxy)
                proxyRole = "True";
            if (!_isBackupServerProxy)
                proxyRole = "False";

            var xml = new XElement("serverInfo",
                new XElement("name", backupServer.Name),
                new XElement("cores", backupServer.Cores),
                new XElement("ram", backupServer.Ram),
                new XElement("configBackupEnabled", configBackupEnabled),
                new XElement("configBackupLastResult", configBackupLastResult),
                new XElement("configBackupEncryption", configBackupEncryption),
                new XElement("configBackupTarget", configBackupTarget),
                new XElement("localSql", _isSqlLocal),
                new XElement("sqlname", sqlHostName),
                new XElement("sqlversion", version),
                new XElement("sqledition", edition),
                new XElement("sqlcpu", sqlCpu),
                new XElement("sqlram", sqlRam),
                new XElement("proxyrole", proxyRole),
                new XElement("repo", _isBackupServerRepo),
                new XElement("veeamVersion", veeamVersion),
                new XElement("wanacc", _isBackupServerWan)
                );

            


            list.Add(backupServer.Name);
            list.Add(veeamVersion);
            list.Add(backupServer.Cores.ToString());
            list.Add(backupServer.Ram.ToString());
            list.Add(configBackupEnabled);
            list.Add(configBackupLastResult);
            list.Add(configBackupEncryption);
            list.Add(configBackupTarget);
            list.Add(_isSqlLocal.ToString());
            list.Add(sqlHostName);
            list.Add(version);
            list.Add(edition);
            list.Add(sqlCpu);
            list.Add(sqlRam);
            list.Add(proxyRole);
            list.Add(_isBackupServerRepo.ToString());
            list.Add(_isBackupServerWan.ToString());





            log.Info("converting backup server info to xml..done!");
            return list;
        }
        public List<string[]> SobrInfoToXml()
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
                if (VhcGui._scrub)
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
        public List<string[]> ExtentXmlFromCsv()
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

                if (VhcGui._scrub)
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
        public List<string[]> RepoInfoToXml()
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

                if (VhcGui._scrub)
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
        public List<string[]> ProxyXmlFromCsv()
        {
            log.Info("converting proxy info to xml");
            List<string[]> list = new();
            List<CProxyTypeInfos> csv = _dTypeParser.ProxyInfo();

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(y => y.Type).ToList();


            foreach (var c in csv)
            {
                string[] s = new string[12];
                if (VhcGui._scrub)
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

                list.Add(s);
            }
            log.Info("converting proxy info to xml..done!");

            return list;
        }

        public List<string[]> ServerXmlFromCsv()
        {
            log.Info("converting server info to xml");
            List<string[]> list = new List<string[]>();
            List<CServerTypeInfos> csv = _dTypeParser.ServerInfo();

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
                string[] s = new string[12];

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

                string pVmStr = "";
                if (protectedCount != 0)
                    pVmStr = protectedCount.ToString();

                string upVmStr = "";
                if (unProtectedCount != 0)
                    upVmStr = unProtectedCount.ToString();
                string tVmStr = "";
                if (vmCount != 0)
                    tVmStr = vmCount.ToString();



                //check for VBR Roles
                CheckServerRoles(c.Id);
                string repoRole = "";
                string proxyRole = "";
                string wanRole = "";
                if (_isBackupServerProxy)
                    proxyRole = "True";
                if (_isBackupServerRepo)
                    repoRole = "True";
                if (_isBackupServerWan)
                    wanRole = "True";

                //scrub name if selected
                string newName = c.Name;
                if (VhcGui._scrub)
                    newName = Scrub(newName);
                s[0] = newName;
                s[1] += c.Cores;
                s[2] += c.Ram;
                s[3] += c.Type;
                s[4] += c.ApiVersion;
                s[5] += pVmStr;// proxyRole;
                s[6] += upVmStr;// repoRole;
                s[7] += tVmStr;// wanRole;
                s[8] += proxyRole;// c.IsUnavailable;
                s[9] += repoRole;// pVmStr;
                s[10] += wanRole;// upVmStr;
                s[11] += c.IsUnavailable;// tVmStr;


                //doc.Add(xml);

                list.Add(s);
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
            Dictionary<int, string[]> sendBack = new();

            string htmlString = String.Empty;
            List<CJobSessionInfo> sessionInfo = _dTypeParser.JobSessions;
            List<CJobTypeInfos> jobInfo = _dTypeParser.JobInfos;

            List<ConcurentTracker> ctList = new();
            Dictionary<DateTime, string> jobStartDict = new();
            List<string> jobNameList = new();
            jobNameList.AddRange(sessionInfo.Select(y => y.JobName).Distinct());

            List<string> mirrorJobNamesList = new();
            List<string> nameDatesList = new();
            if (isJob)
            {
                List<string> mirrorJobBjobList = new();
                List<string> backupSyncNameList = new();
                List<string> epAgentBackupList = new();
                foreach (var backup in jobInfo)
                {
                    if (backup.JobType == "SimpleBackupCopyPolicy")
                        mirrorJobBjobList.Add(backup.Name);
                    if (backup.JobType == "BackupSync")
                        backupSyncNameList.Add(backup.Name);
                    if (backup.JobType == "EpAgentBackup")
                        epAgentBackupList.Add(backup.Name);
                }


                foreach (var m in mirrorJobBjobList)
                {
                    var mirrorSessions = sessionInfo.Where(y => y.JobName.StartsWith(m));

                    foreach (var sess in mirrorSessions)
                    {
                        int i = mirrorSessions.Count();
                        DateTime now = DateTime.Now;
                        double diff = (now - sess.CreationTime).TotalDays;
                        if (diff < VhcGui._reportDays)
                        {
                            mirrorJobNamesList.Add(sess.JobName);
                            string nameDate = sess.JobName + sess.CreationTime.ToString();
                            if (!nameDatesList.Contains(nameDate))
                            {
                                nameDatesList.Add(nameDate);
                                ctList.Add(ParseConcurrency(sess, days));
                            }
                        }

                    }
                }

                var backupSyncJobs = jobInfo.Where(x => x.JobType == "BackupSync");
                foreach (var b in backupSyncJobs)
                {
                    var v = sessionInfo.Where(x => x.JobName == b.Name);

                    foreach (var s in v)
                    {
                        try
                        {
                            string[] n = s.JobName.Split("\\");
                            string bcjName = b.Name;
                            if (!nameDatesList.Contains(bcjName))
                            {
                                nameDatesList.Add(bcjName);

                                ctList.AddRange(ParseBcjConcurrency(s));
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            log.Error("Failed to parse BackupSync job. Error:");
                            log.Error(e.Message);
                        }
                    }
                }

                var epAgentBackupJobs = jobInfo.Where(x => x.JobType == "EpAgentBackup");
                foreach (var e in epAgentBackupJobs)
                {
                    var epBcj = sessionInfo.Where(x => x.JobType == "EEndPoint");

                    foreach (var epB in epBcj)
                    {
                        if (!mirrorJobNamesList.Contains(epB.JobName))
                        {
                            string[] n = epB.JobName.Split(" - ");
                            string n1 = n[0] + epB.CreationTime.ToString();
                            if (!nameDatesList.Contains(n1))
                            {
                                nameDatesList.Add(n1);
                                ctList.Add(ParseConcurrency(epB, VhcGui._reportDays));
                            }
                        }

                    }
                }
                foreach (var b in jobInfo)
                {

                    var remainingSessions = sessionInfo.Where(x => x.JobName.Equals(b.Name));
                    foreach (var sess in remainingSessions)
                    {
                        string nameDate = sess.JobName + sess.CreationTime.ToString();
                        if (!nameDatesList.Contains(nameDate))
                        {
                            nameDatesList.Add(nameDate);
                            ctList.Add(ParseConcurrency(sess, days));
                        }
                    }

                }
            }


            else if (!isJob)
            {
                foreach (var session in sessionInfo)
                {
                    DateTime now = DateTime.Now;
                    double diff = (now - session.CreationTime).TotalDays;
                    if (diff < VhcGui._reportDays)
                    {
                        ctList.Add(ParseConcurrency(session, days));

                    }

                }

            }


            Dictionary<DayOfWeek, Dictionary<int, int>> dict1 = new();
            foreach (var c in ctList)
            {

                if (!dict1.ContainsKey(c.DayofTheWeeek))
                {
                    Dictionary<int, int> minuteMapper = new();
                    foreach (var c2 in ctList)
                    {

                        if (c2.Date.DayOfWeek == c.Date.DayOfWeek)
                        {
                            var ticks = c2.Duration.TotalMinutes;
                            int hMinute = c2.hourMinute;

                            for (int i = 0; i < ticks; i++)
                            {
                                int current2;

                                minuteMapper.TryGetValue(hMinute, out current2);
                                minuteMapper[hMinute] = current2 + 1;
                                hMinute++;
                            }
                        }
                    }
                    Dictionary<int, int> hoursAndCount = new();

                    for (int hour = 0; hour < 24; hour++)
                    {
                        int highestCount = 0;
                        foreach (var h in minuteMapper.Keys)
                        {
                            var p = Math.Round((decimal)h / 60, 0, MidpointRounding.ToZero);
                            if (hour == p)
                            {
                                int minutesSubtract = hour * 60;
                                int minutes = h - minutesSubtract;

                                minuteMapper.TryGetValue(h, out int counter);
                                if (counter > highestCount || highestCount == 0)
                                {
                                    highestCount = counter;

                                }
                            }
                        }
                        hoursAndCount.Add(hour, highestCount);
                    }

                    dict1.Add(c.DayofTheWeeek, hoursAndCount);

                }

            }

            List<int> orderedNumList = new();
            for (int i = 0; i < 24; i++)
            {
                orderedNumList.Add(i);
            }

            foreach (var o in orderedNumList.Distinct()) // o is every hour starting with 0
            {
                string[] weekdays = new string[7];
                string[] rows = new string[7];
                foreach (var c in dict1)
                {
                    foreach (var d in c.Value)
                    {
                        if (d.Key == o)
                        {
                            string count;
                            if (d.Value == 0)
                                count = "";
                            else
                                count = d.Value.ToString();
                            //string count = d.Value.ToString();

                            if (c.Key == DayOfWeek.Sunday)
                                rows[0] = count;
                            if (c.Key == DayOfWeek.Monday)
                                rows[1] = count;
                            if (c.Key == DayOfWeek.Tuesday)
                                rows[2] = count;
                            if (c.Key == DayOfWeek.Wednesday)
                                rows[3] = count;
                            if (c.Key == DayOfWeek.Thursday)
                                rows[4] = count;
                            if (c.Key == DayOfWeek.Friday)
                                rows[5] = count;
                            if (c.Key == DayOfWeek.Saturday)
                                rows[6] = count;


                        }
                    }

                }
                sendBack.Add(o, rows);

            }
            log.Info("calculating concurrency...done!");

            return sendBack;
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
        public List<List<string>> JobInfoToXml()
        {
            List<List<string>> sendBack = new();
            log.Info("converting job info to xml");
            List<CJobTypeInfos> csv = _dTypeParser.JobInfos;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.JobType).ToList();
            csv = csv.OrderBy(x => x.Name).ToList();

            //XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("jobs");
            //doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            foreach (var c in csv)
            {
                List<string> job = new();
                string jname = c.Name;
                string repo = c.RepoName;
                if (c.EncryptionEnabled == "True")
                    _backupsEncrypted = true;
                if (VhcGui._scrub)
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
            //doc.Save(_testFile);
            log.Info("converting job info to xml..done!");
            return sendBack;
        }
        public List<List<string>> ConvertJobSessSummaryToXml()
        {
            CJobSessSummary jss = new(_testFile, log, _scrub, _checkLogs, _scrubber, _dTypeParser);
            return jss.JobSessionSummaryToXml();

        }
        public void JobSessionInfoToXml()
        {
            log.Info("converting job session info to xml");
            CHtmlFormatting _form = new();

            List<CJobSessionInfo> csv = _dTypeParser.JobSessions;
            csv = csv.OrderBy(x => x.Name).ToList();

            csv = csv.OrderBy(y => y.CreationTime).ToList();
            csv.Reverse();
            //csv = csv.OrderBy(x => x.CreationTime).ToList();

            //XDocument doc = XDocument.Load(_testFile);

            //doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
            List<string> processedJobs = new();

            foreach (var cs in csv)
            {
                if (!processedJobs.Contains(cs.JobName))
                {
                    processedJobs.Add(cs.JobName);
                    //XDocument newDoc = new XDocument(new XElement("root"));
                    //XElement extElement = new XElement("jobSessions");

                    //newDoc.Root.Add(extElement);
                    string outDir = CVariables.desiredDir + "\\JobSessionReports";
                    if (!Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);

                    string docName = outDir + "\\";
                    if (VhcGui._scrub)
                    {
                        outDir += "\\" + _scrubber.ScrubItem(cs.JobName) + ".html";
                    }
                    else
                    {
                        outDir += "\\" + cs.JobName + ".html";
                    }

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
                    //docName = VerifyDocName(docName);
                    //newDoc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
                    //XElement xml = null;
                    foreach (var c in csv)
                    {
                        try
                        {
                            if (cs.JobName == c.JobName)
                            {
                                string jname = c.JobName;
                                string vmName = c.VmName;
                                //string repo = _scrubber.ScrubItem(c.)
                                if (VhcGui._scrub)
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
                        string dir = Path.GetDirectoryName(docName);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.WriteAllText(outDir, s);
                        // newDoc.Save(docName);
                        //string xmlString = newDoc.ToString();
                        //exporter.ExportHtml(docName);
                    }
                    catch (Exception e)
                    {
                        log.Error(e.Message);
                    }

                }




            }
            //doc.Save(_testFile);
            log.Info("converting job session summary to xml..done!");
        }
        public string TableData(string data, string toolTip)
        {
            return String.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
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
            int allMinutesOfDay = 7 * 24 * 60;
            TimeSpan sevenDayMinutes = TimeSpan.FromSeconds(allMinutesOfDay);


            foreach (DayOfWeek d in Enum.GetValues((typeof(DayOfWeek))))
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
        private ConcurentTracker ParseConcurrency(CJobSessionInfo session, int days)
        {
            ConcurentTracker ct = new();

            DateTime now = DateTime.Now;
            double diff = (now - session.CreationTime).TotalDays;
            //if (session.CreationTime.Day == now.Day)
            //{

            //}
            if (diff < days)
            {
                DayOfWeek dayOfWeek = session.CreationTime.DayOfWeek;
                var startTime = session.CreationTime;

                TimeSpan.TryParse(session.JobDuration, out TimeSpan duration);
                DateTime endTime = startTime.AddMinutes(duration.Minutes);

                var startDay = session.CreationTime.Date;
                int startHour = startTime.Hour;
                int startMinute = startTime.Minute;
                int endHour = endTime.Hour;
                int endMinute = endTime.Minute;

                ct.Date = startTime.Date;
                ct.DayofTheWeeek = dayOfWeek;
                ct.Hour = startHour;
                ct.hourMinute = startHour * 60 + startMinute;
                ct.Minutes = startMinute;
                ct.Duration = duration;

                return ct;
            }
            return ct;
        }

        public void Dispose()
        {

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
                double n = ((double)freespace / (double)totalspace) * 100;
                return Math.Round((decimal)n, 1);
            }
            return 0;

        }
        #endregion

    }
}
