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
                    // Always store credentials for future use
                    CredentialStore.Set(host, dialog.Username, dialog.Password);
                    CGlobals.Logger.Debug($"Credentials stored for host: {host}");
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
                        // Always store credentials for future use
                        CredentialStore.Set(host, dialog.Username, dialog.Password);
                        CGlobals.Logger.Debug($"Credentials stored for host: {host}");
                    }
                });
            }

            return result;
        }
        
        /// <summary>
        /// Prompts for credentials in a standalone WPF context (when main GUI is not available)
        /// </summary>
        public (string Username, string Password)? PromptForCredentialsStandalone(string host)
        {
            (string Username, string Password)? result = null;
            
            // Create a minimal WPF application context if needed
            if (System.Windows.Application.Current == null)
            {
                var app = new System.Windows.Application();
                app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                
                // Show the credential dialog
                var dialog = new CredentialPromptWindow(host);
                if (dialog.ShowDialog() == true)
                {
                    string username = dialog.Username;
                    string password = dialog.Password;
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        CredentialStore.Set(host, username, password);
                        result = (username, password);
                    }
                }
                
                app.Shutdown();
            }
            else
            {
                // Use existing application context
                var dialog = new CredentialPromptWindow(host);
                if (dialog.ShowDialog() == true)
                {
                    string username = dialog.Username;
                    string password = dialog.Password;
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        CredentialStore.Set(host, username, password);
                        result = (username, password);
                    }
                }
            }
            
            return result;
        }
    }
}
