// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Threading.Tasks;
using System.Windows;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    internal sealed class WpfCredentialPrompter : ICredentialPrompter
    {
        public Task<(string Username, string Password)?> PromptAsync(string host)
        {
            Application app = Application.Current;
            System.Windows.Threading.Dispatcher dispatcher = app?.Dispatcher;

            if (dispatcher == null)
            {
                CGlobals.Logger.Warning("No dispatcher available for credential prompt.");
                return Task.FromResult<(string Username, string Password)?>(null);
            }

            (string Username, string Password)? result = null;

            if (dispatcher.CheckAccess())
            {
                result = ShowDialog(host, app.MainWindow);
            }
            else
            {
                dispatcher.Invoke(() => result = ShowDialog(host, app.MainWindow));
            }

            return Task.FromResult(result);
        }

        private static (string Username, string Password)? ShowDialog(string host, Window owner)
        {
            var dialog = new CredentialPromptWindow(host);

            if (owner != null)
            {
                dialog.Owner = owner;
            }

            if (dialog.ShowDialog() == true)
            {
                CredentialStore.Set(host, dialog.Username, dialog.Password);
                CGlobals.Logger.Debug($"Credentials stored for host: {host}");
                return (dialog.Username, dialog.Password);
            }

            return null;
        }
    }
}
