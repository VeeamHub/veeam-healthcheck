// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly CLogger log = CGlobals.Logger;
        private readonly CCsvParser csvParser = new();

        private List<CServerTypeInfos> serverInfo = new();

        private Dictionary<string, int> serverSummaryInfo;

        // private List<CSobrTypeInfos> _sobrInfo;
        // private List<CRepoTypeInfos> _extentInfo;
        // private List<CProxyTypeInfos> _proxyInfo;
        // private List<CJobTypeInfos> _jobInfo;
        // private List<CJobSessionInfo> _jobSession;
        private readonly List<int> protectedJobIds = new();
        private readonly List<string> typeList = new();

        // private List<CServerTypeInfos> _serverInfo;
        public List<CJobTypeInfos> JobInfos = new();
        public List<CJobSessionInfo> JobSessions =new();
        public List<CServerTypeInfos> ServerInfos = new();
        public List<CRepoTypeInfos> ExtentInfo = new();
        public List<CProxyTypeInfos> ProxyInfos = new();

        public Dictionary<string, int> ServerSummaryInfo { get { return this.serverSummaryInfo; } }

        public List<CSobrTypeInfos> SobrInfo = new();
        public List<CArchiveTierCsv> ArchiveTierInfos = new();

        // public List<CLicTypeInfo> LicInfo { get { return LicInfos(); } }
        public List<CRepoTypeInfos> RepoInfos = new();
        public List<CWanTypeInfo> WanInfos = new();
        public CConfigBackupCsv ConfigBackup = new();
        public List<CNetTrafficRulesCsv> NetTrafficRules = new();

        public List<int> ProtectedJobIds { get { return this.protectedJobIds; } }

        public CDataTypesParser()
        {
            this.Init();
        }

        public void Init()
        {
            this.log.Info("[CDataTypesParser] Init() started");
            try
            {
                this.log.Info("[CDataTypesParser] Creating server summary dictionary...");
                this.serverSummaryInfo = new Dictionary<string, int>();
                this.log.Info("[CDataTypesParser] Parsing JobInfo...");
                this.JobInfos = this.JobInfo();
                this.log.Info("[CDataTypesParser] Parsing JobSessionInfo...");
                this.JobSessions = this.JobSessionInfo().ToList();
                this.log.Info("[CDataTypesParser] Parsing ServerInfo...");
                this.serverInfo = this.ServerInfo();
                this.ServerInfos = this.serverInfo;
                this.log.Info("[CDataTypesParser] Parsing ProxyInfo...");
                this.ProxyInfos = this.ProxyInfo();
                this.log.Info("[CDataTypesParser] Parsing SobrExtInfo...");
                this.ExtentInfo = this.SobrExtInfo();
                this.log.Info("[CDataTypesParser] Parsing SobrInfos...");
                this.SobrInfo = this.SobrInfos();
                this.log.Info("[CDataTypesParser] Parsing ArchiveTierInfo...");
                this.ArchiveTierInfos = this.ArchiveTierCsvInfo();
                this.log.Info("[CDataTypesParser] Parsing RepoInfo...");
                this.RepoInfos = this.RepoInfo();
                this.log.Info("[CDataTypesParser] Parsing WanInfo...");
                this.WanInfos = this.WanInfo();
                this.log.Info("[CDataTypesParser] Parsing ConfigBackupInfo...");
                this.ConfigBackup = this.ConfigBackupInfo();
                this.log.Info("[CDataTypesParser] Parsing NetTrafficRules...");
                this.NetTrafficRules = this.NetTrafficRulesParser();

                this.log.Info("[CDataTypesParser] Filtering and counting types...");
                this.FilterAndCountTypes();
                this.log.Info("[CDataTypesParser] Init() completed successfully");
            }
            catch (Exception ex)
            {
                this.log.Error($"[CDataTypesParser] Failed to initialize data parser: {ex.Message}");
                this.log.Debug($"[CDataTypesParser] Stack trace: {ex.StackTrace}");
            }
        }

        public void Dispose() { }

        private List<CSobrTypeInfos> SobrInfos()
        {
            var sobrCsv = this.csvParser.SobrCsvParser();// .ToList();
            List<CCapTierCsv> capTierCsv;
            try
            {
                capTierCsv = this.csvParser.CapTierCsvParser()?.ToList() ?? new List<CCapTierCsv>();
            }
            catch (Exception ex)
            {
                this.log.Error($"[CDataTypesParser] Failed to parse CapTier CSV: {ex.Message}");
                this.log.Debug($"[CDataTypesParser] Stack trace: {ex.StackTrace}");
                capTierCsv = new List<CCapTierCsv>();
            }

            List<CSobrTypeInfos> eInfoList = new List<CSobrTypeInfos>();

            if (sobrCsv != null)
            {
                List<CSobrCsvInfo> s2;
                try
                {
                    s2 = sobrCsv.ToList();
                    this.log.Info($"[CDataTypesParser] Processing {s2.Count} SOBR records from CSV");
                }
                catch (Exception ex)
                {
                    this.log.Error($"[CDataTypesParser] Failed to parse SOBR CSV: {ex.Message}");
                    this.log.Debug($"[CDataTypesParser] Stack trace: {ex.StackTrace}");
                    return eInfoList;
                }

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
                                bool.TryParse(cap.Immute, out bool immute);
                                eInfo.ImmuteEnabled = immute;
                                eInfo.ImmutePeriod = cap.ImmutePeriod;
                                this.log.Debug($"[CDataTypesParser] SOBR '{s.Name}' CapTier - Immute: '{cap.Immute}' => ImmuteEnabled: {immute}, ImmutePeriod: {cap.ImmutePeriod}");
                                bool.TryParse(cap.SizeLimitEnabled, out bool sizeLimitEnabled);
                                eInfo.SizeLimitEnabled = sizeLimitEnabled;
                                if (eInfo.SizeLimitEnabled == true)
                                {
                                    eInfo.SizeLimit = cap.SizeLimit;
                                }


                                eInfo.CapTierType = cap.Type;
                            }
                        }
                    }

                    eInfo.ArchiveExtent = s.ArchiveExtent;
                    bool.TryParse(s.ArchiveFullBackupModeEnabled, out bool archiveFullBackup);
                    eInfo.ArchiveFullBackupModeEnabled = archiveFullBackup;
                    eInfo.ArchivePeriod = s.ArchivePeriod;
                    bool.TryParse(s.ArchiveTierEnabled, out bool archiveTier);
                    eInfo.ArchiveTierEnabled = archiveTier;
                    eInfo.CapacityExtent = s.CapacityExtent;
                    bool.TryParse(s.CapacityTierCopyPolicyEnabled, out bool capTierCopy);
                    eInfo.CapacityTierCopyPolicyEnabled = capTierCopy;
                    bool.TryParse(s.CapacityTierMovePolicyEnabled, out bool capTierMove);
                    eInfo.CapacityTierMovePolicyEnabled = capTierMove;
                    bool.TryParse(s.CopyAllMachineBackupsEnabled, out bool copyAllMachine);
                    eInfo.CopyAllMachineBackupsEnabled = copyAllMachine;
                    bool.TryParse(s.CopyAllPluginBackupsEnabled, out bool copyAllPlugin);
                    eInfo.CopyAllPluginBackupsEnabled = copyAllPlugin;
                    bool.TryParse(s.CostOptimizedArchiveEnabled, out bool costOptimized);
                    eInfo.CostOptimizedArchiveEnabled = costOptimized;
                    eInfo.Description = s.Description;
                    bool.TryParse(s.EnableCapacityTier, out bool enableCapTier);
                    eInfo.EnableCapacityTier = enableCapTier;
                    if (!eInfo.EnableCapacityTier)
                    {
                        eInfo.CapacityTierCopyPolicyEnabled = false;
                        eInfo.CapacityTierMovePolicyEnabled = false;
                    }

                    bool.TryParse(s.EncryptionEnabled, out bool encryptionEnabled);
                    eInfo.EncryptionEnabled = encryptionEnabled;
                    eInfo.EncryptionKey = s.EncryptionKey;
                    eInfo.Extents = s.Extents;
                    eInfo.Id = s.Id;
                    eInfo.Name = s.Name;
                    eInfo.OffloadWindowOptions = s.OffloadWindowOptions;
                    eInfo.OperationalRestorePeriod = this.ParseToInt(s.OperationalRestorePeriod);
                    bool.TryParse(s.OverridePolicyEnabled, out bool overridePolicy);
                    eInfo.OverridePolicyEnabled = overridePolicy;
                    eInfo.OverrideSpaceThreshold = this.ParseToInt(s.OverrideSpaceThreshold);
                    bool.TryParse(s.PerformFullWhenExtentOffline, out bool performFull);
                    eInfo.PerformFullWhenExtentOffline = performFull;
                    bool.TryParse(s.PluginBackupsOffloadEnabled, out bool pluginOffload);
                    eInfo.PluginBackupsOffloadEnabled = pluginOffload;
                    eInfo.PolicyType = s.PolicyType;
                    bool.TryParse(s.UsePerVMBackupFiles, out bool usePerVM);
                    eInfo.UsePerVMBackupFiles = usePerVM;

                    // int c = eInfo.Extents.Count();

                    // var v = ExtentInfo.Count();
                    // string[] eCount = eInfo.Extents.Split();
                    // eInfo.ExtentCount = eCount.Count();

                    eInfoList.Add(eInfo);
                }
            }

            return eInfoList;
        }

        private List<CArchiveTierCsv> ArchiveTierCsvInfo()
        {
            try
            {
                return this.csvParser.ArchiveTierCsvParser()?.ToList() ?? new List<CArchiveTierCsv>();
            }
            catch (Exception ex)
            {
                this.log.Error($"[CDataTypesParser] Failed to parse ArchiveTier CSV: {ex.Message}");
                this.log.Debug($"[CDataTypesParser] Stack trace: {ex.StackTrace}");
                return new List<CArchiveTierCsv>();
            }
        }

        private string ParseBool(string input)
        {
            bool res = false;
            try
            {
                res = bool.Parse(input);

                if (res == true)
                {

                    return "True";
                }
                else
                {

                    return "False";
                }
            }
            catch (Exception) { return string.Empty; }
        }

        private List<CRepoTypeInfos> RepoInfo()
        {
            List<CRepoTypeInfos> eInfoList = new List<CRepoTypeInfos>();

            var records = this.csvParser.RepoCsvParser();// .ToList();
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
                    eInfo.MaxTasks = this.ParseToInt(s.MaxTasks);
                    bool.TryParse(s.AlignBlock, out bool c);
                    eInfo.AlignBlocks = c;
                    eInfo.GateHosts = s.GateHosts;

                    eInfo.HostId = s.HostId;
                    if (eInfo.HostId == "00000000-0000-0000-0000-000000000000" )// && s.Group != "ArchiveRepository")
                    {
                        eInfo.IsAutoGateway = true;
                    }


                    if (eInfo.HostId != "00000000-0000-0000-0000-000000000000")
                    {
                        eInfo.Host = this.FilterHostIdToName(eInfo.HostId);
                    }

                    if (!this.StoragesToSkip().Contains(s.Type))
                    {
                        eInfo.Ram = this.MatchHostIdtoRam(eInfo.HostId);
                        eInfo.Cores = this.MatchHostIdToCPU(eInfo.HostId);

                        // todo : If cloud, skip provisioning
                        eInfo.Povisioning = this.CalcRepoOptimalTasks(eInfo.MaxTasks, eInfo.Cores, eInfo.Ram);

                        eInfo.FreeSPace = this.ParseToInt(s.FreeSpace);
                        eInfo.TotalSpace = this.ParseToInt(s.TotalSpace);
                    }
                    else
                    {
                        // Skip SanSnapshotOnly repos - they're metadata placeholders, not real storage targets
                        // See GitHub Issue #29: https://github.com/VeeamHub/veeam-healthcheck/issues/29
                        continue;
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
            foreach (var s in this.serverInfo)
            {
                if (hostId == s.Id)
                {

                    return s.Name;
                }
            }

            return string.Empty;
        }

        private List<CRepoTypeInfos> SobrExtInfo()
        {
            var records = this.csvParser.SobrExtParser();
            List<CRepoTypeInfos> eInfoList = new List<CRepoTypeInfos>();
            
            if (records == null)
            {
                this.log.Warning("[CDataTypesParser] SobrExtParser returned null. CSV file may be missing or unreadable.");
                return eInfoList;
            }
            
            List<CSobrExtentCsvInfos> recordsList;
            try
            {
                recordsList = records.ToList();
                this.log.Info($"[CDataTypesParser] Processing {recordsList.Count} SOBR extent records from CSV");
            }
            catch (Exception ex)
            {
                this.log.Error($"[CDataTypesParser] Failed to parse SOBR extent CSV: {ex.Message}");
                this.log.Debug($"[CDataTypesParser] Stack trace: {ex.StackTrace}");
                return eInfoList;
            }
            
            if (recordsList.Count == 0)
            {
                this.log.Warning("[CDataTypesParser] No SOBR extent records found in CSV file.");
                return eInfoList;
            }
            
            if (records != null)
            {
                foreach (CSobrExtentCsvInfos s in recordsList)
                {
                    CRepoTypeInfos eInfo = new CRepoTypeInfos();

                    // eInfo.Host = s.HostName;
                    eInfo.RepoName = s.Name;
                    eInfo.Path = s.FriendlyPath;
                    bool.TryParse(s.IsUnavailable, out bool isUnavail);
                    eInfo.IsUnavailable = isUnavail;
                    eInfo.Type = s.TypeDisplay;
                    bool.TryParse(s.IsRotatedDriveRepository, out bool isRotated);
                    eInfo.IsRotatedDriveRepository = isRotated;
                    bool.TryParse(s.IsDedupStorage, out bool isDedup);
                    eInfo.IsDedupStorage = isDedup;
                    bool.TryParse(s.IsImmutabilitySupported, out bool isImmutable);
                    eInfo.IsImmutabilitySupported = isImmutable;
                    eInfo.SobrName = s.SOBR_Name;
                    eInfo.MaxTasks = this.ParseToInt(s.MaxTasks);
                    eInfo.maxArchiveTasks = this.ParseToInt(s.MaxArchiveTaskCount);
                    eInfo.isUnlimitedTaks = (s.UnlimitedTasks);
                    eInfo.dataRateLimit = this.ParseToInt(s.CombinedDataRateLimit);
                    bool.TryParse(s.UnCompress, out bool b);
                    eInfo.IsDecompress = b;
                    bool.TryParse(s.OneBackupFilePerVm, out bool b2);
                    eInfo.SplitStoragesPerVm = b2;

                    bool.TryParse(s.IsAutoDetectAffinityProxies, out bool b3);
                    eInfo.autoDetectAffinity = b3;

                    eInfo.HostId = s.HostId;
                    if (eInfo.HostId == "00000000-0000-0000-0000-000000000000" && s.Group != "ArchiveRepository")
                    {
                        eInfo.IsAutoGateway = true;
                    }


                    if (eInfo.HostId != "00000000-0000-0000-0000-000000000000")
                    {
                        eInfo.Host = this.FilterHostIdToName(eInfo.HostId);
                    }

                    eInfo.Ram = this.MatchHostIdtoRam(eInfo.HostId);
                    eInfo.Cores = this.MatchHostIdToCPU(eInfo.HostId);
                    eInfo.Povisioning = this.CalcRepoOptimalTasks(eInfo.MaxTasks, eInfo.Cores, eInfo.Ram);

                    eInfo.FreeSPace = this.ParseToInt(s.FreeSpace);
                    eInfo.TotalSpace = this.ParseToInt(s.TotalSpace);
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
            var nt = this.csvParser.NetTrafficCsvParser();
            List<CNetTrafficRulesCsv> ntList = new();
            if (nt != null)
                foreach (var c in nt)
                {
                    CNetTrafficRulesCsv cv = new();
                    cv.EncryptionEnabled = c.EncryptionEnabled;

                    // cv.
                    ntList.Add(cv);
                }

            return ntList;
        }

        private CConfigBackupCsv ConfigBackupInfo()
        {
            var configB = this.csvParser.ConfigBackupCsvParser();
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
            this.log.Info("Starting Job Csv Parse..");

            // var bjobCsv = _csvParser.BJobCsvParser();
            var bjobCsv = this.csvParser.GetDynamicBjobs();
            var jobCsv = this.csvParser.JobCsvParser().ToList();
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
                                if (!this.protectedJobIds.Contains(typeId))
                                {
                                    this.protectedJobIds.Add(typeId);
                                }

                                if (b.name == s.Name)
                                {
                                    jInfo.Name = b.name;
                                    jInfo.ActualSize = b.included_size;
                                    jInfo.RepoName = this.MatchRepoIdToRepo(b.repository_id);
                                    jInfo.JobId = b.type;
                                    if (b.type == "63")
                                    {
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(jInfo.RepoName))
                        {
                            jInfo.RepoName = s.RepoName;
                        }


                        jInfo.Algorithm = s.Algorithm;
                        jInfo.FullBackupDays = s.FullBackupDays;
                        jInfo.FullBackupScheduleKind = s.FullBackupScheduleKind;
                        jInfo.JobType = s.JobType;

                        jInfo.JobType = CJobTypesParser.GetJobType(s.JobType);

                        jInfo.Name = s.Name;

                        // jInfo.RepoName = MatchRepoIdToRepo(bjobCsv.Where(x => x.Name == s.Name).SingleOrDefault().RepositoryId);
                        jInfo.RestorePoints = this.ParseToInt(s.RestorePoints);
                        jInfo.ScheduleOptions = s.ScheduleOptions;
                        jInfo.SheduleEnabledTime = s.SheduleEnabledTime;
                        jInfo.TransformFullToSyntethic = s.TransformFullToSyntethic.ToString();
                        jInfo.TransformIncrementsToSyntethic = s.TransformIncrementsToSyntethic;
                        jInfo.TransformToSyntethicDays = s.TransformToSyntethicDays;

                        if (s.PwdKeyId != "00000000-0000-0000-0000-000000000000" && !string.IsNullOrEmpty(s.PwdKeyId))
                        {
                            jInfo.EncryptionEnabled = "True";
                        }


                        jInfo.ActualSize = s.OriginalSize.ToString();
                        eInfoList.Add(jInfo);
                    }
            }
            catch (Exception) { }

            var rec = this.csvParser.PluginCsvParser().ToList();
            if (rec != null)
                foreach (var r in rec)
                {
                    CJobTypeInfos j = new();
                    j.Name = r.Name;
                    j.JobType = CJobTypesParser.GetJobType(r.JobType);
                    j.RepoName = this.MatchRepoIdToRepo(r.TargetRepositoryId);
                    eInfoList.Add(j);
                }

            this.log.Info("Starting Job Csv Parse..ok!");

            return eInfoList;
        }

        private string MatchRepoIdToRepo(string repoId)
        {
            foreach (var e in this.SobrInfos())
            {
                if (repoId == e.Id)
                {

                    return e.Name;
                }
            }

            foreach (var r in this.RepoInfo())
            {
                if (repoId == r.Id)
                {

                    return r.Name;
                }
            }

            return string.Empty;
        }

        private List<CWanTypeInfo> WanInfo()
        {
            var records = this.csvParser.WanParser();
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
                var sessionCsv = this.csvParser.SessionCsvParser();
                if (sessionCsv == null)
                {
                    this.log.Warning("SessionCsvParser returned null - no session data available");
                    return new List<CJobSessionInfo>();
                }

                var records = sessionCsv.ToList();
                if(CGlobals.DEBUG)
                {
                    this.log.Debug(String.Format("! Sessions loaded from csv parser: " + records.Count().ToString()), false);
                }

                var jobCsv = this.csvParser.JobCsvParser();
                var jobRecords = jobCsv != null ? jobCsv.ToList() : new List<CJobCsvInfos>();
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
                        jInfo.BackupSize = this.ParseToDouble(s.BackupSize);
                        jInfo.Bottleneck = s.BottleneckDetails;
                        jInfo.CompressionRatio = s.CompressionRation;
                        jInfo.CreationTime = this.TryParseDateTime(s.CreationTime);

                        // refactoring DataSize to use OriginalSize from Jobs CSV to match used instead of provisioned size
                        try
                        {
                            var jobFromCsv = jobRecords.Where(x => x.Name == s.JobName).SingleOrDefault();
                            if (jobFromCsv != null)
                            {
                                jInfo.UsedVmSize = jobFromCsv.OriginalSize /1024 /1024 /1024;
                            }
                        }
                        catch (Exception e)
                        {
                            this.log.Error("Failed to parse job original size");
                            this.log.Error("\t" + e.ToString());
                        }

                        jInfo.DataSize = this.ParseToDouble(s.DataSize);

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
                this.log.Error("JobSessionInfo Error: "); this.log.Error("\t" + e.Message);
                return null;
            }
        }

        private DateTime TryParseDateTime(string dateTime)
        {
            // Issue #41: Enhanced DateTime parsing to handle Chinese locale formats and encoding issues
            if (string.IsNullOrWhiteSpace(dateTime))
            {
                return DateTime.MinValue;
            }

            // Remove corrupted AM/PM indicators (Chinese systems may export "??" instead of 上午/下午)
            string cleanedDateTime = dateTime.Replace("??", "").Replace("  ", " ").Trim();

            // First attempt: Try InvariantCulture (works for most standard formats)
            if (DateTime.TryParse(cleanedDateTime, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            // Second attempt: Try current culture (respects system locale)
            if (DateTime.TryParse(cleanedDateTime, out result))
            {
                return result;
            }

            // Third attempt: Try specific format patterns common in Chinese/Asian systems
            string[] commonFormats = new[]
            {
                "yyyy/MM/dd HH:mm:ss",
                "yyyy/MM/dd 上午 HH:mm:ss",  // Chinese AM
                "yyyy/MM/dd 下午 HH:mm:ss",  // Chinese PM
                "yyyy-MM-dd HH:mm:ss",
                "dd.MM.yyyy HH:mm:ss",      // European format
                "MM/dd/yyyy HH:mm:ss",      // US format
                "yyyy/MM/dd h:mm:ss tt",    // 12-hour with AM/PM
                "yyyy-MM-dd'T'HH:mm:ss",    // ISO 8601
                "yyyy-MM-dd'T'HH:mm:ss'Z'"  // ISO 8601 UTC
            };

            if (DateTime.TryParseExact(cleanedDateTime, commonFormats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
            {
                return result;
            }

            // If all attempts fail, log warning and return MinValue
            CGlobals.Logger.Debug($"Failed to parse DateTime: '{dateTime}'. Returning DateTime.MinValue.");
            return DateTime.MinValue;
        }

        private int MemoryTasksCount(int ram, int ramPerCore)
        {
            return (int)Math.Round((decimal)(ram / ramPerCore) * 3, 0, MidpointRounding.ToPositiveInfinity);
        }

        private string CalcRepoOptimalTasks(int assignedTasks, int cores, int ram)
        {
            if (cores == 0 && ram == 0)
            {

                return "NA";
            }


            CProvisionTypes pt = new();

            if (assignedTasks == -1)
            {

                return pt.OverProvisioned;
            }

            // 1 core + 4 GB RAM per 3 task

            // cores * 1.5 = Tasks

            int availableMem = ram - 4;
            int memTasks = this.MemoryTasksCount(ram, 4);
            int coreTasks = cores * 3;

            // if (CGlobals.VBRMAJORVERSION == 12)
            // {
            //    // user v12 sizing math here.
            //    memTasks = MemoryTasksCount(ram, 2);
            //    coreTasks = cores * 2;
            // }

            if (coreTasks == memTasks)
            {
                if (assignedTasks == memTasks)
                {
                    return pt.WellProvisioned;
                }


                if (assignedTasks > memTasks)
                {

                    return pt.OverProvisioned;
                }

                // if (assignedTasks < memTasks)
                //    return pt.UnderProvisioned;
            }

            if (coreTasks < memTasks)
            {
                if (assignedTasks == coreTasks)
                {
                    return pt.WellProvisioned;
                }

                // if (assignedTasks <= coreTasks)
                //    return pt.UnderProvisioned;

                if (assignedTasks > coreTasks)
                {

                    return pt.OverProvisioned;
                }
            }

            if (coreTasks > memTasks)
            {
                if (assignedTasks == memTasks)
                {
                    return pt.WellProvisioned;
                }

                // if (assignedTasks <= memTasks)
                //    return pt.UnderProvisioned;

                if (assignedTasks > memTasks)
                {

                    return pt.OverProvisioned;
                }
            }

            // 1 = underprov, 2 = on point, 3 = overprov
            return "NA";
        }

        private int MatchHostIdtoRam(string hostId)
        {
            int i = 0;

            foreach (var h in this.serverInfo)
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
            foreach (var h in this.serverInfo)
            {
                if (h.Id == hostId)
                {

                    return h.Name;
                }
            }

            return string.Empty;
        }

        private int MatchHostIdToCPU(string hostId)
        {
            int i = 0;

            foreach (var h in this.serverInfo)
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
            string message = "ServerInfo: Parsing Server CSV Data";
            if(CGlobals.DEBUG)
            {
                this.log.Debug(message);
            }


            List<CServerTypeInfos> l = new();

            var records = this.csvParser.ServerCsvParser();// .ToList();
            if (records == null)
            {
                return l;
            }


            foreach (CServerCsvInfos s in records.ToList())
            {
                CServerTypeInfos ti = new();
                if (s.ApiVersion == "Unknown")
                    ti.ApiVersion = string.Empty;
                else
                {
                    ti.ApiVersion = s.ApiVersion;
                }

                ti.Cores = this.ParseToInt(s.Cores);
                ti.CpuCount = this.ParseToInt(s.CPU);
                ti.Description = s.Description;
                ti.Id = s.Id;

                ti.PhysHostId = s.PhysHostId;
                ti.Info = s.Info;
                ti.OSInfo = s.OSInfo;
                ti.IsUnavailable = s.IsUnavailable;
                if (ti.IsUnavailable == "False")
                {
                    ti.IsUnavailable = string.Empty;
                }


                ti.Name = s.Name;

                // ti.ParentId = Guid.TryParse(s.ParentId);
                // ti.PhysHostId = Guid.TryParse(s.PhysHostId);
                ti.ProxyServicesCreds = s.ProxyServicesCreds;
                double i = this.ParseToDouble(s.Ram); // "410665353216"
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

                // ti.Uid = Guid.TryParse(s.Uid);
                this.AddServerTypeToDict(ti.Type);
                l.Add(ti);
            }

            this.log.Info(message + "..ok!");
            this.serverInfo = l;
            
            return l;
        }

        private int CalculateServerTasks(string type, string serverId)
        {
            return 0;
        }

        private void AddServerTypeToDict(string entry)
        {
            this.typeList.Add(entry);
        }

        private void FilterAndCountTypes()
        {
            // ServerInfo();
            List<string> types = this.typeList;
            types.Sort();
            foreach (var type in types)
            {
                int i = 0;
                if (!this.serverSummaryInfo.ContainsKey(type))
                {
                    foreach (var t in types)
                    {
                        if (type == t)
                        {
                            i++;
                        }
                    }

                    this.serverSummaryInfo.Add(type, i);
                }
            }
        }

        private string CalcProxyOptimalTasks(int assignedTasks, int cores, int ram)
        {
            CProxyDataFormer df = new();
            return df.CalcProxyTasks(assignedTasks, cores, ram);
        }

        private List<CProxyTypeInfos> ProxyInfo()
        {
this.log.Info("Processing Proxy info..");
var proxyCsv = this.csvParser.ProxyCsvParser();
var cdpCsv = this.csvParser.CdpProxCsvParser();
var fileCsv = this.csvParser.NasProxCsvParser();
var hvCsv = this.csvParser.HvProxCsvParser();

List<CProxyTypeInfos> proxyList = new();
if (proxyCsv != null)
            {
                this.log.Info("Checking Vi Proxies...");
                foreach (CProxyCsvInfos s in proxyCsv)
                {
                    CProxyTypeInfos ti = new();
                    ti.ChassisType = s.ChassisType;
                    ti.MaxTasksCount = this.ParseToInt(s.MaxTasksCount);
                    ti.Description = s.Description;
                    ti.Id = s.Id;
                    ti.Info = s.Info;
                    ti.Name = s.Name;
                    ti.Host = this.MatchHostIdToName(s.HostId);
                    ti.HostId = s.HostId;
                    ti.Type = s.Type;
                    ti.FailoverToNetwork = s.FailoverToNetwork;
                    ti.ChosenVm = s.ChosenVm;
                    ti.IsDisabled = this.ParseBool(s.IsDisabled);
                    ti.Options = s.Options;
                    ti.UseSsl = this.ParseBool(s.UseSsl);
                    ti.TransportMode = s.TransportMode;
                    ti.Cores = this.MatchHostIdToCPU(ti.HostId);
                    ti.Ram = this.MatchHostIdtoRam(ti.HostId);
                    ti.Provisioning = this.CalcProxyOptimalTasks(ti.MaxTasksCount, ti.Cores, ti.Ram);

                    proxyList.Add(ti);
                }

                this.log.Info("Checking Vi Proxies...ok!");
            }

if (cdpCsv != null)
            {
                this.log.Info("Checking CDP Procies...");
                foreach (CCdpProxyCsvInfo cdp in cdpCsv)
                {
                    CProxyTypeInfos p = new();
                    p.Name = cdp.Name;

                    // p.IsDisabled = ParseBool(cdp.IsEnabled);
                    p.Host = this.MatchHostIdToName(cdp.ServerId);
                    p.CachePath = cdp.CachePath;
                    p.CacheSize = cdp.CacheSize;
                    p.Type = "CDP";
                    p.Cores = this.MatchHostIdToCPU(cdp.ServerId);
                    p.Ram = this.MatchHostIdtoRam(cdp.ServerId);
                    p.ChassisType = string.Empty;
                    p.TransportMode = string.Empty;
                    p.UseSsl = string.Empty;
                    p.Provisioning = string.Empty;
                    p.FailoverToNetwork = string.Empty;

                    if (cdp.IsEnabled == "False")
                    {
                        p.IsDisabled = "True";
                    }


                    proxyList.Add(p);
                }

                this.log.Info("Checking CDP Procies...ok!");
            }

if (fileCsv != null)
            {
                this.log.Info("Checking File Procies...");
                foreach (CFileProxyCsvInfo fp in fileCsv)
                {
                    CProxyTypeInfos p = new();

                    // p.Name = fp.Server;
                    p.Name = fp.Host;
                    p.Host = this.MatchHostIdToName(fp.HostId);
                    p.MaxTasksCount = this.ParseToInt(fp.ConcurrentTaskNumber);
                    p.Type = "File";
                    p.Cores = this.MatchHostIdToCPU(fp.HostId);
                    p.Ram = this.MatchHostIdtoRam(fp.HostId);
                    p.CachePath = string.Empty;
                    p.CacheSize = string.Empty;
                    p.ChassisType = string.Empty;
                    p.TransportMode = string.Empty;
                    p.UseSsl = string.Empty;
                    p.Provisioning = string.Empty;
                    p.FailoverToNetwork = string.Empty;

                    proxyList.Add(p);
                }

                this.log.Info("Checking File Procies...ok!");
            }

if (hvCsv != null)
            {
                this.log.Info("Processing Hyper-V Proxies..");
                foreach (CHvProxyCsvInfo hp in hvCsv)
                {
                    CProxyTypeInfos p = new();
                    p.Type = hp.Type;
                    p.Name = hp.Name;
                    p.Host = this.MatchHostIdToName(hp.HostId);
                    p.MaxTasksCount = this.ParseToInt(hp.MaxTasksCount);
                    p.IsDisabled = this.ParseBool(hp.IsDisabled);
                    p.Cores = this.MatchHostIdToCPU(hp.HostId);
                    p.Ram = this.MatchHostIdtoRam(hp.HostId);
                    p.CachePath = string.Empty;
                    p.CacheSize = string.Empty;
                    p.ChassisType = string.Empty;
                    p.TransportMode = string.Empty;
                    p.UseSsl = string.Empty;
                    p.Provisioning = this.CalcProxyOptimalTasks(p.MaxTasksCount, p.Cores, p.Ram);
                    p.FailoverToNetwork = string.Empty;

                    proxyList.Add(p);
                }

                this.log.Info("Processing Hyper-V Proxies..ok!");
            }

this.log.Info("Processing Proxy info..ok!");
return proxyList;
        }

        private int ParseToInt(string input)
        {
            try
            {
                int.TryParse(input, out int i);
                return i;
            }
            catch (Exception) { return 0; };
        }

        private double ParseToDouble(string input)
        {
            try
            {
                double.TryParse(input, out double i);
                return i;
            }
            catch (Exception) { return 0; };
        }
    }
}
