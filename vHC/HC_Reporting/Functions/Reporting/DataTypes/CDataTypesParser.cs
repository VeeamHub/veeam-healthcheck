﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes.ProxyData;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    public class CDataTypesParser : IDisposable
    {
        private CLogger log = CGlobals.Logger;
        private CCsvParser _csvParser = new();

        private List<CServerTypeInfos> _serverInfo;
        private Dictionary<string, int> _serverSummaryInfo;
        //private List<CSobrTypeInfos> _sobrInfo;
        //private List<CRepoTypeInfos> _extentInfo;
        //private List<CProxyTypeInfos> _proxyInfo;
        //private List<CJobTypeInfos> _jobInfo;
        //private List<CJobSessionInfo> _jobSession;
        private List<int> _protectedJobIds = new();
        private List<string> _typeList = new();
        //private List<CServerTypeInfos> _serverInfo;

        public List<CJobTypeInfos> JobInfos { get { return JobInfo(); } }
        public List<CJobSessionInfo> JobSessions { get { return JobSessionInfo(); } }
        public List<CServerTypeInfos> ServerInfos { get { return ServerInfo(); } }
        public List<CRepoTypeInfos> ExtentInfo { get { try { return SobrExtInfo(); } catch (Exception e) { throw; } } }
        public List<CProxyTypeInfos> ProxyInfos { get { return ProxyInfo(); } }
        public Dictionary<string, int> ServerSummaryInfo { get { return _serverSummaryInfo; } }
        public List<CSobrTypeInfos> SobrInfo { get { return SobrInfos(); } }
        //public List<CLicTypeInfo> LicInfo { get { return LicInfos(); } }
        public List<CRepoTypeInfos> RepoInfos { get { return RepoInfo(); } }
        public List<CWanTypeInfo> WanInfos { get { return WanInfo(); } }
        public CConfigBackupCsv ConfigBackup { get { return ConfigBackupInfo(); } }
        public List<CNetTrafficRulesCsv> NetTraffRules { get { return NetTrafficRulesParser(); } }
        public List<int> ProtectedJobIds { get { return _protectedJobIds; } }

        public CDataTypesParser()
        {
            Init();
        }

        public void Init()
        {
            try
            {
                _serverSummaryInfo = new Dictionary<string, int>();

                FilterAndCountTypes();
            }
            catch (Exception ex)
            {

            }
        }

        public void Dispose() { }



        private List<CSobrTypeInfos> SobrInfos()
        {
            var sobrCsv = _csvParser.SobrCsvParser();//.ToList();
            var capTierCsv = _csvParser.CapTierCsvParser().ToList();// ToList();

            List<CSobrTypeInfos> eInfoList = new List<CSobrTypeInfos>();

            if (sobrCsv != null)
            {
                var s2 = sobrCsv.ToList();
                foreach (CSobrCsvInfo s in s2)
                {
                    CSobrTypeInfos eInfo = new();

                    if (capTierCsv != null)
                    {
                        var c2 = capTierCsv;
                        foreach (var cap in c2)
                        {
                            if (cap.ParentId == s.Id)
                            {
                                eInfo.ImmuteEnabled = cap.Immute;
                                eInfo.ImmutePeriod = cap.ImmutePeriod;
                                eInfo.SizeLimitEnabled = cap.SizeLimitEnabled;
                                if (cap.SizeLimitEnabled == true)
                                    eInfo.SizeLimit = cap.SizeLimit;

                                eInfo.CapTierType = cap.Type;
                            }
                        }

                    }

                    eInfo.ArchiveExtent = s.ArchiveExtent;
                    eInfo.ArchiveFullBackupModeEnabled = s.ArchiveFullBackupModeEnabled;
                    eInfo.ArchivePeriod = s.ArchivePeriod;
                    eInfo.ArchiveTierEnabled = s.ArchiveTierEnabled;
                    eInfo.CapacityExtent = s.CapacityExtent;
                    eInfo.CapacityTierCopyPolicyEnabled = s.CapacityTierCopyPolicyEnabled;
                    eInfo.CapacityTierMovePolicyEnabled = s.CapacityTierMovePolicyEnabled;
                    eInfo.CopyAllMachineBackupsEnabled = s.CopyAllMachineBackupsEnabled;
                    eInfo.CopyAllPluginBackupsEnabled = s.CopyAllPluginBackupsEnabled;
                    eInfo.CostOptimizedArchiveEnabled = s.CostOptimizedArchiveEnabled;
                    eInfo.Description = s.Description;
                    eInfo.EnableCapacityTier = s.EnableCapacityTier;
                    if (!eInfo.EnableCapacityTier)
                    {
                        eInfo.CapacityTierCopyPolicyEnabled = false;
                        eInfo.CapacityTierMovePolicyEnabled = false;
                    }
                    eInfo.EncryptionEnabled = s.EncryptionEnabled;
                    eInfo.EncryptionKey = s.EncryptionKey;
                    eInfo.Extents = s.Extents;
                    eInfo.Id = s.Id;
                    eInfo.Name = s.Name;
                    eInfo.OffloadWindowOptions = s.OffloadWindowOptions;
                    eInfo.OperationalRestorePeriod = ParseToInt(s.OperationalRestorePeriod);
                    eInfo.OverridePolicyEnabled = s.OverridePolicyEnabled;
                    eInfo.OverrideSpaceThreshold = ParseToInt(s.OverrideSpaceThreshold);
                    eInfo.PerformFullWhenExtentOffline = (s.PerformFullWhenExtentOffline);
                    eInfo.PluginBackupsOffloadEnabled = s.PluginBackupsOffloadEnabled;
                    eInfo.PolicyType = s.PolicyType;
                    eInfo.UsePerVMBackupFiles = s.UsePerVMBackupFiles;

                    //int c = eInfo.Extents.Count();

                    //var v = ExtentInfo.Count();
                    //string[] eCount = eInfo.Extents.Split();
                    //eInfo.ExtentCount = eCount.Count();


                    eInfoList.Add(eInfo);

                }

            }
            return eInfoList;
        }

        private string ParseBool(string input)
        {
            bool res = false;
            try
            {
                res = bool.Parse(input);

                if (res == true)
                    return "True";
                else
                    return "False";
            }
            catch (Exception e) { return ""; }
        }
        private List<CRepoTypeInfos> RepoInfo()
        {
            List<CRepoTypeInfos> eInfoList = new List<CRepoTypeInfos>();

            var records = _csvParser.RepoCsvParser();//.ToList();
            if (records != null)
            {
                foreach (CRepoCsvInfos s in records)
                {
                    CRepoTypeInfos eInfo = new CRepoTypeInfos();

                    eInfo.CreationTime = s.CreationTime;
                    eInfo.Description = s.Description;
                    eInfo.EndPointCryptoKeyId = s.EndPointCryptoKeyId;
                    eInfo.FriendlyPath = s.FriendlyPath;
                    eInfo.FullPath = s.FullPath;
                    eInfo.Group = s.Group;
                    eInfo.HasBackupChainLengthLimitation = (s.HasBackupChainLengthLimitation);
                    eInfo.Id = s.Id;
                    eInfo.IsDedupStorage = (s.IsDedupStorage);
                    eInfo.IsImmutabilitySupported = (s.IsImmutabilitySupported);
                    eInfo.IsRotatedDriveRepository = (s.IsRotatedDriveRepository);
                    eInfo.IsSanSnapshotOnly = (s.IsSanSnapshotOnly);
                    eInfo.IsTemporary = (s.IsTemporary);
                    eInfo.IsUnavailable = (s.IsUnavailable);
                    eInfo.Name = s.Name;
                    eInfo.Path = s.Path;
                    eInfo.SplitStoragesPerVm = (s.SplitStoragesPerVm);
                    eInfo.Status = s.Status;
                    eInfo.Type = s.Type;
                    eInfo.TypeDisplay = s.TypeDisplay;
                    eInfo.VersionOfCreation = s.VersionOfCreation;

                    bool.TryParse(s.Uncompress, out bool b);
                    eInfo.IsDecompress = b;
                    eInfo.MaxTasks = ParseToInt(s.MaxTasks);
                    bool.TryParse(s.AlignBlock, out bool c);
                    eInfo.AlignBlocks = c;
                    eInfo.GateHosts = s.GateHosts;

                    eInfo.HostId = s.HostId;
                    if (eInfo.HostId == "00000000-0000-0000-0000-000000000000" && s.Group != "ArchiveRepository")
                        eInfo.IsAutoGateway = true;

                    if (eInfo.HostId != "00000000-0000-0000-0000-000000000000")
                    {
                        eInfo.Host = FilterHostIdToName(eInfo.HostId);
                    }

                    if (!StoragesToSkip().Contains(s.Type))
                    {
                        eInfo.Ram = MatchHostIdtoRam(eInfo.HostId);
                        eInfo.Cores = MatchHostIdToCPU(eInfo.HostId);

                        // todo : If cloud, skip provisioning
                        eInfo.Povisioning = CalcRepoOptimalTasks(eInfo.MaxTasks, eInfo.Cores, eInfo.Ram);

                        eInfo.FreeSPace = ParseToInt(s.FreeSpace);
                        eInfo.TotalSpace = ParseToInt(s.TotalSpace);
                    }
                    else
                    {
                        eInfo.Ram = 0;
                        eInfo.Cores = 0;
                        eInfo.Povisioning = "NA";

                        eInfo.TotalSpace = 0;
                        eInfo.FreeSPace = 0;
                    }


                    eInfoList.Add(eInfo);

                }

            }
            return eInfoList;
        }

        private string[] StoragesToSkip()
        {
            return new string[] { "SanSnapshotOnly" };
        }
        private string FilterHostIdToName(string hostId)
        {
            foreach (var s in _serverInfo)
            {
                if (hostId == s.Id)
                    return s.Name;
            }
            return "";
        }
        private List<CRepoTypeInfos> SobrExtInfo()
        {
            var records = _csvParser.SobrExtParser();
            List<CRepoTypeInfos> eInfoList = new List<CRepoTypeInfos>();
            if (records != null)
            {
                foreach (CSobrExtentCsvInfos s in records)
                {
                    CRepoTypeInfos eInfo = new CRepoTypeInfos();


                    //eInfo.Host = s.HostName;
                    eInfo.RepoName = s.Name;
                    eInfo.Path = s.FriendlyPath;
                    eInfo.IsUnavailable = (s.IsUnavailable);
                    eInfo.Type = s.TypeDisplay;
                    eInfo.IsRotatedDriveRepository = (s.IsRotatedDriveRepository);
                    eInfo.IsDedupStorage = (s.IsDedupStorage);
                    eInfo.IsImmutabilitySupported = (s.IsImmutabilitySupported);
                    eInfo.SobrName = s.SOBR_Name;
                    eInfo.MaxTasks = ParseToInt(s.MaxTasks);
                    eInfo.maxArchiveTasks = ParseToInt(s.MaxArchiveTaskCount);
                    eInfo.isUnlimitedTaks = (s.UnlimitedTasks);
                    eInfo.dataRateLimit = ParseToInt(s.CombinedDataRateLimit);
                    bool.TryParse(s.UnCompress, out bool b);
                    eInfo.IsDecompress = b;
                    bool.TryParse(s.OneBackupFilePerVm, out bool b2);
                    eInfo.SplitStoragesPerVm = b2;

                    bool.TryParse(s.IsAutoDetectAffinityProxies, out bool b3);
                    eInfo.autoDetectAffinity = b3;

                    eInfo.HostId = s.HostId;
                    if (eInfo.HostId == "00000000-0000-0000-0000-000000000000" && s.Group != "ArchiveRepository")
                        eInfo.IsAutoGateway = true;

                    if (eInfo.HostId != "00000000-0000-0000-0000-000000000000")
                    {
                        eInfo.Host = FilterHostIdToName(eInfo.HostId);
                    }

                    eInfo.Ram = MatchHostIdtoRam(eInfo.HostId);
                    eInfo.Cores = MatchHostIdToCPU(eInfo.HostId);
                    eInfo.Povisioning = CalcRepoOptimalTasks(eInfo.MaxTasks, eInfo.Cores, eInfo.Ram);

                    eInfo.FreeSPace = ParseToInt(s.FreeSpace);
                    eInfo.TotalSpace = ParseToInt(s.TotalSpace);
                    eInfo.GateHosts = s.GateHosts;
                    bool.TryParse(s.ObjectLockEnabled, out bool b4);
                    eInfo.ObjectLockEnabled = b4;

                    eInfoList.Add(eInfo);

                }

            }
            return eInfoList;
        }
        private List<CNetTrafficRulesCsv> NetTrafficRulesParser()
        {
            var nt = _csvParser.NetTrafficCsvParser();
            List<CNetTrafficRulesCsv> ntList = new();
            if (nt != null)
                foreach (var c in nt)
                {
                    CNetTrafficRulesCsv cv = new();
                    cv.EncryptionEnabled = c.EncryptionEnabled;
                    //cv.

                    ntList.Add(cv);
                }
            return ntList;
        }
        private CConfigBackupCsv ConfigBackupInfo()
        {
            var configB = _csvParser.ConfigBackupCsvParser();
            if (configB != null)
                foreach (var c in configB)
                {
                    CConfigBackupCsv cv = new();
                    cv.Enabled = c.Enabled;
                    cv.EncryptionOptions = c.EncryptionOptions;
                    cv.Description = c.Description;
                    cv.Id = c.Id;
                    cv.LastResult = c.LastResult;
                    cv.LastState = c.LastState;
                    cv.Name = c.Name;
                    cv.NextRun = c.NextRun;
                    cv.NotificationOptions = c.NotificationOptions;
                    cv.Repository = c.Repository;
                    cv.RestorePointsToKeep = c.RestorePointsToKeep;
                    cv.ScheduleOptions = c.ScheduleOptions;
                    cv.Target = c.Target;
                    cv.Type = c.Type;

                    return cv;
                }
            return null;

        }
        private List<CJobTypeInfos> JobInfo()
        {
            log.Info("Starting Job Csv Parse..");

            //var bjobCsv = _csvParser.BJobCsvParser();
            var bjobCsv = _csvParser.GetDynamicBjobs();
            var jobCsv = _csvParser.JobCsvParser().ToList();
            List<CJobTypeInfos> eInfoList = new();
            try
            {
                if (jobCsv != null)
                    foreach (var s in jobCsv)
                    {
                        CJobTypeInfos jInfo = new CJobTypeInfos();

                        if (bjobCsv != null)
                        {
                            foreach (var b in bjobCsv)
                            {
                                int.TryParse(b.type, out int typeId);
                                if (!_protectedJobIds.Contains(typeId))
                                {
                                    _protectedJobIds.Add(typeId);

                                }
                                if (b.name == s.Name)
                                {
                                    jInfo.Name = b.name;
                                    jInfo.ActualSize = b.included_size;
                                    jInfo.RepoName = MatchRepoIdToRepo(b.repository_id);
                                    jInfo.JobId = b.type;
                                    if (b.type == "63")
                                    {

                                    }
                                }

                            }

                        }
                        if (string.IsNullOrEmpty(jInfo.RepoName))
                            jInfo.RepoName = s.RepoName;

                        jInfo.Algorithm = s.Algorithm;
                        jInfo.FullBackupDays = s.FullBackupDays;
                        jInfo.FullBackupScheduleKind = s.FullBackupScheduleKind;
                        jInfo.JobType = s.JobType;




                        jInfo.JobType = CJobTypesParser.GetJobType(s.JobType);

                        jInfo.Name = s.Name;
                        //jInfo.RepoName = MatchRepoIdToRepo(bjobCsv.Where(x => x.Name == s.Name).SingleOrDefault().RepositoryId);
                        jInfo.RestorePoints = ParseToInt(s.RestorePoints);
                        jInfo.ScheduleOptions = s.ScheduleOptions;
                        jInfo.SheduleEnabledTime = s.SheduleEnabledTime;
                        jInfo.TransformFullToSyntethic = s.TransformFullToSyntethic.ToString();
                        jInfo.TransformIncrementsToSyntethic = s.TransformIncrementsToSyntethic;
                        jInfo.TransformToSyntethicDays = s.TransformToSyntethicDays;

                        if (s.PwdKeyId != "00000000-0000-0000-0000-000000000000" && !string.IsNullOrEmpty(s.PwdKeyId))
                            jInfo.EncryptionEnabled = "True";

                        jInfo.ActualSize = s.OriginalSize.ToString();
                        eInfoList.Add(jInfo);


                    }

            }
            catch (Exception e) { }

            var rec = _csvParser.PluginCsvParser().ToList();
            if (rec != null)
                foreach (var r in rec)
                {
                    CJobTypeInfos j = new();
                    j.Name = r.Name;
                    j.JobType = CJobTypesParser.GetJobType(r.JobType);
                    j.RepoName = MatchRepoIdToRepo(r.TargetRepositoryId);
                    eInfoList.Add(j);
                }

            log.Info("Starting Job Csv Parse..ok!");

            return eInfoList;
        }

        private string MatchRepoIdToRepo(string repoId)
        {
            foreach (var e in SobrInfos())
            {
                if (repoId == e.Id)
                    return e.Name;
            }

            foreach (var r in RepoInfo())
            {
                if (repoId == r.Id)
                    return r.Name;
            }
            return "";
        }
        private List<CWanTypeInfo> WanInfo()
        {

            var records = _csvParser.WanParser();
            List<CWanTypeInfo> eInfoList = new();
            if (records != null)
            {
                foreach (CWanCsvInfos s in records)
                {
                    CWanTypeInfo jInfo = new();

                    jInfo.HostId = s.HostId;
                    jInfo.Id = s.Id;
                    jInfo.Name = s.Name;
                    jInfo.Options = s.Options;

                    eInfoList.Add(jInfo);

                }
            }


            return eInfoList;
        }
        private List<CJobSessionInfo> JobSessionInfo()
        {
            try
            {
                var records = _csvParser.SessionCsvParser().ToList();
                var jobRecords = _csvParser.JobCsvParser().ToList();
                List<CJobSessionInfo> eInfoList = new();
                if (records != null)
                    foreach (CJobSessionCsvInfos s in records)
                    {
                        CJobSessionInfo jInfo = new();

                        jInfo.avgTime = 0;
                        jInfo.avgTimeHr = 0;
                        jInfo.maxTime = 0;
                        jInfo.maxTimeHr = 0;
                        jInfo.minTime = 0;
                        jInfo.minTimeHr = 0;

                        jInfo.Name = s.JobName;
                        jInfo.Alg = s.Alg;
                        jInfo.BackupSize = ParseToDouble(s.BackupSize);
                        jInfo.Bottleneck = s.BottleneckDetails;
                        jInfo.CompressionRatio = s.CompressionRation;
                        jInfo.CreationTime = TryParseDateTime(s.CreationTime);

                        //refactoring DataSize to use OriginalSize from Jobs CSV to match used instead of provisioned size
                        try
                        {
                            var jobFromCsv = jobRecords.Where(x => x.Name == s.JobName).SingleOrDefault();
                            if (jobFromCsv != null)
                                jInfo.UsedVmSize = jobFromCsv.OriginalSize /1024 /1024 /1024;

                        }
                        catch (Exception e)
                        {
                            log.Error("Failed to parse job original size");

                        }

                        jInfo.DataSize = ParseToDouble(s.DataSize);


                        jInfo.DedupRatio = s.DedupRatio;
                        jInfo.IsRetry = s.IsRetry;
                        jInfo.JobDuration = s.JobDuration;
                        jInfo.PrimaryBottleneck = s.PrimaryBottleneck;
                        jInfo.ProcessingMode = s.ProcessingMode;
                        jInfo.Status = s.Status;
                        jInfo.TaskDuration = s.TaskDuration;
                        jInfo.VmName = s.VmName;
                        jInfo.JobName = s.JobName;
                        jInfo.JobType = s.JobType;
                        eInfoList.Add(jInfo);

                    }

                return eInfoList;
            }
            catch (Exception e)
            {
                return null;
            }

        }
        private DateTime TryParseDateTime(string dateTime)
        {
            DateTime.TryParse(dateTime, out var d);
            return d;
        }

        private int MemoryTasksCount(int ram, int ramPerCore)
        {
            return (int)Math.Round((decimal)(ram / ramPerCore) * 3, 0, MidpointRounding.ToPositiveInfinity);
        }
        private string CalcRepoOptimalTasks(int assignedTasks, int cores, int ram)
        {
            if (cores == 0 && ram == 0)
                return "NA";

            CProvisionTypes pt = new();

            if (assignedTasks == -1)
                return pt.OverProvisioned;
            // 1 core + 4 GB RAM per 3 task

            // cores * 1.5 = Tasks
            int availableMem = ram - 4;
            int memTasks = MemoryTasksCount(ram, 4);
            int coreTasks = cores * 3;

            //if (CGlobals.VBRMAJORVERSION == 12)
            //{
            //    // user v12 sizing math here.
            //    memTasks = MemoryTasksCount(ram, 2);
            //    coreTasks = cores * 2;
            //}


            if (coreTasks == memTasks)
            {
                if (assignedTasks == memTasks)
                    return pt.WellProvisioned;
                if (assignedTasks > memTasks)
                    return pt.OverProvisioned;
                //if (assignedTasks < memTasks)
                //    return pt.UnderProvisioned;
            }

            if (coreTasks < memTasks)
            {
                if (assignedTasks == coreTasks)
                    return pt.WellProvisioned;
                //if (assignedTasks <= coreTasks)
                //    return pt.UnderProvisioned;
                if (assignedTasks > coreTasks)
                    return pt.OverProvisioned;
            }
            if (coreTasks > memTasks)
            {
                if (assignedTasks == memTasks)
                    return pt.WellProvisioned;
                //if (assignedTasks <= memTasks)
                //    return pt.UnderProvisioned;
                if (assignedTasks > memTasks)
                    return pt.OverProvisioned;
            }



            // 1 = underprov, 2 = on point, 3 = overprov
            return "NA";
        }

        private int MatchHostIdtoRam(string hostId)
        {
            int i = 0;

            foreach (var h in _serverInfo)
            {
                if (h.Id == hostId)
                {
                    return h.Ram;
                }
            }

            return i;
        }
        private string MatchHostIdToName(string hostId)
        {
            foreach (var h in _serverInfo)
            {
                if (h.Id == hostId)
                    return h.Name;

            }
            return "";
        }
        private int MatchHostIdToCPU(string hostId)
        {
            int i = 0;

            foreach (var h in _serverInfo)
            {
                if (h.Id == hostId)
                {
                    return h.Cores;
                }
            }

            return i;
        }
        public List<CServerTypeInfos> ServerInfo()
        {
            log.Info("parsing server csv data");
            List<CServerTypeInfos> l = new();

            var records = _csvParser.ServerCsvParser();//.ToList();
            if (records == null)
                return l;
            foreach (CServerCsvInfos s in records.ToList())
            {
                CServerTypeInfos ti = new();
                if (s.ApiVersion == "Unknown")
                    ti.ApiVersion = "";
                else
                {
                    ti.ApiVersion = s.ApiVersion;
                }
                ti.Cores = ParseToInt(s.Cores);
                ti.CpuCount = ParseToInt(s.CPU);
                ti.Description = s.Description;
                ti.Id = s.Id;
                ti.PhysHostId = s.PhysHostId;
                ti.Info = s.Info;
                ti.OSInfo = s.OSInfo;
                ti.IsUnavailable = s.IsUnavailable;
                if (ti.IsUnavailable == "False")
                    ti.IsUnavailable = "";
                ti.Name = s.Name;
                //ti.ParentId = Guid.TryParse(s.ParentId);
                //ti.PhysHostId = Guid.TryParse(s.PhysHostId);
                ti.ProxyServicesCreds = s.ProxyServicesCreds;
                double i = ParseToDouble(s.Ram); //"410665353216"
                i = i / 1024 / 1024 / 1024;
                int e = Convert.ToInt32(i);
                ti.Ram = e;
                ti.Reference = ti.Reference;

                if (s.Type == "Local")
                {
                    ti.Type = "Windows";
                }
                else
                    ti.Type = s.Type;
                //ti.Uid = Guid.TryParse(s.Uid);
                AddServerTypeToDict(ti.Type);
                l.Add(ti);
            }
            log.Info("parsing server csv data..ok!");
            _serverInfo = l;
            return l;
        }
        private int CalculateServerTasks(string type, string serverId)
        {


            return 0;
        }

        private void AddServerTypeToDict(string entry)
        {
            _typeList.Add(entry);
        }
        private void FilterAndCountTypes()
        {
            ServerInfo();
            List<string> types = _typeList;
            types.Sort();
            foreach (var type in types)
            {
                int i = 0;
                if (!_serverSummaryInfo.ContainsKey(type))
                {
                    foreach (var t in types)
                    {
                        if (type == t)
                        {
                            i++;
                        }
                    }
                    _serverSummaryInfo.Add(type, i);
                }

            }
        }

        private string CalcProxyOptimalTasks(int assignedTasks, int cores, int ram)
        {
            CProxyDataFormer df = new();
            return df.CalcProxyTasks(assignedTasks, cores, ram);

        }
        public List<CProxyTypeInfos> ProxyInfo()
        {

            var proxyCsv = _csvParser.ProxyCsvParser();
            var cdpCsv = _csvParser.CdpProxCsvParser();
            var fileCsv = _csvParser.NasProxCsvParser();
            var hvCsv = _csvParser.HvProxCsvParser();

            List<CProxyTypeInfos> proxyList = new();
            if (proxyCsv != null)
            {
                foreach (CProxyCsvInfos s in proxyCsv)
                {
                    CProxyTypeInfos ti = new();
                    ti.ChassisType = s.ChassisType;
                    ti.MaxTasksCount = ParseToInt(s.MaxTasksCount);
                    ti.Description = s.Description;
                    ti.Id = s.Id;
                    ti.Info = s.Info;
                    ti.Name = s.Name;
                    ti.Host = MatchHostIdToName(s.HostId);
                    ti.HostId = s.HostId;
                    ti.Type = s.Type;
                    ti.FailoverToNetwork = s.FailoverToNetwork;
                    ti.ChosenVm = s.ChosenVm;
                    ti.IsDisabled = ParseBool(s.IsDisabled);
                    ti.Options = s.Options;
                    ti.UseSsl = ParseBool(s.UseSsl);
                    ti.TransportMode = s.TransportMode;
                    ti.Cores = MatchHostIdToCPU(ti.HostId);
                    ti.Ram = MatchHostIdtoRam(ti.HostId);
                    ti.Provisioning = CalcProxyOptimalTasks(ti.MaxTasksCount, ti.Cores, ti.Ram);

                    proxyList.Add(ti);
                }

            }

            if (cdpCsv != null)
            {
                foreach (CCdpProxyCsvInfo cdp in cdpCsv)
                {
                    CProxyTypeInfos p = new();
                    p.Name = cdp.Name;
                    //p.IsDisabled = ParseBool(cdp.IsEnabled);
                    p.Host = MatchHostIdToName(cdp.ServerId);
                    p.CachePath = cdp.CachePath;
                    p.CacheSize = cdp.CacheSize;
                    p.Type = "CDP";
                    p.Cores = MatchHostIdToCPU(cdp.ServerId);
                    p.Ram = MatchHostIdtoRam(cdp.ServerId);
                    p.ChassisType = "";
                    p.TransportMode = "";
                    p.UseSsl = "";
                    p.Provisioning = "";
                    p.FailoverToNetwork = "";

                    if (cdp.IsEnabled == "False")
                        p.IsDisabled = "True";

                    proxyList.Add(p);
                }

            }

            if (fileCsv != null)
            {
                foreach (CFileProxyCsvInfo fp in fileCsv)
                {
                    CProxyTypeInfos p = new();
                    //p.Name = fp.Server;
                    p.Name = fp.Host;
                    p.Host = MatchHostIdToName(fp.HostId);
                    p.MaxTasksCount = ParseToInt(fp.ConcurrentTaskNumber);
                    p.Type = "File";
                    p.Cores = MatchHostIdToCPU(fp.HostId);
                    p.Ram = MatchHostIdtoRam(fp.HostId);
                    p.CachePath = "";
                    p.CacheSize = "";
                    p.ChassisType = "";
                    p.TransportMode = "";
                    p.UseSsl = "";
                    p.Provisioning = "";
                    p.FailoverToNetwork = "";

                    proxyList.Add(p);
                }

            }

            if (hvCsv != null)
            {
                foreach (CHvProxyCsvInfo hp in hvCsv)
                {
                    CProxyTypeInfos p = new();
                    p.Type = hp.Type;
                    p.Name = hp.Name;
                    p.Host = MatchHostIdToName(hp.HostId);
                    p.MaxTasksCount = ParseToInt(hp.MaxTasksCount);
                    p.IsDisabled = ParseBool(hp.IsDisabled);
                    p.Cores = MatchHostIdToCPU(hp.HostId);
                    p.Ram = MatchHostIdtoRam(hp.HostId);
                    p.CachePath = "";
                    p.CacheSize = "";
                    p.ChassisType = "";
                    p.TransportMode = "";
                    p.UseSsl = "";
                    p.Provisioning = CalcProxyOptimalTasks(p.MaxTasksCount, p.Cores, p.Ram);
                    p.FailoverToNetwork = "";

                    proxyList.Add(p);
                }

            }

            return proxyList;
        }
        private int ParseToInt(string input)
        {
            try
            {
                int.TryParse(input, out int i);
                return i;
            }
            catch (Exception e) { return 0; };
        }
        private double ParseToDouble(string input)
        {
            try
            {
                double.TryParse(input, out double i);
                return i;
            }
            catch (Exception e) { return 0; };
        }
    }
}
