// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Security.Principal;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    class CQueries
    {
        private readonly CLogger log = CGlobals.Logger;
        private readonly string cString;

        private DataTable sqlInfo;
        private string sqlEdition;
        private string sqlVersion;

        public DataTable SqlServerInfo { get { return this.sqlInfo; } }

        public string SqlEdition { get { return this.sqlEdition; } }

        public string SqlVerion { get { return this.sqlVersion; } }

        public CQueries()
        {
            CDbAccessor dbs = new CDbAccessor();
            this.cString = dbs.DbAccessorString();
            try
            {
                this.GetSqlServerInfo();
                this.GetSqlServerVersion();
                // Job collection via SQL [Bjobs] (GetJobSummary/GetBjobInfo) retired (1.3):
                // its _bjobs.csv output was proven byte-for-byte non-contributing to the report
                // (fields overwritten or unconsumed). Job data now comes solely from the
                // PowerShell-collected _Jobs.csv. SQL is kept only for server edition/version,
                // consumed by CSqlExecutor -> CGlobals.DBEdition/DBVERSION.
            }
            catch (Exception e)
            {
                // MessageBox.Show("SQL Processing Failed. Some information will be skipped.\n" +e.Message);
                this.log.Error(e.Message);
            }
        }

        public CQueries(bool testconnection)
        {
            this.GetSqlServerVersion();
        }

        private void GetSqlServerVersion()
        {
            this.log.Info("getting sql server version");

            // CDbWorker d = new();
            DataTable dt = this.FetchSqlServerVersion();

            if (dt == null)
            {
                this.sqlVersion = "undetermined";
                this.sqlEdition = "undetermined";
            }
            else
            {
                try
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string s = r[0].ToString();
                        string[] s2 = s.Split();

                        this.sqlVersion = s2[0] + " " + s2[1] + " " + s2[2] + " " + s2[3];

                        // _sqlEdition = s2[24] + " " + s2[25]; //tofix
                        if (s.Contains("express edition", StringComparison.CurrentCultureIgnoreCase))
                        {
                            this.sqlEdition = "Express";
                        }

                        if (s.Contains("developer edition", StringComparison.CurrentCultureIgnoreCase))
                        {
                            this.sqlEdition = "Developer";
                        }

                        if (s.Contains("Enterprise edition", StringComparison.CurrentCultureIgnoreCase))
                        {
                            this.sqlEdition = "Enterprise";
                        }

                        if (s.Contains("Standard edition", StringComparison.CurrentCultureIgnoreCase))
                        {
                            this.sqlEdition = "Standard";
                        }
                    }
                }
                catch (Exception e) { this.log.Error(e.Message); }
            }

            this.log.Info("getting sql server version..done!");
        }

        private DataTable FetchSqlServerVersion()
        {
            try
            {
                using var connection = new SqlConnection(this.cString); ;
                using SqlCommand command = new SqlCommand("Select @@version", connection);

                connection.Open();
                DataTable t = new();

                t.Load(command.ExecuteReader());

                connection.Close();
                this.log.Info("executing sql query..done!");
                return t;
            }
            catch (Exception e)
            {
                this.log.Error(e.Message);
                return null;
            }
        }

        private void GetSqlServerInfo()
        {
            this.log.Info("getting sql server info");
            this.sqlInfo = this.FetchSqlServerInfo();
            this.log.Info("getting sql server info..done!");
        }

        private DataTable FetchSqlServerInfo()
        {
            try
            {
                using var connection = new SqlConnection(this.cString);
                string query = "select cpu_count, hyperthread_ratio, physical_memory_kb from sys.dm_os_sys_info";

                using SqlCommand command = new SqlCommand(query, connection);

                connection.Open();
                DataTable t = new();

                t.Load(command.ExecuteReader());

                connection.Close();
                this.log.Info("executing sql query..done!");
                return t;
            }
            catch (Exception e)
            {
                this.log.Error(e.Message);
                return null;
            }
        }

    }
}
