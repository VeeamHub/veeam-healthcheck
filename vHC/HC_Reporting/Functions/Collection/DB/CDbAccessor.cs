// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Data.SqlClient;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    internal class CDbAccessor
    {
        // Injection seam: tests supply a pre-configured CRegReader; production code
        // leaves this null so SimpleConnectionBuilder() creates a real one.
        // This is the lightest possible seam — no ctor overload required.
        internal CRegReader RegReader { get; set; } = null;

        // Warning sink: defaults to CGlobals.Logger.Warning so production behaviour
        // is unchanged. Tests can substitute a recording lambda to assert ISC-16/17
        // without touching the static logger.
        internal Action<string> WarningSink { get; set; } = msg => CGlobals.Logger.Warning(msg);

        public string DbAccessorString()
        {
            return this.StringBuilder().ConnectionString;
        }

        internal SqlConnectionStringBuilder StringBuilder()
        {
            SqlConnectionStringBuilder builder = this.SimpleConnectionBuilder();
            return builder;
        }

        internal SqlConnectionStringBuilder SimpleConnectionBuilder()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(GetConnectionString());
            builder.Remove("Initial Catalog");

            // Use injected CRegReader if provided (test seam), otherwise create a real one.
            CRegReader reg = this.RegReader ?? new CRegReader();
            if (this.RegReader == null)
            {
                reg.GetDbInfo();
            }
            string host = reg.HostString;
            string db = reg.DbString;
            if (host == null || db == null)
            {
                // why am i asking for interaction?

                // Console.WriteLine("Please enter SQL Host Name & Instance (i.e. vbr-server\\sqlserver2016):");
                // host = Console.ReadLine();
                // Console.WriteLine("Please enter DB name:");
                // db = Console.ReadLine();
            }

            builder["Server"] = host;

            // CGlobals.DBHOSTNAME = host;
            builder["Database"] = db;

            // Pre-flight test against the just-built connection string. If it fails we
            // still return the builder so CQueries can log per-query failures inline;
            // the warning surfaces the most common cause up front so it's obvious in the
            // log: the user running vHC lacks db_datareader on the Veeam config DB.
            if (!this.TestConnection(builder.ConnectionString))
            {
                this.WarningSink(
                    "SQL connection pre-flight failed. If subsequent queries log 'Login failed', " +
                    "the user running vHC needs db_datareader on the Veeam config database, " +
                    "or vHC should be run under the VBR service account.");
            }
            return builder;
        }

        internal bool TestConnection(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand("select @@version", connection);
                connection.Open();
                return true;
            }
            catch (Exception e)
            {
                this.WarningSink("Sql Test Connection Failed: " + e.Message);
                return false;
            }
        }

        private static string GetConnectionString()
        {
            // Connect Timeout bounds how long Open() blocks before failing. Without it the
            // default is 15s, but we set it explicitly so a wrong/unreachable SQL host fails
            // loudly in the log rather than appearing to "hang" during collection.
            return "Server=(local);Integrated Security=SSPI;Connect Timeout=15;";
        }
    }
}
