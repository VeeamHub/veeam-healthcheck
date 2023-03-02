// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Microsoft.Win32;
using System;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.Security
{
    internal class CSecurityInit
    {
        private readonly string _appLogName = "Veeam.HealthCheck.ServerApplications.log";

        private readonly CLogger LOG;
        private readonly CLogger AppLOG;
        private readonly string logStart = "[Security]\t";
        public CSecurityInit()
        {
            LOG = new CLogger("Veeam.HealthCheck.Security.log");
            AppLOG = new CLogger(_appLogName);
        }

        public void Run()
        {
            GetInstalledApps(); //TODO: uncomment before publish
            IsRdpEnabled();
            IsDomainJoined();
        }
        private void IsDomainJoined()
        {
            string domain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
            if (string.IsNullOrEmpty(domain))
            {
                LOG.Info(logStart + "host is not domain joined.");
                CSecurityGlobalValues.IsDomainJoined = "False";
            }
            else if (domain.Length > 0)
            {
                LOG.Info(logStart + "host is domain joined.");
                CSecurityGlobalValues.IsDomainJoined = "True";
            }
            else
            {
                LOG.Warning(logStart + "unable to determine host domain status");
                CSecurityGlobalValues.IsDomainJoined = "Undetermined.";
            }
        }
        private void GetInstalledApps()
        {
            LOG.Info(logStart + "Getting list of apps. Output to be shown in " + _appLogName);
            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry_key))
            {
                AppLOG.Info("Installed apps: ", false);
                foreach (string subkey_name in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        try
                        {
                            //var n  = subkey.TryGetPropertyValue<string>("DisplayName");
                            string name = subkey.GetValue("DisplayName").ToString();
                            if (CSecurityGlobalValues.IsConsoleInstalled == "Undetermined" || CSecurityGlobalValues.IsConsoleInstalled == "False")
                            {
                                if (name == "Veeam Backup & Replication Console")
                                    CSecurityGlobalValues.IsConsoleInstalled = "True";
                                else
                                    CSecurityGlobalValues.IsConsoleInstalled = "False";
                            }
                            AppLOG.Info("\t" + subkey.GetValue("DisplayName").ToString(), true);

                        }
                        catch (Exception e)
                        {
                            //LOG.Error(e.ToString(), true);
                        }
                    }
                }
            }
        }
        private void IsRdpEnabled()
        {
            string registryKey = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
            string keyName = "fDenyTSConnections";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey))
            {
                LOG.Info("RDP Status:");

                var v = key.GetValue(keyName).ToString();


                switch (v)
                {
                    case "0":
                        LOG.Info("\tRDP enabled.");
                        CSecurityGlobalValues.IsRdpEnabled = "True";
                        break;
                    case "1":
                        LOG.Info("\tRDP disabled.");
                        CSecurityGlobalValues.IsRdpEnabled = "False";
                        break;
                    default:
                        LOG.Info("\tRDP undetermined");
                        CSecurityGlobalValues.IsRdpEnabled = "Undetermined";
                        break;
                }


            }
        }
    }
}

