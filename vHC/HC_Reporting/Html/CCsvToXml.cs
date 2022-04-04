// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.DataTypes;
using VeeamHealthCheck;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.DataTypes;
using VeeamHealthCheck.DB;
using VeeamHealthCheck.Html;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.RegSettings;
using VeeamHealthCheck.Scrubber;
using static VeeamHealthCheck.DB.CModel;

namespace VeeamHealthCheck.Html
{
    class CCsvToXml
    {
        private readonly string _testFile = "xml\\xmlout.xml";
        private string _outPath = CVariables.unsafeDir;
        private readonly string _htmlName = "Veeam HealthCheck Report";
        private readonly string _backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8";
        private string _backupServerName;
        private bool _isBackupServerProxy;
        private bool _isBackupServerProxyDisabled;
        private bool _isBackupServerRepo;
        private bool _isBackupServerWan;
        private CQueries _cq;
        private Dictionary<string, int> _repoJobCount;
        private CXmlHandler _scrubber = new();
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

        private string _latestReport;
        private CDataTypesParser _dTypeParser;
        private CLogger log = MainWindow.log;

        private CFillerTexts fillerText = new();

        public void ConvertToXml(bool scrub, bool checkLogs, bool openHtml, bool isImport)
        {
            _isImport = isImport;
            _scrub = scrub;
            _checkLogs = checkLogs;
            if (!isImport)
                _cq = new();
            _dTypeParser = new();
            Work();
            if (openHtml)
                OpenHtml();
        }


        #region XML Conversions
        private void HeaderInfoToXml()
        {
            log.Info("converting header info to xml");
            var parser = new CCsvParser();
            var rec = parser.GetDynamicLicenseCsv();

            string cxName = "";
            foreach (var r in rec)
            {
                cxName = r.licensedto;
            }

            XDocument doc = new XDocument(new XElement("root"));

            XElement serverRoot = new XElement("header");
            doc.Root.Add(serverRoot);
            doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
            string summary = "This report provides data and insight into your Veeam Backup and Replication (VBR) deployment. The information provided here is intended to be used in collaboration with your Veeam representative.";

            var xml = new XElement("h1",
                new XElement("name", cxName),
                new XElement("hc", "Health Check Report"),
                new XElement("summary", summary)
                );

            serverRoot.Add(xml);



            doc.Save(_testFile);
            log.Info("converting header info to xml");
        }
        private void LicInfoToXml()
        {
            log.Info("converting lic info to xml");
            CCsvParser parser = new();
            var recs = parser.GetDynamicLicenseCsv();

            XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("license");
            var summary = new XElement("info", fillerText.LicenseSummary);
            extElement.Add(summary);
            doc.Root.Add(extElement);
            foreach (var c in recs)
            {
                var xml = new XElement("licInfo",
                    new XElement("client", c.licensedto),
                    new XElement("edition", c.edition),
                    new XElement("status", c.status),
                    new XElement("type", c.type),
                    new XElement("licInst", c.licensedinstances),
                    new XElement("usedInst", c.usedinstances),
                    new XElement("newInst", c.newinstances),
                    new XElement("rentInst", c.rentalinstances),
                    new XElement("licSock", c.licensedsockets),
                    new XElement("usedSock", c.usedsockets),
                    new XElement("licCap", c.licensedcapacitytb),
                    new XElement("usedCap", c.usedcapacitytb),
                    new XElement("expire", c.expirationdate),
                    new XElement("supExp", c.supportexpirationdate),
                    new XElement("cloudconnect", c.cloudconnect)
                    );

                extElement.Add(xml);
            }
            doc.Save(_testFile);
            log.Info("converting lic info to xml..done!");
        }
        private void ParseNonProtectedTypes()
        {
            List<string> notProtectedTypes = new();

            var bTypes = _dTypeParser.JobInfos;

            List<string> jtList = new();
            List<int> protectedTypes = _dTypeParser.ProtectedJobIds;
            List<string> unProtectedTypes = new();
            foreach (CModel.EDbJobType jt in Enum.GetValues(typeof(CModel.EDbJobType)))
            {
                foreach (var b in bTypes)
                {
                    int.TryParse(b.JobId, out int id);

                    if (id == 52)
                    {

                    }
                    if ((int)jt == 52)
                    {

                    }
                }
            }
            foreach (EDbJobType jt2 in Enum.GetValues(typeof(EDbJobType)))
            {
                if (!protectedTypes.Contains((int)jt2))
                {
                    notProtectedTypes.Add(jt2.ToString());
                }
            }

            notProtectedTypes.Add("Kubernetes");
            notProtectedTypes.Add("Microsoft 365");

            XDocument doc = XDocument.Load(_testFile);
            XElement extElement = new XElement("npjobSummary");
            //var summary = new XElement("info", fillerText.JobSummary);
            //extElement.Add(summary);
            doc.Root.Add(extElement);

            foreach (var v in notProtectedTypes)
            {
                var xml2 = AddXelement(v, "Type", "");
                extElement.Add(xml2);
            }
            doc.Save(_testFile);
        }
        private void SecSummary()
        {
            try
            {
                var configBackup = _dTypeParser.ConfigBackup;
                if (configBackup.EncryptionOptions == "True")
                    _configBackupEncrypted = true;
            }
            catch (Exception)
            {
                log.Error("Config backup not detected. Marking false");
                //log.Info(e.Message);
                _configBackupEncrypted = false;
            }
            try
            {
                var netTraffic = _dTypeParser.NetTraffRules;
                if (netTraffic.Any(x => x.EncryptionEnabled == "True"))
                    _trafficEncrypted = true;
            }
            catch (Exception)
            {
                log.Info("Traffic encryption not detected. Marking false");
                _trafficEncrypted = false;
            }
            try
            {
                var backupEnc = _dTypeParser.JobInfos;
                if (backupEnc.Any(x => x.EncryptionEnabled == "True"))
                    _backupsEncrypted = true;
            }
            catch (Exception)
            {
                log.Error("Unable to detect backup encryption. Marking false");
                _backupsEncrypted = false;
            }
            try
            {
                var onPremRepo = _dTypeParser.RepoInfos;
                if (onPremRepo.Any(x => x.IsImmutabilitySupported == "True"))
                    _immuteFound = true;
                var sobrRepo = _dTypeParser.SobrInfo;
                if (sobrRepo.Any(x => x.ImmuteEnabled == "True"))
                    _immuteFound = true;
                var extRepo = _dTypeParser.ExtentInfo;
                if (extRepo.Any(x => x.IsImmutabilitySupported == "True"))
                    _immuteFound = true;
            }
            catch (Exception)
            {
                log.Error("Unable to find immutability. Marking false");
                _immuteFound = false;
            }



            XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("secSummary");
            doc.Root.Add(extElement);


            var xml = new XElement("secProfile",
                new XElement("immute", _immuteFound),
                new XElement("trafficEnc", _trafficEncrypted),
                new XElement("configEnc", _configBackupEncrypted),
                new XElement("backupEnc", _backupsEncrypted)
                );

            extElement.Add(xml);
            doc.Save(_testFile);
        }
        private void ServerSummaryToXml()
        {
            log.Info("converting server summary to xml");
            Dictionary<string, int> di = _dTypeParser.ServerSummaryInfo;
            XDocument doc = XDocument.Load(_testFile);



            XElement serverRoot = new XElement("serverSummary");
            doc.Root.Add(serverRoot);


            var summary = new XElement("info", fillerText.serverSummary);
            serverRoot.Add(summary);
            foreach (var c in di)
            {
                var xml = new XElement("server",
                    new XElement("name", c.Key),
                    new XElement("count", c.Value)
                    );

                serverRoot.Add(xml);

                //doc.Add(xml);


            }
            doc.Save(_testFile);
            log.Info("converting server summary to xml.done!");
        }
        private void ProtectedWorkloadsToXml()
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



            // begin XML info input
            XDocument doc = XDocument.Load(_testFile);

            // NEW NODE NAME HERE
            XElement extElement = new XElement("protectedWorkloads");
            doc.Root.Add(extElement);

            // Filter duplicates
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
                if(un.Type == "Vm")
                {
                    viNotProtectedNames.Add(un.Name);
                    vmNames.Add(un.Name);

                }

            }

            viDupes = vmNames.Count - (viProtectedNames.Distinct().Count() + viNotProtectedNames.Distinct().Count());

            List<string> physNames = new();
            List<string> physProtNames = new();
            List<string> physNotProtNames = new();

            foreach (var p in phProt)
            {
                physNames.Add(p.name);
                physProtNames.Add(p.name);
            }
            foreach (var u in phNotProt)
            {
                physNames.Add(p.name);
                physProtNames.Add(p.name);
            }
            foreach (var u in physNotProtected)
            {
                if(u.type == "Computer")
                {
                    physNames.Add(u.name);
                    physNotProtNames.Add(u.name);
                }

            }
            List<string> vmProtectedByPhys = new();
            foreach(var p in physProtected)
            {
                foreach(var v in protectedVms)
                    if(p.name.Contains(v.Name))
                        vmProtectedByPhys.Add(v.Name);
                foreach(var w in unProtectedVms)
                {
                    if (p.name.Contains(w.Name))
                        vmProtectedByPhys.Add(w.Name);
                }
            }

            //var xml2 = AddXelement(protectedVms.Count.ToString(), "ViProtected");
            extElement.Add(AddXelement((viProtectedNames.Distinct().Count() + viNotProtectedNames.Distinct().Count()).ToString(), "Vi Total"));
            extElement.Add(AddXelement(viProtectedNames.Distinct().Count().ToString(), "Vi Protected"));
            extElement.Add(AddXelement(viNotProtectedNames.Distinct().Count().ToString(), "Vi Not Prot."));
            extElement.Add(AddXelement(viDupes.ToString(), "Vi Potential Duplicates"));
            extElement.Add(AddXelement(vmProtectedByPhys.Distinct().Count().ToString(), "VM Protected as Physical"));

            extElement.Add(AddXelement((physNotProtNames.Distinct().Count() + physProtNames.Distinct().Count()).ToString(), "Phys Total"));
            extElement.Add(AddXelement(physProtNames.Distinct().Count().ToString(), "Phys Protected"));
            extElement.Add(AddXelement(physNotProtNames.Distinct().Count().ToString(), "Phys Not Prot."));

            doc.Save(_testFile);

            log.Info("Converting protected workloads data to xml..done!");
        }
        private void NewXmlNodeTemplate()
        {
            //customize the log line:
            log.Info("xml node template start...");


            // gather data needed for input
            List<CServerTypeInfos> csv = _dTypeParser.ServerInfo();
            CServerTypeInfos backupServer = csv.Find(x => (x.Id == _backupServerId));
            CCsvParser config = new();

            // begin XML info input
            XDocument doc = XDocument.Load(_testFile);

            // NEW NODE NAME HERE
            XElement extElement = new XElement("backupServer");
            doc.Root.Add(extElement);

            // Check for items needing scrubbed
            if (_scrub)
            {
                // set items to scrub
            }

            //set items to XML + save
            var xml = new XElement("serverInfo",
                new XElement("name", backupServer.Name),
                new XElement("cores", backupServer.Cores),
                new XElement("ram", backupServer.Ram),
                new XElement("wanacc", _isBackupServerWan)
                );

            extElement.Add(xml);
            doc.Save(_testFile);

            log.Info("xml template..done!");
        }
        private void BackupServerInfoToXml()
        {
            log.Info("converting backup server info to xml");
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

            if (!_isImport)
            {
                try
                {
                    CRegReader regReader = new();
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
                    //CQueries cq = _cq;
                    si = _cq.SqlServerInfo;
                    edition = _cq.SqlEdition;
                    version = _cq.SqlVerion;
                    //veeamVersion = _cq.VbrVersion;
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
                        if (_scrub)
                            sqlHostName = Scrub(sqlHostName);
                    }
                }
                catch (Exception f)
                {

                }
            }
            try
            {
                _backupServerName = backupServer.Name;
                if (!backupServer.Name.Contains(sqlHostName, StringComparison.OrdinalIgnoreCase))
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

                        sqlCpu = (c * h).ToString();
                        sqlRam = ((mem / 1024 / 1024) + 1).ToString();

                    }
                    if (_scrub)
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


            XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("backupServer");
            doc.Root.Add(extElement);

            XElement summary = new XElement("info", fillerText.BackupServerSummary);
            extElement.Add(summary);

            if (_scrub)
            {
                backupServer.Name = Scrub(backupServer.Name);
                configBackupTarget = Scrub(configBackupTarget);
            }
            string proxyRole = "";
            //if (_isBackupServerProxy && _isBackupServerProxyDisabled)
            //   proxyRole = "True: Disabled";
            //if (_isBackupServerProxy && !_isBackupServerProxyDisabled)
            //    proxyRole = "True: Enabled";
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

            extElement.Add(xml);
            doc.Save(_testFile);








            log.Info("converting backup server info to xml..done!");
        }
        private void SobrInfoToXml()
        {
            log.Info("Starting SOBR conversion to xml..");
            List<CSobrTypeInfos> csv = _dTypeParser.SobrInfo;
            List<CRepoTypeInfos> repos = _dTypeParser.ExtentInfo;
            csv = csv.OrderBy(x => x.Name).ToList();

            XDocument doc = XDocument.Load(_testFile);
            var summary = new XElement("info", fillerText.SobrSummary);
            XElement extElement = new XElement("sobrs");
            extElement.Add(summary);
            doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            foreach (var c in csv)
            {
                int repoCount = repos.Count(x => x.SobrName == c.Name);

                string newName = c.Name;
                if (_scrub)
                    newName = _scrubber.ScrubItem(c.Name, "sobr");



                _repoJobCount.TryGetValue(c.Name, out int jobCount);
                var xml = new XElement("sobr",
                    new XElement("name", newName),
                    new XElement("policy", c.PolicyType),
                    new XElement("extentCount", repoCount),
                    new XElement("extents", c.Extents.Count()),
                    new XElement("pervm", c.UsePerVMBackupFiles),
                    new XElement("performfull", c.PerformFullWhenExtentOffline),
                    new XElement("captier", c.EnableCapacityTier),
                    new XElement("restoreperiod", c.OperationalRestorePeriod),
                    new XElement("capacitytext", c.CapacityExtent),
                    new XElement("offloadwindow", c.OffloadWindowOptions),
                    new XElement("overridespace", c.OverrideSpaceThreshold),
                    new XElement("encryption", c.EncryptionEnabled),
                    new XElement("copy", c.CapacityTierCopyPolicyEnabled),
                    new XElement("move", c.CapacityTierMovePolicyEnabled),
                    new XElement("archiveenabled", c.ArchiveTierEnabled),
                    new XElement("archiveextent", c.ArchiveExtent),
                    new XElement("archiveperiod", c.ArchivePeriod),
                    new XElement("costoptimized", c.CostOptimizedArchiveEnabled),
                    new XElement("archivefull", c.ArchiveFullBackupModeEnabled),
                    new XElement("plugin", c.PluginBackupsOffloadEnabled),
                    new XElement("plugincopy", c.CopyAllPluginBackupsEnabled),
                    new XElement("copyallmachine", c.CopyAllMachineBackupsEnabled),
                    new XElement("id", c.Id),
                    new XElement("desc", c.Description),
                    new XElement("overridepolicy", c.OverridePolicyEnabled),
                    new XElement("captiertype", c.CapTierType),
                    new XElement("immuteEnabled", c.ImmuteEnabled),
                    new XElement("immutePeriod", c.ImmutePeriod),
                    new XElement("sizeLimit", c.SizeLimit),
                    new XElement("sizeLimitEnabled", c.SizeLimitEnabled),
                    new XElement("jobcount", jobCount
                    ));

                extElement.Add(xml);
            }
            doc.Save(_testFile);
            log.Info("Starting SOBR conversion to xml..done!");
        }
        private void ExtentXmlFromCsv()
        {
            log.Info("converting extent info to xml");
            List<CRepoTypeInfos> csv = _dTypeParser.ExtentInfo;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.SobrName).ToList();

            XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("extent");
            var summary = new XElement("info", fillerText.SobrExtentSummary);
            extElement.Add(summary);
            doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            foreach (var c in csv)
            {
                string newName = c.RepoName;
                string sobrName = c.SobrName;
                string hostName = c.Host;
                string path = c.Path;

                if (_scrub)
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

                var xml = new XElement("repository",
                    new XElement("Name", newName),
                    new XElement("Tasks", c.MaxTasks,
                    new XAttribute("color", c.Povisioning)),
                    new XElement("Path", path),
                    new XElement("sobr", sobrName),
                    new XElement("RAM", c.Ram),
                    new XElement("AutoGate", c.IsAutoGateway),
                    new XElement("Cores", c.Cores),
                    new XElement("Host", hostName),
                    new XElement("freespace", Math.Round((decimal)c.FreeSPace / 1024, 2)),
                    new XElement("totalspace", Math.Round((decimal)c.TotalSpace / 1024, 2)),
                    new XElement("freespacepercent", freePercent),
                    new XElement("align", c.AlignBlocks),
                    new XElement("type", type),
                    new XElement("rotate", c.IsRotatedDriveRepository),
                    new XElement("uncompress", c.IsDecompress),
                    new XElement("immute", c.IsImmutabilitySupported
                    ));

                extElement.Add(xml);
            }
            doc.Save(_testFile);
            log.Info("converting extent info to xml..done!");
        }
        private void RepoInfoToXml()
        {
            log.Info("converting repository info to xml");
            List<CRepoTypeInfos> csv = _dTypeParser.RepoInfos;
            csv = csv.OrderBy(x => x.Name).ToList();
            //csv = csv.OrderBy(y => y.sobrName).ToList();

            XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("repositories");
            var summary = new XElement("info", fillerText.RepoSummary);
            extElement.Add(summary);
            doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            foreach (var c in csv)
            {
                string name = c.Name;
                string host = c.Host;
                string path = c.Path;

                if (_scrub)
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


                var xml = new XElement("repository",
                    new XElement("Name", name),
                    new XElement("Tasks", c.MaxTasks,
                    new XAttribute("color", c.Povisioning)),
                    new XElement("Path", path),
                    new XElement("RAM", FilterZeros(c.Ram)),
                    new XElement("AutoGate", c.IsAutoGateway),
                    new XElement("Cores", FilterZeros(c.Cores)),
                    new XElement("freespace", freeSpace),
                    new XElement("totalspace", totalSpace),
                    new XElement("freespacepercent", percentFree),
                    new XElement("uncompress", c.IsDecompress),
                    new XElement("fpath", c.FriendlyPath),
                    new XElement("chainlimit", c.HasBackupChainLengthLimitation),
                    new XElement("dedup", c.IsDedupStorage),
                    new XElement("rotated", c.IsRotatedDriveRepository),
                    new XElement("sansnaponly", c.IsSanSnapshotOnly),
                    new XElement("isUnavailable", c.IsUnavailable),
                    new XElement("pervm", c.SplitStoragesPerVm),
                    new XElement("align", c.AlignBlocks),
                    new XElement("rotate", c.IsRotatedDriveRepository),
                    new XElement("status", c.Status),
                    new XElement("type", c.TypeDisplay),
                    new XElement("host", host),
                    new XElement("immute", c.IsImmutabilitySupported),
                    new XElement("jobcount", jobCount
                    ));

                extElement.Add(xml);
            }
            doc.Save(_testFile);
            log.Info("converting repository info to xml..done!");
        }
        private void ProxyXmlFromCsv()
        {
            log.Info("converting proxy info to xml");
            List<CProxyTypeInfos> csv = _dTypeParser.ProxyInfo();

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(y => y.Type).ToList();
            XDocument doc = XDocument.Load(_testFile);

            XElement serverRoot = new XElement("proxies");
            //testbelow
            //XElement serverRoot2 = new XElement("category", new XAttribute("catagory","proxies"));
            //doc.Root.Add(serverRoot2);
            //testabove
            doc.Root.Add(serverRoot);
            var summary = new XElement("info", fillerText.ProxySummary);
            serverRoot.Add(summary);

            foreach (var c in csv)
            {

                if (_scrub)
                {
                    c.Name = Scrub(c.Name);
                    c.Host = Scrub(c.Host);
                }

                var xml2 = new XElement("proxy");
                xml2.Add(AddXelement(c.Name, "Name", "Proxy Host Name"));
                xml2.Add(AddXelement(c.MaxTasksCount.ToString(), "Tasks", "Max tasks proxy is set to accept", c.Provisioning));
                xml2.Add(AddXelement(c.Cores.ToString(), "Cores", "Total detecte CPU Cores (no hyper-threading)"));
                xml2.Add(AddXelement(c.Ram.ToString(), "RAM", "Total deteced ram on server"));
                xml2.Add(AddXelement(c.Type, "Proxy Type", "Proxy type defined in VBR"));
                xml2.Add(AddXelement(c.TransportMode, "Transport Mode", "Transport mode assigned to proxy"));
                xml2.Add(AddXelement(c.FailoverToNetwork, "Failover to NBD", "If true, proxy is configured to fail back to network mode if primary transport mode fails"));
                xml2.Add(AddXelement(c.ChassisType, "Chassis", "Shows if proxy is physical or virtual"));
                xml2.Add(AddXelement(c.CachePath, "Cache Path", "Path defined for CDP proxy to use. Applies to CDP proxy only"));
                xml2.Add(AddXelement(c.CacheSize, "Cache Size", "Cache size specified for CDP proxy. Applies to CDP proxy only."));
                xml2.Add(AddXelement(c.Host, "Host", "Actual server name that the proxy role is installed on."));
                xml2.Add(AddXelement(c.IsDisabled, "Is Disabled", "Defines if the proxy is manually disabled. If true, the user has selected this option in the GUI."));


                serverRoot.Add(xml2);


                //test area
                //#region testARea
                //var xml = new XElement("entity",
                //    new XAttribute("entityName", "Proxy"));
                //xml.Add(AddXelement(c.Name, "Name", "Proxy Host Name"));
                //xml.Add(AddXelement(c.MaxTasksCount.ToString(), "Tasks", "Max tasks proxy is set to accept", c.Provisioning));
                //xml.Add(AddXelement(c.Cores.ToString(), "Cores", "Total detecte CPU Cores (no hyper-threading)"));
                //xml.Add(AddXelement(c.Ram.ToString(), "RAM", "Total deteced ram on server"));
                //xml.Add(AddXelement(c.Type, "Proxy Type", "Proxy type defined in VBR"));
                //xml.Add(AddXelement(c.TransportMode, "Transport Mode", "Transport mode assigned to proxy"));
                //xml.Add(AddXelement(c.FailoverToNetwork, "Failover to NBD", "If true, proxy is configured to fail back to network mode if primary transport mode fails"));
                //xml.Add(AddXelement(c.ChassisType, "Chassis", "Shows if proxy is physical or virtual"));
                //xml.Add(AddXelement(c.CachePath, "Cache Path", "Path defined for CDP proxy to use. Applies to CDP proxy only"));
                //xml.Add(AddXelement(c.CacheSize, "Cache Size", "Cache size specified for CDP proxy. Applies to CDP proxy only."));
                //xml.Add(AddXelement(c.Host, "Host", "Actual server name that the proxy role is installed on."));
                //xml.Add(AddXelement(c.IsDisabled, "Is Disabled", "Defines if the proxy is manually disabled. If true, the user has selected this option in the GUI."));


                //serverRoot2.Add(xml);

                //#endregion

                //doc.Add(xml);


            }
            doc.Save(_testFile);
            log.Info("converting proxy info to xml..done!");
        }

        private void ProtectedVmCounter(List<CViProtected> protectedList, List<string> uniqueVmList, int vmcounter, int protectedCounter)
        {
            foreach (var p in protectedList)
            {
                if (!uniqueVmList.Contains(p.Name))
                {
                    vmcounter++;
                    protectedCounter++;
                    uniqueVmList.Add(p.Name);
                }
            }
        }
        private void ServerXmlFromCsv()
        {
            log.Info("converting server info to xml");
            List<CServerTypeInfos> csv = _dTypeParser.ServerInfo();

            csv = csv.OrderBy(x => x.Name).ToList();
            csv = csv.OrderBy(x => x.Type).ToList();

            CCsvParser csvp = new();
            var protectedVms = csvp.ViProtectedReader().ToList();
            var unProtectedVms = csvp.ViUnProtectedReader().ToList();

            XDocument doc = XDocument.Load(_testFile);
            XElement serverRoot = new XElement("servers");
            doc.Root.Add(serverRoot);
            doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
            var summary = new XElement("info", fillerText.ServerInfo);
            serverRoot.Add(summary);

            //list to ensure we only count unique VMs
            List<string> countedVMs = new();

            foreach (var c in csv)
            {
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
                if (_scrub)
                    newName = Scrub(newName);

                //create XML entries
                var xml = new XElement("server",
                    new XElement("Name", newName),
                    new XElement("Cores", c.Cores),
                    new XElement("RAM", c.Ram),
                    new XElement("Type", c.Type),
                    new XElement("ApiVersion", c.ApiVersion),
                    new XElement("proxyrole", proxyRole),
                    new XElement("repo", repoRole),
                    new XElement("wanacc", wanRole),
                    new XElement("isavailable", c.IsUnavailable),
                    new XElement("protectedVms", pVmStr),
                    new XElement("unProtectedVms", upVmStr),
                    new XElement("totalVms", tVmStr
                    ));

                serverRoot.Add(xml);

                //doc.Add(xml);


            }
            doc.Save(_testFile);
            log.Info("converting server info to xml..done!");
        }
        private void JobSummaryInfoToXml()
        {
            try
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
                if (!_isImport)
                {
                    try
                    {
                        foreach (DataRow r in _cq.JobTypes.Rows)
                        {
                            string typeString = r["type"].ToString();
                            int.TryParse(typeString, out int i);
                            types.Add((CModel.EDbJobType)i);
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error("Error processing SQL JobTypes:");
                        log.Error(e.Message);
                    }
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

                XDocument doc = XDocument.Load(_testFile);

                XElement extElement = new XElement("jobSummary");
                var summary = new XElement("info", fillerText.JobSummary);
                extElement.Add(summary);
                doc.Root.Add(extElement);
                foreach (var d in typeSummary)
                {
                    var xml = new XElement("summary",
                        new XElement("type", d.Key),
                        new XElement("typeCount", d.Value));

                    extElement.Add(xml);

                }
                var totalElement = new XElement("summary",
                    new XElement("type", "TotalJobs"),
                    new XElement("typeCount", totalJobs));
                extElement.Add(totalElement);



                doc.Save(_testFile);
                log.Info("converting job summary info to xml..done!");
            }
            catch (Exception e) { log.Error(e.Message); }
        }


        private void JobConcurrency(bool isJob, int days)
        {
            log.Info("calculating concurrency");
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

                //var mirrorJobs = jobInfo.Select(x => x.JobType == "SimpleBackupCopyPolicy");
                //int mtest = mirrorJobs.Count();

                foreach (var m in mirrorJobBjobList)
                {
                    var mirrorSessions = sessionInfo.Where(y => y.JobName.StartsWith(m));

                    foreach (var sess in mirrorSessions)
                    {
                        int i = mirrorSessions.Count();
                        DateTime now = DateTime.Now;
                        double diff = (now - sess.CreationTime).TotalDays;
                        if (diff < 7)
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
                                ctList.Add(ParseConcurrency(epB, 7));
                            }
                        }



                    }
                }
                foreach (var b in jobInfo)
                {

                    //if (b.JobType == "EpAgentBackup")
                    //{
                    //    var v = sessionInfo.Where(x => x.JobName.StartsWith(b.Name));

                    //    foreach (var s in v)
                    //    {
                    //        string n = b.Name + s.CreationTime;
                    //        if (!nameDatesList.Contains(n))
                    //        {
                    //            nameDatesList.Add(n);
                    //            ctList.Add(ParseConcurrency(s, 7));
                    //        }
                    //    }
                    //}
                    //if (b.JobType == "BackupSync")
                    //{

                    //}
                    //if (b.JobType == "EpAgentBackup")
                    //{

                    //}
                    //if(b.JobType == "SimpleBackupCopyPolicy")
                    //{
                    //    var mirrorSessions = sessionInfo.Where(x => x.JobName.StartsWith(b.Name));

                    //    foreach (var sess in mirrorSessions)
                    //    {
                    //        string nameDate = sess.JobName + sess.CreationTime.ToString();
                    //        if (!nameDatesList.Contains(nameDate))
                    //        {
                    //            nameDatesList.Add(nameDate);
                    //            ctList.Add(ParseConcurrency(sess, days));
                    //        }
                    //    }
                    //}
                    //else
                    //{
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

                    //}

                }
            }


            else if (!isJob)
            {
                foreach (var session in sessionInfo)
                {
                    DateTime now = DateTime.Now;
                    double diff = (now - session.CreationTime).TotalDays;
                    if (diff < 7)
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
                        int minuteWithHighestCount;
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

            XDocument doc = XDocument.Load(_testFile);
            string jobOrTask = "";
            if (isJob)
                jobOrTask = "job";
            if (!isJob)
                jobOrTask = "task";
            string name = "concurrencyChart" + "_" + jobOrTask + days;
            XElement extElement = new XElement(name);
            doc.Root.Add(extElement);

            foreach (var o in orderedNumList.Distinct())
            {
                var xml = new XElement("day",
                    new XElement("hour", o));
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

                            xml.Add(
                        new XElement("count", count,
                        new XAttribute("day", c.Key)));

                        }
                    }
                }
                extElement.Add(xml);
            }
            doc.Save(_testFile);
            log.Info("calculating concurrency...done!");
        }
        private void TaskConcurrency(int days)
        {
            JobConcurrency(false, days);
        }
        private void RegOptions()
        {
            var reg = new CCsvParser();
            var RegOptions = reg.RegOptionsCsvParser();
            CDefaultRegOptions defaults = new();

            XDocument doc = XDocument.Load(_testFile);
            XElement extElement = new XElement("regOptions");
            doc.Root.Add(extElement);

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
                        var xml = new XElement("rOpt",
                            new XElement("key", r.KeyName),
                            new XElement("value", r.Value)
                            );

                        extElement.Add(xml);
                    }
                }
                if (!defaults._defaultKeys.ContainsKey(r.KeyName))
                {
                    defaults._defaultKeys.TryGetValue(r.KeyName, out string setValue);
                    var xml = new XElement("rOpt",
                        new XElement("key", r.KeyName),
                        new XElement("value", r.Value)
                        );
                    extElement.Add(xml);
                }
            }
            doc.Save(_testFile);
        }
        private void JobInfoToXml()
        {
            log.Info("converting job info to xml");
            List<CJobTypeInfos> csv = _dTypeParser.JobInfos;
            csv = csv.OrderBy(x => x.RepoName).ToList();
            csv = csv.OrderBy(y => y.JobType).ToList();
            csv = csv.OrderBy(x => x.Name).ToList();

            XDocument doc = XDocument.Load(_testFile);

            XElement extElement = new XElement("jobs");
            var summary = new XElement("info", fillerText.JobInfoSummary);
            extElement.Add(summary);
            doc.Root.Add(extElement);
            //doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));

            foreach (var c in csv)
            {
                string jname = c.Name;
                string repo = c.RepoName;
                if (c.EncryptionEnabled == "True")
                    _backupsEncrypted = true;
                if (_scrub)
                {
                    jname = _scrubber.ScrubItem(c.Name, "job");
                    repo = _scrubber.ScrubItem(c.RepoName, "repo");
                }
                decimal.TryParse(c.ActualSize, out decimal actualSize);

                var xml = new XElement("job",
                    new XElement("name", jname),
                    new XElement("repo", repo),
                    new XElement("sourceSize", Math.Round(actualSize / 1024 / 1024 / 1024, 2)),
                    new XElement("encrypted", c.EncryptionEnabled),
                    new XElement("alg", c.Algorithm),
                    new XElement("fulldays", c.FullBackupDays),
                    new XElement("fullkind", c.FullBackupScheduleKind),
                    new XElement("jobType", c.JobType),
                    new XElement("restorePoints", c.RestorePoints),
                    new XElement("scheduleoptions", c.ScheduleOptions),
                    new XElement("scheduleEnabledTime", c.SheduleEnabledTime),
                    new XElement("transformfulltosynth", c.TransformFullToSyntethic),
                    new XElement("transforminctosynth", c.TransformIncrementsToSyntethic),
                    new XElement("transformdays", c.TransformToSyntethicDays
                    ));

                extElement.Add(xml);
            }
            doc.Save(_testFile);
            log.Info("converting job info to xml..done!");
        }
        private void ConvertJobSessSummaryToXml()
        {
            try
            {
                CJobSessSummary jss = new(_testFile, log, _scrub, _checkLogs, _scrubber, _dTypeParser);

            }
            catch (Exception e) { }
        }
        private void JobSessionInfoToXml()
        {
            log.Info("converting job session info to xml");
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
                    XDocument newDoc = new XDocument(new XElement("root"));
                    XElement extElement = new XElement("jobSessions");

                    newDoc.Root.Add(extElement);
                    string docName = "xml\\" + cs.JobName + ".xml";
                    docName = VerifyDocName(docName);
                    newDoc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
                    XElement xml = null;
                    foreach (var c in csv)
                    {
                        try
                        {
                            if (cs.JobName == c.JobName)
                            {
                                string jname = c.JobName;
                                string vmName = c.VmName;
                                //string repo = _scrubber.ScrubItem(c.)
                                if (_scrub)
                                {
                                    jname = _scrubber.ScrubItem(c.JobName, "job");
                                    vmName = _scrubber.ScrubItem(c.VmName, "vm");
                                }


                                xml = new XElement("session",
                                    new XElement("alg", c.Alg),
                                    new XElement("backupsize", c.BackupSize),
                                    new XElement("bottleneck", c.Bottleneck),
                                    new XElement("compression", c.CompressionRatio),
                                    new XElement("creationtime", c.CreationTime),
                                    new XElement("datasize", c.DataSize),
                                    new XElement("dedupratio", c.DedupRatio),
                                    new XElement("isretry", c.IsRetry),
                                    new XElement("jobDuration", c.JobDuration),
                                    new XElement("jobName", jname),
                                    new XElement("minTime", c.minTime),
                                    new XElement("maxtime", c.maxTime),
                                    new XElement("avgTime", c.avgTime),
                                    new XElement("primBottleneck", c.PrimaryBottleneck),
                                    new XElement("processingmode", c.ProcessingMode),
                                    new XElement("status", c.Status),
                                    new XElement("taskDuration", c.TaskDuration),
                                    new XElement("vmName", vmName

                                    )); ;

                                extElement.Add(xml);

                            }
                        }
                        catch (Exception e) { }

                    }
                    try
                    {
                        newDoc.Save(docName);
                        //string xmlString = newDoc.ToString();
                        ExportHtml(docName);
                    }
                    catch (Exception e) { }

                }




            }
            //doc.Save(_testFile);
            log.Info("converting job session summary to xml..done!");
        }


        #endregion

        #region localFunctions
        private string FilterZeros(int value)
        {
            return FilterZeros((decimal)value);
        }
        private string FilterZeros(decimal value)
        {
            string s = "";

            if (value != 0)
                s = value.ToString();

            return s;
        }
        private XElement AddXelement(string data, string headerName, string tooltip)
        {
            return AddXelement(data, headerName, tooltip, "");
        }
        private XElement AddXelement(string data, string headerName)
        {
            return AddXelement(data, headerName, "", "");
        }
        private XElement AddXelement(string data, string headerName, string tooltip, string provisioning)
        {

            var xml = new XElement("td", data,
                new XAttribute("headerName", headerName),
                new XAttribute("tooltip", tooltip),
                new XAttribute("color", provisioning)
                );

            return xml;
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

        private string VerifyDocName(string docName)
        {
            if (docName.Contains('/'))
            {
                docName = docName.Replace('/', ' ');
            }
            VerifyDocPath(docName);
            return docName;

        }
        private void VerifyDocPath(string docName)
        {
            string dir = Path.GetDirectoryName(docName);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        public void Dispose()
        {

        }
        private int SplitDurationToMinutes(string duration)
        {
            try
            {
                //log.Info("splitting duration..");
                int i = 0;
                //00:01:59.4060000
                string[] split = duration.Split(':');
                int hours = 0;
                int minutes = 0;
                int seconds = 0;

                if (split[0] != "00")
                {
                    int.TryParse(split[0], out int h);
                    hours = h;
                }
                if (split[1] != "00")
                {
                    int.TryParse(split[1], out int m);
                    minutes = m;
                }
                minutes = minutes + (hours * 60);

                //log.Info("splitting duration..done!");
                return minutes;
            }
            catch (Exception e) { return 0; }
        }

        //public void ConvertToXml(bool import, bool scrub)
        //{
        //    _scrub = scrub;
        //    Work();
        //}


        private void Work()
        {
            log.Info("Starting Data conversion...");
            PreCalculations();
            HeaderInfoToXml();
            LicInfoToXml();
            ParseNonProtectedTypes();
            SecSummary();
            ServerSummaryToXml();
            BackupServerInfoToXml();
            //OverallSummaryToXml();
            SobrInfoToXml();
            ExtentXmlFromCsv();
            RepoInfoToXml();
            ProxyXmlFromCsv();
            ServerXmlFromCsv();
            JobSummaryInfoToXml();
            JobConcurrency(true, 7);
            TaskConcurrency(7);
            ProtectedWorkloadsToXml();
            //JobConcurrency(31); //does sum instead
            RegOptions();
            //JobSessionSummaryToXml();
            ConvertJobSessSummaryToXml();

            JobInfoToXml();
            try
            {
                JobSessionInfoToXml();

            }
            catch (Exception e) { }

            //_html.ExportHtml(_testFile, _backupServerName);
            ExportHtml();
            log.Info("Starting Data conversion...done!");
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
            CheckXmlFolder();
        }

        private void CheckXmlFolder()
        {
            if (!Directory.Exists("xml"))
                Directory.CreateDirectory("xml");
        }
        private void ResetRoles()
        {
            _isBackupServerWan = false;
            _isBackupServerRepo = false;
            _isBackupServerProxy = false;
            _isBackupServerProxyDisabled = false;
        }
        private void CheckServerRoles(string serverId)
        {
            //log.Info("Checking server roles..");
            ResetRoles();

            List<CProxyTypeInfos> proxy = _dTypeParser.ProxyInfo();
            List<CRepoTypeInfos> extents = _dTypeParser.ExtentInfo;
            List<CRepoTypeInfos> repos = _dTypeParser.RepoInfos;
            List<CWanTypeInfo> wans = _dTypeParser.WanInfos;

            //List<bool> enabledList = new();
            //if (proxy.Any(x => x.HostId == serverId))
            //    _isBackupServerProxy = true;
            //if ((proxy.Any(y => y.HostId == serverId && y.IsDisabled == "False")))
            //{
            //    //if(proxy.)
            //    enabledList.Add(false);
            //}
            //if ((proxy.Any(y => y.HostId == serverId && y.IsDisabled == "True")))
            //{
            //    enabledList.Add(true);
            //}

            //if (enabledList.Any(x => x == false))
            //    _isBackupServerProxyDisabled = false;

            //else
            //    _isBackupServerProxyDisabled = true;
            _isBackupServerProxyDisabled = true;

            foreach (var p in proxy)
            {
                if (p.HostId == serverId)
                {
                    _isBackupServerProxy = true;
                    if (p.IsDisabled == "False")
                    {
                        _isBackupServerProxyDisabled = false;
                        //break;
                    }
                    if (p.IsDisabled == "TRUE")
                    {
                    }
                }
            }
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

        #region HTML Handling
        private void ExportHtml()
        {
            log.Info("exporting xml to html");
            string s = TransformXMLToHTML(_testFile, "SessionReport.xsl");
            DateTime dateTime = DateTime.Now;
            string n = MainWindow._desiredPath;
            string htmlCore = "\\" + _htmlName + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd_HHmmss") + ".html";
            //string name = _outPath + htmlCore;
            string name = n + htmlCore;
            //if (_scrub)
            //    name = CVariables.safeDir + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(s);
            }
            log.Info("exporting xml to html..done!");
            //OpenHtml();
            if (MainWindow._openExplorer)
                OpenExplorer();
        }
        private void ExportHtml(string xmlFile)
        {
            log.Info("exporting xml to html");
            string s = TransformXMLToHTML(xmlFile, "SessionReport.xsl");
            string reportsFolder = "\\JobSessionReports\\";

            string jname = Path.GetFileNameWithoutExtension(xmlFile);
            if (_scrub)
                jname = _scrubber.ScrubItem(jname, "job");
            DateTime dateTime = DateTime.Now;

            string n = MainWindow._desiredPath;
            string outFolder = _outPath + reportsFolder;
            outFolder = n + reportsFolder;
            //if (_scrub)
            //    outFolder = CVariables.safeDir + reportsFolder;
            if (!Directory.Exists(outFolder))
                Directory.CreateDirectory(outFolder);
            string name = outFolder + jname + dateTime.ToString("_yyyy.MM.dd_HHmmss") + ".html";
            _latestReport = name;
            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(s);
            }
            log.Info("exporting xml to html..done!");
            //OpenHtml();
        }
        private void OpenExplorer()
        {
            Process.Start("explorer.exe", @"C:\temp\vHC");
        }
        public static string TransformXMLToHTML(string xmlFile, string xsltFile)
        {
            //log.Info("transforming XML to HTML");
            var transform = new XslCompiledTransform();
            XsltArgumentList xList = new();
            using (var reader = XmlReader.Create(File.OpenRead(xsltFile)))
            {
                transform.Load(reader);
            }

            var results = new StringWriter();
            using (var reader = XmlReader.Create(File.OpenRead(xmlFile)))
            {
                transform.Transform(reader, null, results);
            }
            //log.Info("transforming XML to HTML..done!");
            return results.ToString();
        }

        private void OpenHtml()
        {
            log.Info("opening html");
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                WebBrowser w1 = new();
                //w1.Navigate(("C:\\temp\\HC_Report.html", null,null,null));
                //string report = SaveSessionReportToTemp(_htmlOut);
                //string s = String.Format("cmd", $"/c start {0}", report);
                //Process.Start(new ProcessStartInfo(s));

                var p = new Process();
                p.StartInfo = new ProcessStartInfo(_latestReport)
                {
                    UseShellExecute = true
                };
                p.Start();
            });

            log.Info("opening html..done!");

            //CDataExport ex = new();
            //ex.OpenFolder();
        }
        #endregion
    }
}
