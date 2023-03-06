// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Data;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    internal class CSqlExecutor
    {
        private readonly CLogger LOG = CGlobals.Logger;
        public CSqlExecutor()
        {

        }
        public void Run()
        {
            //CGlobals.BACKUPSERVER = 
            TrySetSqlInfo();
        }
        private void TrySetSqlInfo()
        {
            BackupServer b = new();
            try
            {
                LOG.Info("starting sql queries");
                DataTable dbServerInfo = new DataTable();
                CQueries cq = new();
                dbServerInfo = cq.SqlServerInfo;
                //b.Edition = cq.SqlEdition;
                //b.DbVersion = cq.SqlVerion;
                //b = TryParseSqlResources(dbServerInfo, b);
                //b.DbType = CGlobals.SqlTypeName;

                CGlobals.DBEdition = cq.SqlEdition;
                CGlobals.DBVERSION = cq.SqlVerion;
                CGlobals.DBTYPE = CGlobals.SqlTypeName;
                TryParseSqlResources(dbServerInfo);
                LOG.Info("starting sql queries..done!");
            }
            catch (Exception e)
            {
                LOG.Error(e.Message);

            }
            //return b;
        }
        private static void TryParseSqlResources(DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                string cpu = row["cpu_count"].ToString();
                string hyperthread = row["hyperthread_ratio"].ToString();
                string memory = row["physical_memory_kb"].ToString();
                int.TryParse(cpu, out int c);
                int.TryParse(hyperthread, out int h);
                int.TryParse(memory, out int mem);

                //b.DbCores = c;//(c * h).ToString();
                //b.DbRAM = ((mem / 1024 / 1024) + 1);

                CGlobals.DBCORES = c;
                CGlobals.DBRAM = mem / 1024 / 1024 + 1;

            }

            //return b;
        }

    }

}
