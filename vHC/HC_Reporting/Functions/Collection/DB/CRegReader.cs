// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Windows.Documents;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.DB
{
    public class CRegReader
    {
        private static string _databaseName;
        private static string _hostInstanceString;
        private static string _host;
        private static string _user;
        private static string _passString;

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
            if (string.IsNullOrEmpty(_databaseName))
            {
                try { GetVbrTwelveDbInfo(); }
                catch (Exception e3)
                {
                    log.Error(logStart + "Failed to get v12 DB info from Registry.");
                }
            }

        }
        public string GetVbrVersionFilePath()
        {
            using (RegistryKey key =
                RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                var keyValue = key.GetValue("CorePath");
                string path = "";
                if (keyValue != null)
                {
                    path = keyValue.ToString();
                }
                //string path = key.GetValue("CorePath").ToString();
                //FileInfo dllInfo = new FileInfo(path + "\\Packages\\VeeamDeploymentDll.dll");
                var version = FileVersionInfo.GetVersionInfo(path + "\\Packages\\VeeamDeploymentDll.dll");
                CGlobals.VBRFULLVERSION = version.FileVersion;
                ParseVbrMajorVersion(CGlobals.VBRFULLVERSION);
                return CGlobals.VBRFULLVERSION;

            }
        }
        private void ParseVbrMajorVersion(string fullVersion)
        {
            try
            {
                string[] segments = fullVersion.Split(".");
                int.TryParse(segments[0], out int mVersion);

                switch (mVersion)
                {
                    case 10:
                        SetMajorVersion(10);
                        break;
                    case 11:
                        SetMajorVersion(11);
                        break;
                    case 12:
                        SetMajorVersion(12);
                        break;
                }
            }
            catch (Exception e)
            {
                log.Error(logStart + "Failed to parse VBR Major Version:\n\t" + e.Message);
            }


        }
        private void SetMajorVersion(int version)
        {
            CGlobals.VBRMAJORVERSION = version;
        }
        private void GetVbrElevenDbInfo()
        {
            using (RegistryKey key =
                RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
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
            if (!string.IsNullOrEmpty(input))
            {
                return null; //placeholder
            }
            return null; //placeholder
        }
        private void GetVbrTwelveDbInfo()
        {
            using (RegistryKey key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations"))
            {
                string host = "";

                if (key != null)
                {
                    var dbType = key.GetValue("SqlActiveConfiguration").ToString();
                    log.Info(logStart + "DB Type = " + dbType);
                    if (dbType == "MsSql")
                    {

                        CGlobals.DBTYPE = CGlobals.SqlTypeName;

                        //using (RegistryKey sqlKey = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
                        using (RegistryKey sqlKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
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
                        CGlobals.DBTYPE = CGlobals.PgTypeName;
                        using (RegistryKey pgKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\PostgreSql"))
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
        public Dictionary<string, Object> DefaultVbrKeys()
        {
            Dictionary<string, Object> keys = new();
            using (RegistryKey key =
                RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                if (key != null)
                {
                    string[] values = key.GetValueNames();
                    foreach (var name in values)
                    {
                        keys.Add(name, key.GetValue(name)) ;
                        //var vTest = key.GetValue(name);
                    }

                }
            }


            return keys;
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
