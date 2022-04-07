// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.DB
{
    class CQueries
    {
        private CLogger log = MainWindow.log;

        private DataTable _sqlInfo;
        private string _sqlEdition;
        private string _sqlVersion;
        private DataTable _jobInfo;
        private DataTable _jobTypes;
        private string _vbrVersion;
        public DataTable SqlServerInfo { get { return _sqlInfo; }  }
        public string SqlEdition { get { return _sqlEdition; } }
        public string SqlVerion { get { return _sqlVersion; } }
        public DataTable JobInfo { get { return _jobInfo; } }
        public DataTable JobTypes { get { return _jobTypes; } }

        public string VbrVersion { get { return _vbrVersion; } }

        public CDbWorker dbWorker = new();
        public CQueries()
        {
            try
            {
                GetSqlServerInfo();
                GetSqlServerVersion();
                GetJobSummary();
                GetVbrVersion();
                GetBjobInfo();
                //CDbWorker db = new();
                //db.GetVbrJobsAll();
            }
            catch(Exception e)
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
                                value = String.Format("\"{0}\"", value);
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
            DataTable dt = Fetch(query);

            if(dt == null)
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

        private void GetSqlServerInfo()
        {
            log.Info("getting sql server info");
            string query = "select cpu_count, hyperthread_ratio, physical_memory_kb from sys.dm_os_sys_info";
            _sqlInfo = Fetch(query);
            log.Info("getting sql server info..done!");
        }

        private void GetBjobInfo()
        {
            string query = "select type,name,repository_id, included_size  from [Bjobs]";
            _jobInfo = Fetch(query);

            DumpDataToCsv(_jobInfo);
        }
        private void GetJobSummary()
        {
            log.Info("getting job summary info");
            string query = "select type from [Bjobs]";
            _jobTypes = Fetch(query);
            log.Info("getting job summary info..ok!");
        }
        private void GetVbrVersion()
        {
            log.Info("getting VBR version info");
            string query = "select * from [Version]";
            DataTable t = Fetch(query);
            if (t == null)
                _vbrVersion = "";
            else
            {
                string versionId = "";
                foreach (DataRow r in t.Rows)
                {
                    versionId = r["current_version"].ToString();
                }
                int.TryParse(versionId, out int i);
                CVersions cv = new(i);
                _vbrVersion = cv.VbrVersion;
            }

            log.Info("getting VBR version info..done!");
        }
        private DataTable Fetch(string query)
        {
            try
            {
                log.Info("fetching sql data..");
                //CDbWorker d = new();
                DataTable dt = dbWorker.ExecQuery(query);

                log.Info("fetching sql data..ok!");
                return dt;
            }
            catch(Exception e)
            {
                log.Error(e.Message);
                return null;
            }
        }
    }
    
}
