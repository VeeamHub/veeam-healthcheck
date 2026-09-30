// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
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
                var owner = AvaloniaHost.CurrentOwner();
                var dialog = new CredentialPromptWindow(host);
                bool accepted = await dialog.ShowDialog<bool>(owner);

                if (!accepted)
                {
                    return ((string, string)?)null;
                }

                try
                {
                    CredentialStore.Set(host, dialog.Username, dialog.Password);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Only the disk write is tolerated. Set() caches in memory before it
                    // persists, so after an IO failure the credentials are still good for
                    // this run, and losing them would abort a run over a disk problem the
                    // user can do nothing about mid-prompt. A failure to encrypt them
                    // (CryptographicException from DPAPI) means nothing was cached, so it
                    // is deliberately not caught here and surfaces as before.
                    CGlobals.Logger.Warning($"Could not persist credentials for host {host}; using them for this session only. Error: {ex.Message}");
                }

                CAppSettings.AddServer(host);
                CGlobals.Logger.Debug($"Credentials stored for host: {host}");
                return (dialog.Username, dialog.Password);
            });
        }
    }
}
