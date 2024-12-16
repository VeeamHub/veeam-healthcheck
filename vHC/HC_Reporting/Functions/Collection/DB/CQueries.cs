// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Security.Principal;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    class CQueries
    {
        private CLogger log = CGlobals.Logger;
        private readonly string _cString;

        private DataTable _sqlInfo;
        private string _sqlEdition;
        private string _sqlVersion;
        private DataTable _jobInfo;
        private DataTable _jobTypes;
        public DataTable SqlServerInfo { get { return _sqlInfo; } }
        public string SqlEdition { get { return _sqlEdition; } }
        public string SqlVerion { get { return _sqlVersion; } }
        public DataTable JobInfo { get { return _jobInfo; } }
        public DataTable JobTypes { get { return _jobTypes; } }


        public CQueries()
        {
            CDbAccessor dbs = new CDbAccessor();
            _cString = dbs.DbAccessorString();
            try
            {
                GetSqlServerInfo();
                GetSqlServerVersion();
                GetJobSummary();
                //GetVbrVersion();
                GetBjobInfo();
                //CDbWorker db = new();
                //db.GetVbrJobsAll();
            }
            catch (Exception e)
            {
                //MessageBox.Show("SQL Processing Failed. Some information will be skipped.\n" +e.Message);
                log.Error(e.Message);
            }

        }
        public CQueries(bool testconnection)
        {
            GetSqlServerVersion();
        }
        private void DumpDataToCsv(DataTable data)
        {
            using (StreamWriter sw = new StreamWriter(CVariables.vbrDir + @"\localhost_bjobs.csv", false))
            {
                //headers    
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    sw.Write(data.Columns[i]);
                    if (i < data.Columns.Count - 1)
                    {
                        sw.Write(",");
                    }
                }
                sw.Write(sw.NewLine);
                foreach (DataRow dr in data.Rows)
                {
                    for (int i = 0; i < data.Columns.Count; i++)
                    {
                        if (!Convert.IsDBNull(dr[i]))
                        {
                            string value = dr[i].ToString();
                            if (value.Contains(','))
                            {
                                value = string.Format("\"{0}\"", value);
                                sw.Write(value);
                            }
                            else
                            {
                                sw.Write(dr[i].ToString());
                            }
                        }
                        if (i < data.Columns.Count - 1)
                        {
                            sw.Write(",");
                        }
                    }
                    sw.Write(sw.NewLine);
                }
            }

            // sw.Close();


            //StringBuilder sb = new StringBuilder();

            //IEnumerable<string> columnNames = data.Columns.Cast<DataColumn>().
            //                                  Select(column => column.ColumnName);
            //sb.AppendLine(string.Join(",", columnNames));

            //foreach (DataRow row in data.Rows)
            //{
            //    IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
            //    sb.AppendLine(string.Join(",", fields));
            //}

            //File.WriteAllText("test.csv", sb.ToString());
        }
        private void GetSqlServerVersion()
        {
            log.Info("getting sql server version");
            string query = "Select @@version";

            //CDbWorker d = new();
            DataTable dt = FetchSqlServerVersion();

            if (dt == null)
            {
                _sqlVersion = "undetermined";
                _sqlEdition = "undetermined";
            }
            else
            {
                try
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string s = r[0].ToString();
                        string[] s2 = s.Split();

                        _sqlVersion = s2[0] + " " + s2[1] + " " + s2[2] + " " + s2[3];
                        //_sqlEdition = s2[24] + " " + s2[25]; //tofix

                        if (s.Contains("express edition", StringComparison.CurrentCultureIgnoreCase))
                            _sqlEdition = "Express";
                        if (s.Contains("developer edition", StringComparison.CurrentCultureIgnoreCase))
                            _sqlEdition = "Developer";
                        if (s.Contains("Enterprise edition", StringComparison.CurrentCultureIgnoreCase))
                            _sqlEdition = "Enterprise";
                        if (s.Contains("Standard edition", StringComparison.CurrentCultureIgnoreCase))
                            _sqlEdition = "Standard";
                    }
                }
                catch (Exception e) { log.Error(e.Message); }
            }
            log.Info("getting sql server version..done!");

        }
        private DataTable FetchSqlServerVersion()
        {
            try
            {
                using var connection = new SqlConnection(_cString); ;
                using SqlCommand command = new SqlCommand("Select @@version", connection);


                connection.Open();
                DataTable t = new();

                t.Load(command.ExecuteReader());

                connection.Close();
                log.Info("executing sql query..done!");
                return t;
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }

        private void GetSqlServerInfo()
        {
            log.Info("getting sql server info");
            _sqlInfo = FetchSqlServerInfo();
            log.Info("getting sql server info..done!");
        }
        private DataTable FetchSqlServerInfo()
        {
            try
            {
                using var connection = new SqlConnection(_cString);
                string query = "select cpu_count, hyperthread_ratio, physical_memory_kb from sys.dm_os_sys_info";

                using SqlCommand command = new SqlCommand(query, connection);


                connection.Open();
                DataTable t = new();

                t.Load(command.ExecuteReader());

                connection.Close();
                log.Info("executing sql query..done!");
                return t;
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }

        private void GetBjobInfo()
        {
            _jobInfo = FetchBJobInfo();

            try { DumpDataToCsv(_jobInfo); }
            catch(Exception e){ log.Error("Failed to dump bjobs to csv.."); log.Error(e.Message); }
        }
        private DataTable FetchBJobInfo()
        {
            try
            {
                using var connection = new SqlConnection(_cString);
                string query = "select type,name,repository_id, included_size  from [Bjobs]";

                using SqlCommand command = new SqlCommand(query, connection);


                connection.Open();
                DataTable t = new();

                t.Load(command.ExecuteReader());

                connection.Close();
                log.Info("executing sql query..done!");
                return t;
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }

        private void GetJobSummary()
        {
            log.Info("getting job summary info");
            _jobTypes = FetchJobSummaryInfo();
            log.Info("getting job summary info..ok!");
        }
        private DataTable FetchJobSummaryInfo()
        {
            try
            {
                using var connection = new SqlConnection(_cString);
                string query = "select type from [Bjobs]";

                using SqlCommand command = new SqlCommand(query, connection);


                connection.Open();
                DataTable t = new();

                t.Load(command.ExecuteReader());

                connection.Close();
                log.Info("executing sql query..done!");
                return t;
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }


    }

}
