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

        public string DbString
        {
            get { return _databaseName; }
        }

        public string HostString
        {
            get { return _hostInstanceString; }
        }


        public CRegReader()
        {

        }
        public void GetDbInfo()
        {

            try
            {
                if(CGlobals.REMOTEEXEC)
                    GetVbrElevenDbInfoRemote();
                else
                    GetVbrElevenDbInfo();
            }
            catch (Exception e2)
            {
                log.Error(logStart + "Failed to get v11 DB info from Registry. Trying v12 registry hives:\t" + e2.Message);
            }
            if (string.IsNullOrEmpty(_databaseName))
            {
                try
                {
                    if (CGlobals.REMOTEEXEC)
                    {
                        GetRemoteVbrTwelveDbInfo();
                    }
                    else
                    {
                        GetVbrTwelveDbInfo();
                    }
                }
                catch (Exception e3)
                {
                    log.Error(logStart + "Failed to get v12 DB info from Registry:\t" + e3.Message);
                }


            }
        }

        public string GetVbrVersionFilePath()
        {
            string consoleInstallPath = @"C:\Program Files\Veeam\Backup and Replication\Console\Veeam.Backup.Core.dll";
            var coreVersion = FileVersionInfo.GetVersionInfo (consoleInstallPath).FileVersion;
            if (!string.IsNullOrEmpty(coreVersion))
            {
                CGlobals.VBRFULLVERSION = coreVersion;
                ParseVbrMajorVersion(CGlobals.VBRFULLVERSION);
                return coreVersion;
            }
            
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Mount Service"))
            {
                var keyValue = key.GetValue("InstallationPath");
                string path = "";
                if (keyValue != null)
                {
                    path = keyValue.ToString();
                }
                //string path = key.GetValue("CorePath").ToString();
                //FileInfo dllInfo = new FileInfo(path + "\\Packages\\VeeamDeploymentDll.dll");
                var version = FileVersionInfo.GetVersionInfo(path + "\\Veeam.Backup.Core.dll");
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
        private void GetVbrElevenDbInfoRemote()
        {
            using (RegistryKey key =
                RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                SetSqlInfo(key);
               
            }
        }
        private void GetVbrElevenDbInfo()
        {
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                SetSqlInfo(key);

            }
        }
        private void SetSqlInfo(RegistryKey key)
        {
            if (key != null)
            {
                var value = key.GetValue("SqlInstanceName");
                string instance = "";
                if (value != null)
                {
                    instance = value.ToString();
                }
                //var instance = key.GetValue("SqlInstanceName").ToString();
                var hostValue = key.GetValue("SqlServerName");
                string host = "";
                if(hostValue != null)
                {
                    host = hostValue.ToString();
                }

                //var host = key.GetValue("SqlServerName").ToString();
                var db = key.GetValue("SqlDatabaseName");
                string database = "";
                if (db != null)
                    database = db.ToString();
                //var database =key.GetValue("SqlDatabaseName").ToString();

                var userName = key.GetValue("SqlLogin");
                if (userName != null)
                    _user = userName.ToString();
                //_user = key.GetValue("SqlLogin").ToString();

                var password = key.GetValue("SqlSecuredPassword");
                if (password != null)
                    _passString = password.ToString();
                //_passString = key.GetValue("SqlSecuredPassword").ToString();

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
        private void GetRemoteVbrTwelveDbInfo()
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
        private void GetVbrTwelveDbInfo()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations"))
            {
                string host = "";

                if (key != null)
                {
                    var dbType = key.GetValue("SqlActiveConfiguration").ToString();
                    log.Info(logStart + "DB Type = " + dbType);
                    if (dbType == "MsSql")
                    {

                        CGlobals.DBTYPE = CGlobals.SqlTypeName;

                        using (RegistryKey sqlKey = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
                        //using (RegistryKey sqlKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
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

        public Dictionary<string, Object> DefaultVbrKeysRemote()
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
        public Dictionary<string, Object> DefaultVbrKeys()
        {
            Dictionary<string, Object> keys = new();
            using (RegistryKey key =
                Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                if (key != null)
                {
                    string[] values = key.GetValueNames();
                    foreach (var name in values)
                    {
                        keys.Add(name, key.GetValue(name));
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
