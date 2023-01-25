using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.DataTypes;
using VeeamHealthCheck.DB;
using VeeamHealthCheck.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Reporting.TableDatas;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Common;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Reporting.Html
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
            TrySetSqlInfo();
            SetDbHostNameOption2();
        }
        private void ScrubElements()
        {
            _backupServer.Name = Scrub(_backupServer.Name);
            _backupServer.ConfigBackupTarget = Scrub(_backupServer.ConfigBackupTarget);
            _backupServer.DbHostName = Scrub(_backupServer.DbHostName);
        }
        private void SetConfigBackupSettings()
        {
            CCsvParser config = new();
            List<CConfigBackupCsv> cv = config.ConfigBackupCsvParser().ToList();
            _backupServer.ConfigBackupEnabled = CObjectHelpers.ParseBool(cv[0].Enabled);
            if (_backupServer.ConfigBackupEnabled == true)
            {

                _backupServer.ConfigBackupTarget = cv[0].Target;
                _backupServer.ConfigBackupEncryption = CObjectHelpers.ParseBool(cv[0].EncryptionOptions);
                _backupServer.ConfigBackupLastResult = cv[0].LastResult;
                _backupServer.ConfigBackupRetentionPoints =  CObjectHelpers.ParseInt(cv[0].RestorePointsToKeep);
            }

        }

        private string  CheckFixes(string fixes)
        {

            return "";
        }
        private void SetDbHostNameOption2()
        {
            using (CDataTypesParser parser = new())
            {
                List<CServerTypeInfos> csv = parser.ServerInfo().ToList();
                CServerTypeInfos backupServer = csv.Find(x => (x.Id == CGlobals._backupServerId));
                CCsvParser config = new();


                if (_backupServer.Version == "" || _backupServer.Version == null || _backupServer.DbHostName == null || _backupServer.DbHostName == "")
                {
                    try
                    {
                        var records = config.BnrCsvParser().ToList();
                        _backupServer.Version = records[0].Version;
                        _backupServer.DbHostName = records[0].SqlServer;
                        _backupServer.FixIds =  CheckFixes(records[0].Fixes);
                        _backupServer.HasFixes = _hasFixes;

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
                    else if (backupServer.Name.Contains(_backupServer.DbHostName, StringComparison.OrdinalIgnoreCase))
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
        private void SetVBRVersion()
        {
            if (_backupServer.Version == "")
            {

            }
        }
        private void SetProxyRoles()
        {

        }
        private void TrySetSqlInfo()
        {
            try
            {
                log.Info("starting sql queries");
                DataTable dbServerInfo = new DataTable();
                CQueries cq = new();
                dbServerInfo = cq.SqlServerInfo;
                _backupServer.Edition = cq.SqlEdition;
                _backupServer.DbVersion = cq.SqlVerion;
                _backupServer = TryParseSqlResources(dbServerInfo, _backupServer);
                _backupServer.DbType = "MS SQL";

                log.Info("starting sql queries..done!");
            }
            catch (Exception e)
            {
                log.Error(e.Message);

            }
        }
        private static BackupServer TryParseSqlResources(DataTable table, BackupServer b)
        {
            foreach (DataRow row in table.Rows)
            {
                string cpu = row["cpu_count"].ToString();
                string hyperthread = row["hyperthread_ratio"].ToString();
                string memory = row["physical_memory_kb"].ToString();
                int.TryParse(cpu, out int c);
                int.TryParse(hyperthread, out int h);
                int.TryParse(memory, out int mem);

                b.DbCores = c;//(c * h).ToString();
                b.DbRAM = ((mem / 1024 / 1024) + 1);

            }

            return b;
        }
        private string Scrub(string item)
        {
            CScrubHandler scrubber = CGlobals.Scrubber;
            return scrubber.ScrubItem(item);
        }
    }
}
