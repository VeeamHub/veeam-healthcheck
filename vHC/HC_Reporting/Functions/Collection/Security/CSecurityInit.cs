// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.Security
{
    internal class CSecurityInit
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
        int dwLogonType, int dwLogonProvider, out SafeAccessTokenHandle phToken);

        private readonly string appLogName = "Veeam.HealthCheck.ServerApplications.log";

        private readonly CLogger LOG;
        private readonly CLogger AppLOG;
        private readonly string logStart = "[Security]\t";

        private string VBRSERVER = CGlobals.REMOTEHOST;
        
        public CSecurityInit()
        {
            this.LOG = new CLogger("Veeam.HealthCheck.Security.log");
            this.AppLOG = new CLogger(this.appLogName);
        }

        public void Run()
        {
            if (CGlobals.REMOTEEXEC)
            {
            }

            // RunImpersonated();
            this.GetInstalledApps(); 
            this.IsRdpEnabled();
            this.IsDomainJoined();
        }

        private void RunImpersonated()
        {
            // Get the user token for the specified user, domain, and password using the   
            // unmanaged LogonUser method.   
            // The local machine name can be used for the domain name to impersonate a user on this machine.  
            // Console.Write("Enter the name of the VBR Server on which to log on: ");
            // string domainName = Console.ReadLine();

            Console.WriteLine("Logging into: " + CGlobals.REMOTEHOST);
            string domainName = CGlobals.REMOTEHOST;

            this.VBRSERVER = domainName;
            Console.Write("Enter the login of a user on {0} that you wish to impersonate: ", domainName);
            string userName = Console.ReadLine();

            Console.Write("Enter the password for {0}: ", userName);

            const int LOGON32_PROVIDER_DEFAULT = 0;

            // This parameter causes LogonUser to create a primary token.   
            // const int LOGON32_LOGON_INTERACTIVE = 2;
            const int LOGON32_LOGON_INTERACTIVE = 9;

            string password = null;
            while (true)
            {
                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }


                password += key.KeyChar;
            }

            Console.WriteLine("Executing...");

            // Call LogonUser to obtain a handle to an access token.   
            SafeAccessTokenHandle safeAccessTokenHandle;

            // bool returnValue = LogonUser(userName, domainName, Console.ReadLine(),
            //    LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
            //    out safeAccessTokenHandle);
            bool returnValue = LogonUser(userName, domainName, password,
            LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
            out safeAccessTokenHandle);

            if (false == returnValue)
            {
                int ret = Marshal.GetLastWin32Error();
                Console.WriteLine("LogonUser failed with error code : {0}", ret);
                throw new System.ComponentModel.Win32Exception(ret);
            }

            // Console.WriteLine("Did LogonUser Succeed? " + (returnValue ? "Yes" : "No"));
            // Check the identity.  
            // Console.WriteLine("Before impersonation: " + WindowsIdentity.GetCurrent().Name);

            // Note: if you want to run as unimpersonated, pass  
            //       'SafeAccessTokenHandle.InvalidHandle' instead of variable 'safeAccessTokenHandle'  
            WindowsIdentity.RunImpersonated(
                safeAccessTokenHandle,

                // User action  
                () =>
                {
                    // Check the identity.  
                //    Console.WriteLine("During impersonation: " + WindowsIdentity.GetCurrent().Name);
                    this.IsRdpEnabled();
                    this.GetInstalledApps();

                    // IsDomainJoined(); // This may not work....
                }
                );

            // Check the identity again.  
            Console.WriteLine("After impersonation: " + WindowsIdentity.GetCurrent().Name);
        }

        private void IsDomainJoined()
        {
            string domain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
            if (string.IsNullOrEmpty(domain))
            {
                this.LOG.Info(this.logStart + "host is not domain joined.");
                CSecurityGlobalValues.IsDomainJoined = "False";
            }
            else if (domain.Length > 0)
            {
                this.LOG.Info(this.logStart + "host is domain joined.");
                CSecurityGlobalValues.IsDomainJoined = "True";
            }
            else
            {
                this.LOG.Warning(this.logStart + "unable to determine host domain status");
                CSecurityGlobalValues.IsDomainJoined = "Undetermined.";
            }
        }

        private void GetInstalledApps()
        {
            this.LOG.Info(this.logStart + "Getting list of apps. Output to be shown in " + this.appLogName);
            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, this.VBRSERVER).OpenSubKey(registry_key))
            {
                this.AppLOG.Info("Installed apps: ", false);
                foreach (string subkey_name in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        try
                        {
                            var res = subkey.GetValue("DisplayName");
                            if(res != null)
                            {
                                res.ToString();

                                // var n  = subkey.TryGetPropertyValue<string>("DisplayName");
                                string name = res.ToString();// subkey.GetValue("DisplayName").ToString();
                                if (CSecurityGlobalValues.IsConsoleInstalled == "Undetermined" || CSecurityGlobalValues.IsConsoleInstalled == "False")
                                {
                                    if (name == "Veeam Backup & Replication Console")
                                    {
                                        CSecurityGlobalValues.IsConsoleInstalled = "True";
                                    }
                                    else
                                    {
                                        CSecurityGlobalValues.IsConsoleInstalled = "False";
                                    }
                                }

                                this.AppLOG.Info("\t" + name, true);
                            }
                        }
                        catch (Exception)
                        {
                            // LOG.Error(e.ToString(), true);
                        }
                    }
                }
            }
        }

        private void IsRdpEnabled()
        {
            string registryKey = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
            string keyName = "fDenyTSConnections";
            using (RegistryKey key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, this.VBRSERVER).OpenSubKey(registryKey))// .LocalMachine.OpenSubKey(registryKey))
            {
                this.LOG.Info("RDP Status:");

                var v = key.GetValue(keyName).ToString();

                switch (v)
                {
                    case "0":
                        this.LOG.Info("\tRDP enabled.");
                        CSecurityGlobalValues.IsRdpEnabled = "True";
                        break;
                    case "1":
                        this.LOG.Info("\tRDP disabled.");
                        CSecurityGlobalValues.IsRdpEnabled = "False";
                        break;
                    default:
                        this.LOG.Info("\tRDP undetermined");
                        CSecurityGlobalValues.IsRdpEnabled = "Undetermined";
                        break;
                }
            }
        }
    }
}

