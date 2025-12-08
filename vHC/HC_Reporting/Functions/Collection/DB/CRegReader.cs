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
        private static string databaseName;
        private static string hostInstanceString;
        private static string host;
        private static string user;
        private static string passString;

        private readonly string logStart = "[RegistryReader]\t";

        private readonly CLogger log = CGlobals.Logger;

        public string DbString
        {
            get { return databaseName; }
        }

        public string HostString
        {
            get { return hostInstanceString; }
        }

        public CRegReader()
        {
        }

        public void GetDbInfo()
        {
            try
            {
                if(CGlobals.REMOTEEXEC)
                {
                    this.GetVbrElevenDbInfoRemote();
                }
                else
                {
                    this.GetVbrElevenDbInfo();
                }
            }
            catch (Exception e2)
            {
                this.log.Error(this.logStart + "Failed to get v11 DB info from Registry. Trying v12 registry hives:\t" + e2.Message);
            }

            if (string.IsNullOrEmpty(databaseName))
            {
                try
                {
                    if (CGlobals.REMOTEEXEC)
                    {
                        this.GetRemoteVbrTwelveDbInfo();
                    }
                    else
                    {
                        this.GetVbrTwelveDbInfo();
                    }
                }
                catch (Exception e3)
                {
                    this.log.Error(this.logStart + "Failed to get v12 DB info from Registry:\t" + e3.Message);
                }
            }
        }

        public string GetVbrVersionFilePath()
        {
            string consoleInstallPath = @"C:\Program Files\Veeam\Backup and Replication\Console\Veeam.Backup.Core.dll";

            var coreVersion = FileVersionInfo.GetVersionInfo (consoleInstallPath).FileVersion;
            this.log.Debug("[InstallDirectoryFileChecker]" + "VBR Core Version: " + coreVersion);
            if (!string.IsNullOrEmpty(coreVersion))
            {
                CGlobals.VBRFULLVERSION = coreVersion;
                this.ParseVbrMajorVersion(CGlobals.VBRFULLVERSION);
                return coreVersion;
            }

            log.Info("[InstallDirectoryFileChecker]" + "VBR Core Version not found in Console path, trying Mount Service path...");
            log.Debug(this.logStart + "Checking Registry for VBR Core path via Mount Service key...");
            try
            {
                using (RegistryKey key =
                    Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Mount Service"))
                {
                    var keyValue = key.GetValue("InstallationPath");
                    string path = string.Empty;
                    if (keyValue != null)
                    {
                        path = keyValue.ToString();
                    }
                    else
                    {
                        this.log.Error(this.logStart + "Failed to get VBR Core path from Mount Service registry key.");
                        return null;
                    }

                    // string path = key.GetValue("CorePath").ToString();
                    // FileInfo dllInfo = new FileInfo(path + "\\Packages\\VeeamDeploymentDll.dll");
                    var version = FileVersionInfo.GetVersionInfo(path + "\\Veeam.Backup.Core.dll");
                    CGlobals.VBRFULLVERSION = version.FileVersion;
                    this.ParseVbrMajorVersion(CGlobals.VBRFULLVERSION);
                    return CGlobals.VBRFULLVERSION;
                }
            }
            catch (System.Security.SecurityException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Cannot determine VBR version from Mount Service registry.");
                    return null;
                }
                else
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Cannot determine VBR version from Mount Service registry.");
                    return null;
                }
                else
                {
                    throw;
                }
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
                        this.SetMajorVersion(10);
                        break;
                    case 11:
                        this.SetMajorVersion(11);
                        break;
                    case 12:
                        this.SetMajorVersion(12);
                        break;
                    default:
                        this.SetMajorVersion(13);
                        break;
                }
            }
            catch (Exception e)
            {
                this.log.Error(this.logStart + "Failed to parse VBR Major Version:\n\t" + e.Message);
            }
        }

        private void SetMajorVersion(int version)
        {
            this.log.Info(this.logStart + "Setting VBR Major Version to: " + version);
            CGlobals.VBRMAJORVERSION = version;
        }

        private void GetVbrElevenDbInfoRemote()
        {
            using (RegistryKey key =
                RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
            {
                this.SetSqlInfo(key);
            }
        }

        private void GetVbrElevenDbInfo()
        {
            try
            {
                using (RegistryKey key =
                    Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
                {
                    this.SetSqlInfo(key);
                }
            }
            catch (System.Security.SecurityException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Skipping VBR v11 DB info collection.");
                }
                else
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Skipping VBR v11 DB info collection.");
                }
                else
                {
                    throw;
                }
            }
        }

        private void SetSqlInfo(RegistryKey key)
        {
            if (key != null)
            {
                var value = key.GetValue("SqlInstanceName");
                string instance = string.Empty;
                if (value != null)
                {
                    instance = value.ToString();
                }

                // var instance = key.GetValue("SqlInstanceName").ToString();
                var hostValue = key.GetValue("SqlServerName");
                string host = string.Empty;
                if(hostValue != null)
                {
                    host = hostValue.ToString();
                }

                // var host = key.GetValue("SqlServerName").ToString();
                var db = key.GetValue("SqlDatabaseName");
                string database = string.Empty;
                if (db != null)
                {
                    database = db.ToString();
                }

                // var database =key.GetValue("SqlDatabaseName").ToString();
                var userName = key.GetValue("SqlLogin");
                if (userName != null)
                {
                    user = userName.ToString();
                }

                // _user = key.GetValue("SqlLogin").ToString();
                var password = key.GetValue("SqlSecuredPassword");
                if (password != null)
                {
                    passString = password.ToString();
                }

                // _passString = key.GetValue("SqlSecuredPassword").ToString();

                if (!string.IsNullOrEmpty(host))
                {
                    if (!string.IsNullOrEmpty(database))
                    {
                        databaseName = database;
                        CRegReader.host = host;
                        hostInstanceString = host + "\\" + instance;
                    }
                }
            }
        }

        private string SetDbServerName(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                return null; // placeholder
            }

            return null; // placeholder
        }

        private void GetRemoteVbrTwelveDbInfo()
        {
            using (RegistryKey key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations"))
            {
                string host = string.Empty;

                if (key != null)
                {
                    var dbType = key.GetValue("SqlActiveConfiguration").ToString();
                    this.log.Info(this.logStart + "DB Type = " + dbType);
                    if (dbType == "MsSql")
                    {
                        CGlobals.DBTYPE = CGlobals.SqlTypeName;

                        // using (RegistryKey sqlKey = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
                        using (RegistryKey sqlKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
                        {
                            this.SetSqlInfo(sqlKey);

                            host = sqlKey.GetValue("SqlServerName").ToString();
                            if (host == "localhost")
                            {
                                CGlobals.ISDBLOCAL = "True";
                            }


                            CGlobals.DBHOSTNAME = host;

                            // CGlobals.DBINSTANCE = key.GetValue("SqlInstanceName").ToString();

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
                            {
                                CGlobals.ISDBLOCAL = "True";
                            }


                            CGlobals.DBHOSTNAME = host;
                        }
                    }

                    // var database =
                    //    key.GetValue("SqlDatabaseName")
                    //        .ToString();
                    // _user = key.GetValue("SqlLogin").ToString();
                    // _passString = key.GetValue("SqlSecuredPassword").ToString();
                    // if (!string.IsNullOrEmpty(host))
                    // {
                    //    if (!string.IsNullOrEmpty(database))
                    //    {
                    //        _databaseName = database;
                    //        _host = host;
                    //        _hostInstanceString = host + "\\" + instance;
                    //    }
                    // }
                }
            }
        }

        private void GetVbrTwelveDbInfo()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations"))
                {
                    string host = string.Empty;

                    if (key != null)
                    {
                        var dbType = key.GetValue("SqlActiveConfiguration").ToString();
                        this.log.Info(this.logStart + "DB Type = " + dbType);
                        if (dbType == "MsSql")
                        {
                            CGlobals.DBTYPE = CGlobals.SqlTypeName;

                            using (RegistryKey sqlKey = Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))

                            // using (RegistryKey sqlKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication\\DatabaseConfigurations\\MsSql"))
                            {
                                this.SetSqlInfo(sqlKey);

                                host = sqlKey.GetValue("SqlServerName").ToString();
                                if (host == "localhost")
                                {
                                    CGlobals.ISDBLOCAL = "True";
                                }


                                CGlobals.DBHOSTNAME = host;

                                // CGlobals.DBINSTANCE = key.GetValue("SqlInstanceName").ToString();

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
                                {
                                    CGlobals.ISDBLOCAL = "True";
                                }


                                CGlobals.DBHOSTNAME = host;
                            }
                        }

                    // var database =
                    //    key.GetValue("SqlDatabaseName")
                    //        .ToString();
                    // _user = key.GetValue("SqlLogin").ToString();
                    // _passString = key.GetValue("SqlSecuredPassword").ToString();
                    // if (!string.IsNullOrEmpty(host))
                    // {
                    //    if (!string.IsNullOrEmpty(database))
                    //    {
                    //        _databaseName = database;
                    //        _host = host;
                    //        _hostInstanceString = host + "\\" + instance;
                    //    }
                    // }
                    }
                }
            }
            catch (System.Security.SecurityException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Skipping VBR v12 DB info collection.");
                }
                else
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Skipping VBR v12 DB info collection.");
                }
                else
                {
                    throw;
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

                        // var vTest = key.GetValue(name);
                    }
                }
            }

            return keys;
        }

        public Dictionary<string, Object> DefaultVbrKeys()
        {
            Dictionary<string, Object> keys = new();
            try
            {
                using (RegistryKey key =
                    Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
                {
                    if (key != null)
                    {
                        string[] values = key.GetValueNames();
                        foreach (var name in values)
                        {
                            keys.Add(name, key.GetValue(name));

                            // var vTest = key.GetValue(name);
                        }
                    }
                }
            }
            catch (System.Security.SecurityException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Skipping default VBR keys collection.");
                }
                else
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Skipping default VBR keys collection.");
                }
                else
                {
                    throw;
                }
            }

            return keys;
        }

        public string DefaultLogDir()
        {
            var logDir = "C:\\ProgramData\\Veeam\\Backup";
            try
            {
                using (RegistryKey key =
                    Registry.LocalMachine.OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
                {
                    string dir = null;

                    if (key != null)
                    {
                        // dir = key.GetValue("LogDirectory").ToString();
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
            catch (System.Security.SecurityException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Using default log directory: " + logDir);
                    return logDir;
                }
                else
                {
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                if (CGlobals.RunningWithoutAdmin)
                {
                    this.log.Warning(this.logStart + "Registry access denied (running without admin). Using default log directory: " + logDir);
                    return logDir;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
