using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.Collection
{
    internal class CImpersonation
    {
        private CLogger _logger = CGlobals.Logger;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
                                        int dwLogonType, int dwLogonProvider, out SafeAccessTokenHandle phToken);

        private string VBRSERVER = "localhost";

        public CImpersonation()
        {

        }

        public void RunCollection()
        {
            SafeAccessTokenHandle phToken = SafeAccessTokenHandle();
            WindowsIdentity.RunImpersonated(
                    phToken,
                    // User action  
                    () =>
                    {
                        //CClientFunctions cf = new();
                        //cf.GetVbrVersion();

                        CCollections collect = new();
                        collect.Run();
                        // Check the identity.  
                        //    Console.WriteLine("During impersonation: " + WindowsIdentity.GetCurrent().Name);
                        //IsDomainJoined(); // This may not work....
                    }
                    );
        }

        private SafeAccessTokenHandle SafeAccessTokenHandle()
        {
            _logger.Info("Logging into: " + CGlobals.REMOTEHOST, false);
            string domainName = CGlobals.REMOTEHOST;

            VBRSERVER = domainName;
            Console.WriteLine(String.Format("Enter the login of a user on {0} that you wish to impersonate: ", domainName),false);
            string userName = Console.ReadLine();

            Console.WriteLine(String.Format("Enter the password for {0}: ", userName), false);

            const int LOGON32_PROVIDER_DEFAULT = 0;
            //This parameter causes LogonUser to create a primary token.   
            //const int LOGON32_LOGON_INTERACTIVE = 2;
            const int LOGON32_LOGON_INTERACTIVE = 9;

            string password = null;
            while (true)
            {
                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                password += key.KeyChar;
            }
            Console.WriteLine("Executing...");

            // Call LogonUser to obtain a handle to an access token.   
            SafeAccessTokenHandle safeAccessTokenHandle;
            //bool returnValue = LogonUser(userName, domainName, Console.ReadLine(),
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
            return safeAccessTokenHandle;
        }


    }
}
