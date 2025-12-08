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
            string host = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;

            // Check if user requested to clear stored credentials
            if (CGlobals.ClearStoredCreds)
            {
                CGlobals.Logger.Info("Clearing stored credentials as requested by user", false);
                CredentialStore.Clear();
                // Reset the flag so it doesn't clear again on subsequent calls
                CGlobals.ClearStoredCreds = false;
            }

            // First, check if credentials were provided via command line
            if (!string.IsNullOrEmpty(CGlobals.CredsUsername) && !string.IsNullOrEmpty(CGlobals.CredsPassword))
            {
                CGlobals.Logger.Info($"Using command-line credentials for host: {host}", false);
                // Store them for future use
                CredentialStore.Set(host, CGlobals.CredsUsername, CGlobals.CredsPassword);
                return (CGlobals.CredsUsername, CGlobals.CredsPassword);
            }

            // Second, check if we have stored credentials
            var stored = CredentialStore.Get(host);
            if (stored != null)
            {
                CGlobals.Logger.Debug($"Using stored credentials for host: {host}");
                return stored;
            }

            // Third, try to prompt for credentials (only if GUI available)
            var creds = this.PromptForCredentials(host);
            if (creds == null)
            {
                CGlobals.Logger.Error("Credentials not provided. Aborting MFA test.", false);
                return creds;
            }

            return creds;
        }

        private (string Username, string Password)? PromptForCredentials(string host)
        {
            // Check if GUI is available before attempting to show dialog
            if (!CGlobals.GUIEXEC || System.Windows.Application.Current == null)
            {
                CGlobals.Logger.Warning("GUI not available. Cannot prompt for credentials in non-interactive environment.");
                CGlobals.Logger.Warning($"Please provide credentials for host '{host}' using the /creds parameter or run in GUI mode.");
                return null;
            }

            var app = System.Windows.Application.Current;
            var dispatcher = app.Dispatcher;

            if (dispatcher == null)
            {
                CGlobals.Logger.Warning("No dispatcher available for credential prompt.");
                return null;
            }

            (string Username, string Password)? result = null;

            // Always use the Application dispatcher to ensure we can show the dialog
            // even if MainWindow is not yet set
            if (dispatcher.CheckAccess())
            {
                // We're on the UI thread, show dialog directly
                result = this.ShowCredentialDialog(host, app.MainWindow);
            }
            else
            {
                // We're on a background thread, marshal to the UI thread
                dispatcher.Invoke(() =>
                {
                    result = this.ShowCredentialDialog(host, app.MainWindow);
                });
            }

            return result;
        }

        private (string Username, string Password)? ShowCredentialDialog(string host, System.Windows.Window owner)
        {
            var dialog = new CredentialPromptWindow(host);

            // Set owner if available (makes the dialog modal to the main window)
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            if (dialog.ShowDialog() == true)
            {
                // Store credentials for future use
                CredentialStore.Set(host, dialog.Username, dialog.Password);
                CGlobals.Logger.Debug($"Credentials stored for host: {host}");
                return (dialog.Username, dialog.Password);
            }

            return null;
        }
    }
}
