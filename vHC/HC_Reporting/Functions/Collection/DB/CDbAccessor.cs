﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Data.SqlClient;
using System.Security.Principal;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    class CDbAccessor
    {
        private string _connectionString;

        public string DbAccessorString()
        {
            var b = StringBuilder();
            _connectionString = b.ConnectionString;
            return b.ConnectionString;
        }
        private SqlConnectionStringBuilder StringBuilder()
        {
            SqlConnectionStringBuilder builder = SimpleConnectionBuilder();
            return builder;
        }
        private SqlConnectionStringBuilder SimpleConnectionBuilder()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(GetConnectionString());
            builder.Remove("Initial Catalog");
            //builder["Server"] = server;
            CRegReader reg = new CRegReader();
            reg.GetDbInfo();
            string host = reg.HostString;
            string db = reg.DbString;
            if (host == null || db == null)
            {

                // why am i asking for interaction?

                //Console.WriteLine("Please enter SQL Host Name & Instance (i.e. vbr-server\\sqlserver2016):");
                //host = Console.ReadLine();
                //Console.WriteLine("Please enter DB name:");
                //db = Console.ReadLine();
            }
            builder["Server"] = host;
            //CGlobals.DBHOSTNAME = host;
            builder["Database"] = db;

            if (TestConnection())
                return builder;
            else
            {
                var cred = WindowsIdentity.GetCurrent();
                builder.UserID = cred.User.ToString();
                builder.Password = cred.Token.ToString();
                return builder;
            }
        }

        private bool TestConnection()
        {
            try
            {
                SqlConnection sqlConnection = new SqlConnection(_connectionString);
                using var connection = sqlConnection;
                using SqlCommand command = new SqlCommand("select @@version", connection);
                connection.Open();
                return true;
            }
            catch (Exception e)
            {
                CGlobals.Logger.Warning("Sql Test Connection Failed: " + e.Message);
                return false;
            }

        }


        private static string GetConnectionString()
        {
            return "Server=(local);Integrated Security=SSPI;";
        }
    }
}
