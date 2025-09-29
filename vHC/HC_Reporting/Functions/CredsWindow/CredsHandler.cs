using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.CredsWindow
{
    public class CredsHandler
    {
        public (string Username, string Password)? GetCreds()
        {
            var creds = PromptForCredentials(CGlobals.REMOTEHOST);
            if (creds == null)
            {
                CGlobals.Logger.Error("Credentials not provided. Aborting MFA test.", false);
                return creds;
            }

            return creds;
        }
        private (string Username, string Password)? PromptForCredentials(string host)
        {
            var cached = CredentialStore.Get(host);
            if (cached != null)
                return cached;

            // Show your WPF dialog here (or use your existing method)
            var dialog = new CredentialPromptWindow(host)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                CredentialStore.Set(host, dialog.Username, dialog.Password);
                return (dialog.Username, dialog.Password);
            }
            return null;
        }
    }
}

