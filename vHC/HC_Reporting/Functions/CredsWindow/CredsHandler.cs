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
        // Test seam: counts the number of times PromptForCredentials is reached.
        // Used by SilentModeTests to assert that silent mode never prompts.
        // Production callers do not need to read this.
        internal static int PromptCallCount = 0;

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

            // Silent mode contract: never prompt. If we got here it means there is
            // no stored credential and no /credfile= entry for this host. The caller
            // (typically MfaTestPassed) is responsible for translating null into
            // the appropriate exit code (2 for "creds missing"). We log to stderr
            // here so unattended runs leave a breadcrumb without the caller having
            // to know the host name.
            if (CGlobals.Silent)
            {
                string msg = $"[silent] No credentials for host '{host}'. Seed with /savecreds or supply /credfile=.";
                Console.Error.WriteLine(msg);
                CGlobals.Logger.Error(msg, false);
                return null;
            }

            // Second, prompt for credentials (GUI or CLI)
            PromptCallCount++;
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
            if (CGlobals.GUIEXEC && CGlobals.CredentialPrompter != null)
            {
                return CGlobals.CredentialPrompter.Prompt(host);
            }

            // Otherwise, use CLI prompt
            return this.PromptForCredentialsCli(host);
        }

        internal (string Username, string Password)? PromptForCredentialsCli(string host, string headerPrefix = "Authentication Required")
        {
            CGlobals.Logger.Info($"Credentials required for host: {host}", false);

            try
            {
                Console.WriteLine();
                Console.WriteLine($"=== {headerPrefix} for {host} ===");
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
        /// Static — no instance state required. Exposed as <c>internal</c>
        /// so silent-mode helpers (e.g. <c>CArgsParser.RunSaveCredsFlow</c>)
        /// can reuse a single masked-read implementation.
        /// </summary>
        internal static string ReadPasswordMasked()
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


    }
}
