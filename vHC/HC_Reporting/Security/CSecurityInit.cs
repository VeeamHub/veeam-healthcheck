using Microsoft.Win32;
using Namotion.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Security
{
    internal class CSecurityInit
    {
        private readonly string _appLogName = "Veeam.ServerApplications.log";

        private readonly CLogger LOG;
        private readonly CLogger AppLOG;
        private readonly string logStart = "[Security]\t";
        public CSecurityInit()
        {
            LOG = new CLogger("Veeam.Security.log");
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
            if(String.IsNullOrEmpty(domain) )
            {
                LOG.Info(logStart + "host is not domain joined.");
                CGlobals._isDomainJoined = "False";
            } 
            else if (domain.Length> 0)
            {
                LOG.Info(logStart + "host is domain joined.");
                CGlobals._isDomainJoined = "True";
            }
            else
            {
                LOG.Warning(logStart + "unable to determine host domain status");
                CGlobals._isDomainJoined = "Undetermined.";
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
                            if (CGlobals.isConsoleLocal == "Undetermined" || CGlobals.isConsoleLocal == "False")
                            {
                                if (name == "Veeam Backup & Replication Console")
                                    CGlobals.isConsoleLocal = "True";
                                else
                                    CGlobals.isConsoleLocal = "False";
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
                        CGlobals._isRdpEnabled = "True";
                        break;
                    case "1":
                        LOG.Info("\tRDP disabled.");
                        CGlobals._isRdpEnabled = "False";
                        break;
                    default:
                        LOG.Info("\tRDP undetermined");
                        CGlobals._isRdpEnabled = "Undetermined";
                        break;
                }


            }
        }
    }
}

