// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Microsoft.Win32;
using System.Windows;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using System.Windows.Input;
using Namotion.Reflection;
using System.IO;
using System.Diagnostics;

namespace VeeamHealthCheck.DB
{
    public class CRegReader
    {
        private static string _databaseName;
        private static string _hostInstanceString;
        private static string _host;
        private static string _user;
        private static string _passString;
        private static string _dbType;
        private static string _dbLocal;

        private string logStart = "[RegistryReader]\t";

        private CLogger log = CGlobals.Logger;

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


            //catch (Exception ex)
            //{
            //    //Logger.Log.Warning("Something went awry with reading registry. Let's try manual DB connection:", false);
            //    string msg = "Registry parsing failed. DB collections will not be possible. Does this server have Veeam Backup & Replication installed?";
            //    MessageBox.Show(msg + "\n" + ex.Message);
            //}
        }
        public void GetDbInfo()
        {

            try
            {
                GetVbrElevenDbInfo();
            }
            catch (Exception e2)
            {
                log.Error(logStart + "Failed to get v11 DB info from Registry. Trying v12 registry hives");
            }
            if (String.IsNullOrEmpty(_databaseName))
            {
                try { GetVbrTwelveDbInfo(); }
                catch (Exception e3)
                {
                    log.Error(logStart + "Failed to get v12 DB info from Registry.");
                }
            }

        }
        public void GetVbrVersionFilePath()
        {
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                string path = key.GetValue("CorePath").ToString();
                //FileInfo dllInfo = new FileInfo(path + "\\Packages\\VeeamDeploymentDll.dll");
                var version = FileVersionInfo.GetVersionInfo(path + "\\Packages\\VeeamDeploymentDll.dll");
                CGlobals.VBRFULLVERSION = version.FileVersion;
                
            }
        }
        private void GetVbrElevenDbInfo()
        {
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                SetSqlInfo(key);
                //if (key != null)
                //{
                //    var instance = key.GetValue("SqlInstanceName").ToString();
                //    var host = key.GetValue("SqlServerName").ToString();
                //    var database =
                //        key.GetValue("SqlDatabaseName")
                //            .ToString();
                //    _user = key.GetValue("SqlLogin").ToString();
                //    _passString = key.GetValue("SqlSecuredPassword").ToString();
                //    if (!string.IsNullOrEmpty(host))
                //    {
                //        if (!string.IsNullOrEmpty(database))
                //        {
                //            _databaseName = database;
                //            _host = host;
                //            _hostInstanceString = host + "\\" + instance;
                //        }
                //    }
                //}
            }
        }
        private void SetSqlInfo(RegistryKey key)
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
                    if (!string.IsNullOrEmpty(database))
                    {
                        _databaseName = database;
                        _host = host;
                        _hostInstanceString = host + "\\" + instance;
                    }
                }
            }
        }
        private string SetDbServerName(string input)
        {
            if (!String.IsNullOrEmpty(input))
            {
                return null; //placeholder
            }
            return null; //placeholder
        }
        private void GetVbrTwelveDbInfo()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations"))
            {
                string host = "";

                if (key != null)
                {
                    var dbType = key.GetValue("SqlActiveConfiguration").ToString();
                    if (dbType == "MsSql")
                    {
                        _dbType = "MS SQL";

                        using (RegistryKey sqlKey = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
                        {
                            SetSqlInfo(sqlKey);

                            host = sqlKey.GetValue("SqlServerName").ToString();
                            if (host == "localhost")
                                CGlobals.ISDBLOCAL = "True";
                            CGlobals.DBHOSTNAME = host;
                            //CGlobals.DBINSTANCE = key.GetValue("SqlInstanceName").ToString();

                            // SqlInstanceName
                            // SqlDatabaseName
                        }
                    }
                    else if (dbType == "PostgreSql")
                    {
                        _dbType = "PostgreSQL";
                        CGlobals.DBTYPE = "PostgreSQL";
                        using (RegistryKey pgKey = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\PostgreSql"))
                        {
                            host = pgKey.GetValue("SqlHostName").ToString();
                            if (host == "localhost")
                                CGlobals.ISDBLOCAL = "True";
                            CGlobals.DBHOSTNAME = host;

                        }
                    }

                    //var database =
                    //    key.GetValue("SqlDatabaseName")
                    //        .ToString();
                    //_user = key.GetValue("SqlLogin").ToString();
                    //_passString = key.GetValue("SqlSecuredPassword").ToString();
                    //if (!string.IsNullOrEmpty(host))
                    //{
                    //    if (!string.IsNullOrEmpty(database))
                    //    {
                    //        _databaseName = database;
                    //        _host = host;
                    //        _hostInstanceString = host + "\\" + instance;
                    //    }
                    //}
                }
            }
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
