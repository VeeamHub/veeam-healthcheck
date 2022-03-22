// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Microsoft.Win32;
using System.Windows;

namespace VeeamHealthCheck.DB
{
    public class CRegReader
    {
        private static string _databaseName;
        private static string _hostInstanceString;
        private static string _host;
        private static string _user;
        private static string _passString;

        public string User { get { return _user; } }
        public string PassString { get { return _passString; } }
        public string DbString
        {
            get { return _databaseName; }
        }

        public string HostString
        {
            get { return _hostInstanceString; }
        }

        public string Host { get { return _host; } }

        public CRegReader()
        {
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                if (key != null)
                {
                    var instance = key.GetValue("SqlInstanceName").ToString();
                    var host = key.GetValue("SqlServerName").ToString();
                    var database =
                        key.GetValue("SqlDatabaseName")
                            .ToString();
                    _user = key.GetValue("SqlLogin").ToString();
                    _passString = key.GetValue("SqlSecuredPassword").ToString();
                    if (!string.IsNullOrEmpty(host))
                    {
                        //if (!string.IsNullOrEmpty(instance))
                        //{
                        if (!string.IsNullOrEmpty(database))
                        {
                            _databaseName = database;
                            _host = host;
                            _hostInstanceString = host + "\\" + instance;
                        }
                        //}
                    }
                }
            }

            //catch (Exception ex)
            //{
            //    //Logger.Log.Warning("Something went awry with reading registry. Let's try manual DB connection:", false);
            //    string msg = "Registry parsing failed. DB collections will not be possible. Does this server have Veeam Backup & Replication installed?";
            //    MessageBox.Show(msg + "\n" + ex.Message);
            //}
        }

        public string DefaultLogDir()
        {
            var logDir = "C:\\ProgramData\\Veeam\\Backup";
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                string dir = null;

                if (key != null)
                {
                    //dir = key.GetValue("LogDirectory").ToString();
                    string[] v = key.GetValueNames();
                    foreach (var x in v)
                    {
                        if (x == "LogDirectory")
                        {
                            dir = key.GetValue("LogDirectory").ToString();
                            break;
                        }
                        else
                        {
                            dir = logDir;
                        }
                    }
                    return dir;
                }
                else
                {
                    logDir = dir;
                    return logDir;
                }
            }
        }
    }
}
