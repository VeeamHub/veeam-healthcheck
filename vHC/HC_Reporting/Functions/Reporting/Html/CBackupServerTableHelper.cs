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
        private static CLogger log = CGlobals.Logger;
        private BackupServer _backupServer;
        private readonly bool _scrub;

        private bool _hasFixes = false;

        public CBackupServerTableHelper(bool scrub)
        {
            log.Debug("! Init BackupServerTableHelper");
            _backupServer = new();
            SetBackupServerWithDbInfo();
            _scrub = scrub;
            log.Debug("! Init BackupServerTableHelper...Done!");
        }
        public BackupServer SetBackupServerData()
        {
            //log.Debug("Setting Backup Server Data");
            SetElements();
            if (_scrub)
                ScrubElements();

            //log.Debug("Setting Backup Server Data...Done!");
            return _backupServer;
        }
        private void SetElements()
        {
            //log.Debug("Setting Backup Server Elements");
            SetConfigBackupSettings();
            SetDbHostNameOption2();
            //log.Debug("Setting Backup Server Elements...Done!");

        }
        private void SetBackupServerWithDbInfo()
        {
            _backupServer.DbType = CGlobals.DBTYPE;
            _backupServer.Edition = CGlobals.DBEdition;
            _backupServer.DbVersion = CGlobals.DBVERSION;
            _backupServer.DbCores = CGlobals.DBCORES;
            _backupServer.DbRAM = CGlobals.DBRAM;
            _backupServer.DbHostName = CGlobals.DBHOSTNAME;
        }
        private void ScrubElements()
        {
            _backupServer.Name = CGlobals.Scrubber.ScrubItem(_backupServer.Name, ScrubItemType.Server);
            _backupServer.ConfigBackupTarget = CGlobals.Scrubber.ScrubItem(_backupServer.ConfigBackupTarget, ScrubItemType.Repository);
            _backupServer.DbHostName = CGlobals.Scrubber.ScrubItem(_backupServer.DbHostName, ScrubItemType.Server);
        }
        private void SetConfigBackupSettings()
        {
            try
            {
                CCsvParser config = new();
                CConfigBackupCsv cv = new();
                var configBackupCsv = config.ConfigBackupCsvParser();
                cv = configBackupCsv.FirstOrDefault();



                _backupServer.ConfigBackupEnabled = CObjectHelpers.ParseBool(cv.Enabled);
                if (_backupServer.ConfigBackupEnabled == true)
                {

                    _backupServer.ConfigBackupTarget = cv.Target;
                    _backupServer.ConfigBackupEncryption = CObjectHelpers.ParseBool(cv.EncryptionOptions);
                    _backupServer.ConfigBackupLastResult = cv.LastResult;
                    _backupServer.ConfigBackupRetentionPoints = CObjectHelpers.ParseInt(cv.RestorePointsToKeep);
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
            //TODO
            return "";
        }
        private void SetDbHostNameOption2()
        {
            //LoadCsvToMemory();
            using (CDataTypesParser parser = new())
            {
                List<CServerTypeInfos> csv = CGlobals.DtParser.ServerInfos;
                CServerTypeInfos bs = csv.Where(x => x.Id == CGlobals._backupServerId).FirstOrDefault();
                //var test = csv.Where(x => x.Id == CGlobals._backupServerId).FirstOrDefault();

                //log.Debug("backup server ID = " +CGlobals._backupServerId);
                CCsvParser config = new();


                if (_backupServer.Version == "" || _backupServer.Version == null || _backupServer.DbHostName == null || _backupServer.DbHostName == "")
                {
                    try
                    {
                        var records = config.BnrCsvParser();//.ToList();
                        if (records != null)
                        {
                            var r2 = records.ToList();
                            _backupServer.Version = r2[0].Version;
                            if (string.IsNullOrEmpty(_backupServer.DbHostName))
                            {
                                _backupServer.DbHostName = r2[0].SqlServer;

                            }
                            _backupServer.FixIds = CheckFixes(r2[0].Fixes);
                            _backupServer.HasFixes = _hasFixes;


                            if (_backupServer.DbType == CGlobals.PgTypeName)
                                _backupServer.DbHostName = r2[0].PgHost;
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
                    _backupServer.Name = bs.Name;
                    if (!bs.Name.Contains(_backupServer.DbHostName, StringComparison.OrdinalIgnoreCase)
                        && _backupServer.DbHostName != "localhost" && _backupServer.DbHostName != "LOCALHOST")
                    {
                        _backupServer.IsLocal = false;

                    }
                    else if (bs.Name.Contains(_backupServer.DbHostName, StringComparison.OrdinalIgnoreCase) || _backupServer.DbHostName == "localhost")
                    {
                        _backupServer.IsLocal = true;
                        _backupServer.DbHostName = "LocalHost";
                        _backupServer.DbCores = 0;
                        _backupServer.DbRAM = 0;
                    }

                }
                catch (Exception g)
                {
                    log.Error("Error processing SQL resource data");
                    log.Error("\t" + g.Message);
                    log.Debug("_backupServer = " + _backupServer.Version);
                    log.Debug("backupServer = " + bs.ApiVersion);
                }
                //try { _backupServer.Name = backupServer.Name; }
                //catch (NullReferenceException e)
                //{ log.Error("[VBR Config] failed to add backup server Name:\n\t" + e.Message); }

                //b.Version = veeamVersion;
                try { _backupServer.Cores = bs.Cores; }
                catch (NullReferenceException e)
                { log.Error("[VBR Config] failed to add backup server cores:\n\t" + e.Message); }
                try { _backupServer.RAM = bs.Ram; }
                catch (NullReferenceException e)
                { log.Error("[VBR Config] failed to add backup server RAM:\n\t" + e.Message); }
            }

        }
        private void LoadCsvToMemory()
        {
            string file = Path.Combine(CVariables.vbrDir, "localhost_vbrinfo.csv");
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
                        versionStr = dict[versionKey];
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
