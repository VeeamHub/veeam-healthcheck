// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    class CDbWorker
    {
        private readonly string _cString;
        private CLogger log = CGlobals.Logger;
        public CDbWorker()
        {
            log.Info("init db worker");
            CDbAccessor dbs = new CDbAccessor();
            _cString = dbs.DbAccessorString();
        }

        public void DoDbWork()
        {

            using (var connection = new SqlConnection(_cString))
            {
                string query = string.Format("Use {0} SELECT {1} from {2}"); //TODO: Find DB info and form string ahead of time
            }
        }

        public DataTable ExecQuery(string query)
        {
            log.Info("executing sql query: " + query);
            try
            {
                SqlConnection sqlConnection = new SqlConnection(_cString);
                using var connection = sqlConnection;
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
                // MessageBox.Show(e.Message);
                var creds = WindowsIdentity.GetCurrent();
                return null;
            }
        }


        public string ExecSimpleQuery(string query)
        {
            log.Info("executing simple query: " + query);
            using (var connection = new SqlConnection(_cString))
            {
                using (var command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    DataTable t = new();
                    SqlDataReader s = command.ExecuteReader();

                    log.Info("executing simple query..done!");
                    return s.GetString(1);
                }
            }
        }
        public void GetVbrJobsAll()
        {
            using (var connection = new SqlConnection(_cString))
            {
                string query = string.Format("USE VEEAMBACKUP SELECT * FROM [BJOBS]");

                using (var command = new SqlCommand(query, connection))
                {
                    connection.Open();

                    DataTable t = new DataTable();
                    //SqlDataReader _reader = command.ExecuteReader();

                    t.Load(command.ExecuteReader());

                    using (StreamWriter sw = new StreamWriter("output.txt", append: true))
                    {
                        StringBuilder sb = new StringBuilder();

                        IEnumerable<string> columnNames = t.Columns.Cast<DataColumn>().
                                                          Select(column => column.ColumnName);
                        sb.AppendLine(string.Join(",", columnNames));

                        foreach (DataRow row in t.Rows)
                        {
                            IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                            sb.AppendLine(string.Join(",", fields));
                        }

                        File.WriteAllText("test.csv", sb.ToString());

                        //sw.WriteLine(d.ToString());
                        // }
                    }

                }
            }
        }
        public void GetJobs()
        {



            /* DataTable temp = new DataTable();
 adapter = new MySqlDataAdapter(command);
 adapter.Fill(temp);

 foreach(DataColumn column in  temp.Columns) 
 {
     foreach(DataRow row in temp.Rows)
     {
          Console.WriteLine(row[column]);
     }
 }
             */
        }
    }
}
