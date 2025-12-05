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
            if (!string.IsNullOrEmpty(CGlobals.CredsUsername) && !string.IsNullOrEmpty(CGlobals.CredsPassword))
            {
                return (CGlobals.CredsUsername, CGlobals.CredsPassword);
            }

            var creds = this.PromptForCredentials(CGlobals.REMOTEHOST);
            if (creds == null)
            {
                CGlobals.Logger.Error("Credentials not provided. Aborting MFA test.", false);
                return creds;
            }

            return creds;
        }

        private (string Username, string Password)? PromptForCredentials(string host)
        {
            if (CGlobals.UseStoredCreds)
            {
                var cached = CredentialStore.Get(host);
                if (cached != null)
                {
                    return cached;
                }
            }

            // Check if GUI is available before attempting to show dialog
            if (!CGlobals.GUIEXEC || System.Windows.Application.Current == null)
            {
                CGlobals.Logger.Warning("GUI not available. Cannot prompt for credentials in non-interactive environment.");
                return null;
            }

            // Show your WPF dialog here (or use your existing method)
            (string Username, string Password)? result = null;
            
            // If we're on the UI thread, show the dialog directly
            if (System.Windows.Application.Current?.MainWindow?.Dispatcher.CheckAccess() == true)
            {
                var dialog = new CredentialPromptWindow(host)
                {
                    Owner = System.Windows.Application.Current?.MainWindow,
                };
                if (dialog.ShowDialog() == true)
                {
                    result = (dialog.Username, dialog.Password);
                    if (CGlobals.UseStoredCreds)
                    {
                        CredentialStore.Set(host, dialog.Username, dialog.Password);
                    }
                }
            }
            else
            {
                // If we're on a background thread, marshal to the UI thread
                System.Windows.Application.Current?.MainWindow?.Dispatcher.Invoke(() =>
                {
                    var dialog = new CredentialPromptWindow(host)
                    {
                        Owner = System.Windows.Application.Current?.MainWindow,
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        result = (dialog.Username, dialog.Password);
                        if (CGlobals.UseStoredCreds)
                        {
                            CredentialStore.Set(host, dialog.Username, dialog.Password);
                        }
                    }
                });
            }

            return result;
        }
    }
}
