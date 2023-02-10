using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.DB;
using VeeamHealthCheck.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Reporting.TableDatas;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Collection.DB
{
    internal class CSqlExecutor
    {
        private readonly CLogger LOG = CGlobals.Logger;
        public CSqlExecutor()
        {

        }
        public void Run()
        {
            CGlobals.BACKUPSERVER = TrySetSqlInfo();
        }
        private BackupServer TrySetSqlInfo()
        {
            BackupServer b = new();
            try
            {
                LOG.Info("starting sql queries");
                DataTable dbServerInfo = new DataTable();
                CQueries cq = new();
                dbServerInfo = cq.SqlServerInfo;
                b.Edition = cq.SqlEdition;
                b.DbVersion = cq.SqlVerion;
                b = TryParseSqlResources(dbServerInfo, b);
                b.DbType = "MS SQL";

                LOG.Info("starting sql queries..done!");
            }
            catch (Exception e)
            {
                LOG.Error(e.Message);

            }
            return b;
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

    }

}
