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



        public DataTable ExecQuery(string query)
        {
            log.Info("executing sql query: " + query);
            try
            {
                using var connection = new SqlConnection(_cString); ;
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



    }
}
