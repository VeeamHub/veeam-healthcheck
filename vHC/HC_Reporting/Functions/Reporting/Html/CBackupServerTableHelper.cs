// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    internal class CBackupServerTableHelper
    {
        private static readonly CLogger log = CGlobals.Logger;
        private readonly BackupServer backupServer;
        private readonly bool scrub;

        private readonly bool hasFixes = false;

        public CBackupServerTableHelper(bool scrub)
        {
            log.Debug("! Init BackupServerTableHelper");
            this.backupServer = new();
            this.SetBackupServerWithDbInfo();
            this.scrub = scrub;
            log.Debug("! Init BackupServerTableHelper...Done!");
        }

        public BackupServer SetBackupServerData()
        {
            // log.Debug("Setting Backup Server Data");
            this.SetElements();
            if (this.scrub)
            {
                this.ScrubElements();
            }

            // log.Debug("Setting Backup Server Data...Done!");

            return this.backupServer;
        }

        private void SetElements()
        {
            // log.Debug("Setting Backup Server Elements");
            this.SetConfigBackupSettings();
            this.SetDbHostNameOption2();

            // log.Debug("Setting Backup Server Elements...Done!");
        }

        private void SetBackupServerWithDbInfo()
        {
            this.backupServer.DbType = CGlobals.DBTYPE;
            this.backupServer.Edition = CGlobals.DBEdition;
            this.backupServer.DbVersion = CGlobals.DBVERSION;
            this.backupServer.DbCores = CGlobals.DBCORES;
            this.backupServer.DbRAM = CGlobals.DBRAM;
            this.backupServer.DbHostName = CGlobals.DBHOSTNAME;
        }

        private void ScrubElements()
        {
            this.backupServer.Name = CGlobals.Scrubber.ScrubItem(this.backupServer.Name, ScrubItemType.Server);
            this.backupServer.ConfigBackupTarget = CGlobals.Scrubber.ScrubItem(this.backupServer.ConfigBackupTarget, ScrubItemType.Repository);
            this.backupServer.DbHostName = CGlobals.Scrubber.ScrubItem(this.backupServer.DbHostName, ScrubItemType.Server);
        }

        private void SetConfigBackupSettings()
        {
            try
            {
                CCsvParser config = new();
                CConfigBackupCsv cv = new();
                var configBackupCsv = config.ConfigBackupCsvParser();
                cv = configBackupCsv.FirstOrDefault();

                this.backupServer.ConfigBackupEnabled = CObjectHelpers.ParseBool(cv.Enabled);
                if (this.backupServer.ConfigBackupEnabled == true)
                {
                    this.backupServer.ConfigBackupTarget = cv.Target;
                    this.backupServer.ConfigBackupEncryption = CObjectHelpers.ParseBool(cv.EncryptionOptions);
                    this.backupServer.ConfigBackupLastResult = cv.LastResult;
                    this.backupServer.ConfigBackupRetentionPoints = CObjectHelpers.ParseInt(cv.RestorePointsToKeep);
                }
            }
            catch (Exception e)
            {
                log.Error("Error processing config backup data");
                log.Error("\t" + e.Message);
            }
        }

        private string CheckFixes(string fixes)
        {
            // TODO
            return string.Empty;
        }

        private void SetDbHostNameOption2()
        {
            // LoadCsvToMemory();
            using (CDataTypesParser parser = new())
            {
                List<CServerTypeInfos> csv = CGlobals.DtParser.ServerInfos;
                CServerTypeInfos bs = csv.Where(x => x.Id == CGlobals.backupServerId).FirstOrDefault();

                // var test = csv.Where(x => x.Id == CGlobals._backupServerId).FirstOrDefault();

                // log.Debug("backup server ID = " +CGlobals._backupServerId);
                CCsvParser config = new();

                if (this.backupServer.Version == string.Empty || this.backupServer.Version == null || this.backupServer.DbHostName == null || this.backupServer.DbHostName == string.Empty)
                {
                    try
                    {
                        var records = config.BnrCsvParser();// .ToList();
                        if (records == null)
                        {
                            log.Warning("BNR CSV parser returned null. Skipping version and host name setting.");
                        }
                        else
                        {
                            var r2 = records.ToList();
                            if (r2.Count < 1)
                            {
                                log.Warning("No records found in BNR CSV parser. Skipping version and host name setting.");
                            }
                            else
                            {
                                if (CGlobals.VBRFULLVERSION == null || CGlobals.VBRFULLVERSION == string.Empty)
                                {
                                    this.backupServer.Version = r2[0].Version;
                                }
                                else
                                {
                                    this.backupServer.Version = CGlobals.VBRFULLVERSION;
                                }

                                if (string.IsNullOrEmpty(this.backupServer.DbHostName))
                                {
                                    this.backupServer.DbHostName = r2[0].SqlServer;
                                }

                                this.backupServer.FixIds = this.CheckFixes(r2[0].Fixes);
                                this.backupServer.HasFixes = this.hasFixes;

                                if (this.backupServer.DbType == CGlobals.PgTypeName)
                                {
                                    this.backupServer.DbHostName = r2[0].PgHost;
                                }
                            }
                        }
                    }
                    catch (Exception f)
                    {
                        log.Error("VBR Server Version parsing error:");
                        log.Error("\t" + f.Message);
                    }
                }

                try
                {
                    // Issue #47: Check if backup server was found before accessing properties
                    if (bs == null)
                    {
                        log.Warning("Backup server not found in server list. This may indicate PowerShell collection failure or empty CSV data.");
                        log.Warning("Setting backup server to default values.");
                        this.backupServer.Name = "Unknown";
                        this.backupServer.IsLocal = true;
                        this.backupServer.DbHostName = "LocalHost";
                        this.backupServer.DbCores = 0;
                        this.backupServer.DbRAM = 0;
                        return; // Exit early to avoid further null reference issues
                    }

                    this.backupServer.Name = bs.Name;
                    if (string.IsNullOrEmpty(this.backupServer.DbHostName))
                    {
                        // DbHostName is null or empty, assume local
                        this.backupServer.IsLocal = true;
                        this.backupServer.DbHostName = "LocalHost";
                        this.backupServer.DbCores = 0;
                        this.backupServer.DbRAM = 0;
                    }
                    else if (!bs.Name.Contains(this.backupServer.DbHostName, StringComparison.OrdinalIgnoreCase)
                        && this.backupServer.DbHostName != "localhost" && this.backupServer.DbHostName != "LOCALHOST")
                    {
                        this.backupServer.IsLocal = false;
                    }
                    else if (bs.Name.Contains(this.backupServer.DbHostName, StringComparison.OrdinalIgnoreCase) || this.backupServer.DbHostName == "localhost")
                    {
                        this.backupServer.IsLocal = true;
                        this.backupServer.DbHostName = "LocalHost";
                        this.backupServer.DbCores = 0;
                        this.backupServer.DbRAM = 0;
                    }
                }
                catch (Exception g)
                {
                    log.Error("Error processing SQL resource data");
                    log.Error("\t" + g.Message);
                    log.Debug("_backupServer = " + this.backupServer.Version);
                    if (bs != null)
                    {
                        log.Debug("backupServer = " + bs.ApiVersion);
                    }
                    else
                    {
                        log.Debug("backupServer = NULL");
                    }
                }

                // try { _backupServer.Name = backupServer.Name; }
                // catch (NullReferenceException e)
                // { log.Error("[VBR Config] failed to add backup server Name:\n\t" + e.Message); }

                // b.Version = veeamVersion;
                // Issue #47: Only set cores/RAM if backup server was found
                if (bs != null)
                {
                    try { this.backupServer.Cores = bs.Cores; }
                    catch (NullReferenceException e)
                    { log.Error("[VBR Config] failed to add backup server cores:\n\t" + e.Message); }
                    try { this.backupServer.RAM = bs.Ram; }
                    catch (NullReferenceException e)
                    { log.Error("[VBR Config] failed to add backup server RAM:\n\t" + e.Message); }
                }
            }
        }

        private void LoadCsvToMemory()
        {
            string serverName = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;
            string file = Path.Combine(CVariables.vbrDir, $"{serverName}_vbrinfo.csv");
            log.Info("looking for VBR CSV at: " + file);
            var res = CCsvsInMemory.GetCsvData(file);
            if (res != null && res.Count > 0)
            {
                log.Info("VBR CSV data loaded successfully. Number of rows: " + res.Count);
                for (int i = 0; i < Math.Min(5, res.Count); i++)
                {
                    log.Debug($"Row {i + 1}: {string.Join(", ", res[i].Select(kv => $"{kv.Key}: {kv.Value}"))}");
                }

                // Try to find the Version key, with or without quotes
                var dict = res[0];
                string versionStr = null;
                if (dict.TryGetValue("Version", out versionStr) && !string.IsNullOrWhiteSpace(versionStr))
                {
                    // found as Version
                }
                else if (dict.TryGetValue("\"Version\"", out versionStr) && !string.IsNullOrWhiteSpace(versionStr))
                {
                    // found as "Version"
                }
                else
                {
                    // Try to find any key that matches Version ignoring quotes and case
                    var versionKey = dict.Keys.FirstOrDefault(k => k.Trim('\"').Equals("Version", StringComparison.OrdinalIgnoreCase));
                    if (versionKey != null)
                    {
                        versionStr = dict[versionKey];
                    }
                }

                if (!string.IsNullOrWhiteSpace(versionStr))
                {
                    // Remove any leading/trailing quotes
                    versionStr = versionStr.Trim('\"');
                    CGlobals.VBRFULLVERSION = versionStr;
                    var majorVersionPart = versionStr.Split('.')[0];
                    if (int.TryParse(majorVersionPart, out int majorVersion))
                    {
                        CGlobals.VBRMAJORVERSION = majorVersion;
                        log.Info($"Set VBRMAJORVERSION to {majorVersion} from Version '{versionStr}'");
                    }
                    else
                    {
                        log.Error($"Failed to parse major version from Version '{versionStr}'");
                    }
                }
                else
                {
                    log.Error("Version field not found in CSV data.");
                }
            }
            else
            {
                log.Error("Failed to load VBR CSV data or no data found.");
            }
        }
    }
}
