// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Threading.Tasks;
using Avalonia.Threading;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    internal sealed class AvaloniaCredentialPrompter : ICredentialPrompter
    {
        public async Task<(string Username, string Password)?> PromptAsync(string host)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new CredentialPromptWindow(host);
                bool accepted = await dialog.ShowDialog<bool>(AvaloniaHost.MainWindow);

                if (!accepted)
                {
                    return ((string, string)?)null;
                }

                CredentialStore.Set(host, dialog.Username, dialog.Password);
                CGlobals.Logger.Debug($"Credentials stored for host: {host}");
                return (dialog.Username, dialog.Password);
            });
        }
    }
}
