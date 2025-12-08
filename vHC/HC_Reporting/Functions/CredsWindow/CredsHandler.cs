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

            // First, check if we have stored credentials
            var stored = CredentialStore.Get(host);
            if (stored != null)
            {
                CGlobals.Logger.Debug($"Using stored credentials for host: {host}");
                return stored;
            }

            // Second, prompt for credentials (GUI or CLI)
            var creds = this.PromptForCredentials(host);
            if (creds == null)
            {
                CGlobals.Logger.Error("Credentials not provided. Aborting.", false);
                return creds;
            }

            return creds;
        }

        private (string Username, string Password)? PromptForCredentials(string host)
        {
            // If GUI is available, use the GUI prompt
            if (CGlobals.GUIEXEC && System.Windows.Application.Current != null)
            {
                return this.PromptForCredentialsGui(host);
            }

            // Otherwise, use CLI prompt
            return this.PromptForCredentialsCli(host);
        }

        private (string Username, string Password)? PromptForCredentialsCli(string host)
        {
            CGlobals.Logger.Info($"Credentials required for host: {host}", false);

            try
            {
                Console.WriteLine();
                Console.WriteLine($"=== Authentication Required for {host} ===");
                Console.Write("Username: ");
                string username = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(username))
                {
                    CGlobals.Logger.Warning("Username cannot be empty.");
                    return null;
                }

                Console.Write("Password: ");
                string password = ReadPasswordMasked();
                Console.WriteLine(); // New line after password entry

                if (string.IsNullOrEmpty(password))
                {
                    CGlobals.Logger.Warning("Password cannot be empty.");
                    return null;
                }

                // Store credentials for future use
                CredentialStore.Set(host, username, password);
                CGlobals.Logger.Info($"Credentials stored for host: {host}", false);

                return (username, password);
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error($"Error reading credentials: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads a password from the console, masking input with asterisks.
        /// </summary>
        private string ReadPasswordMasked()
        {
            var password = new StringBuilder();

            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Remove(password.Length - 1, 1);
                        Console.Write("\b \b"); // Erase the last asterisk
                    }
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    password.Append(keyInfo.KeyChar);
                    Console.Write("*");
                }
            }

            return password.ToString();
        }

        private (string Username, string Password)? PromptForCredentialsGui(string host)
        {
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
