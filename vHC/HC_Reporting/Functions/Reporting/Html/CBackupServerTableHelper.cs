﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
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
            _backupServer = new();
            SetBackupServerWithDbInfo();
            _scrub = scrub;
        }
        public BackupServer SetBackupServerData()
        {
            SetElements();
            if (_scrub)
                ScrubElements();
            return _backupServer;
        }
        private void SetElements()
        {
            SetConfigBackupSettings();
            SetDbHostNameOption2();

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
            using (CDataTypesParser parser = new())
            {
                List<CServerTypeInfos> csv = parser.ServerInfo().ToList();
                CServerTypeInfos backupServer = csv.Find(x => x.Id == CGlobals._backupServerId);
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
                        log.Error(f.Message);
                    }
                }
                try
                {
                    _backupServer.Name = backupServer.Name;
                    if (!backupServer.Name.Contains(_backupServer.DbHostName, StringComparison.OrdinalIgnoreCase)
                        && _backupServer.DbHostName != "localhost" && _backupServer.DbHostName != "LOCALHOST")
                    {
                        _backupServer.IsLocal = false;

                    }
                    else if (backupServer.Name.Contains(_backupServer.DbHostName, StringComparison.OrdinalIgnoreCase) || _backupServer.DbHostName == "localhost")
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
                }
                //try { _backupServer.Name = backupServer.Name; }
                //catch (NullReferenceException e)
                //{ log.Error("[VBR Config] failed to add backup server Name:\n\t" + e.Message); }

                //b.Version = veeamVersion;
                try { _backupServer.Cores = backupServer.Cores; }
                catch (NullReferenceException e)
                { log.Error("[VBR Config] failed to add backup server cores:\n\t" + e.Message); }
                try { _backupServer.RAM = backupServer.Ram; }
                catch (NullReferenceException e)
                { log.Error("[VBR Config] failed to add backup server RAM:\n\t" + e.Message); }
            }

        }

    }
}
