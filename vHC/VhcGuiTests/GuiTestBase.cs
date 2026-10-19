using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;

namespace VhcGuiTests
{
    /// <summary>
    /// Base for every GUI test class. The constructor snapshots every static the GUI touches and
    /// sets a safe baseline (no Veeam product, stub notifier, throwaway settings and credential
    /// stores); Dispose closes windows opened through <see cref="Track{T}"/> and restores everything.
    /// IsVb365 must stay false: CClientFunctions exits the process for local VB365 without admin.
    /// </summary>
    public abstract class GuiTestBase : IDisposable
    {
        private readonly List<Window> _windows = new();
        private readonly List<Action> _undo = new();
        private readonly string _dir;
        private bool _disposed;

        protected GuiTestBase()
        {
            Save(() => CGlobals.IsVbr, v => CGlobals.IsVbr = v, false);
            Save(() => CGlobals.IsVb365, v => CGlobals.IsVb365 = v, false);
            Save(() => CGlobals.IsVbrInstalled, v => CGlobals.IsVbrInstalled = v, false);
            Save(() => CGlobals.GUIEXEC, v => CGlobals.GUIEXEC = v, false);
            Save(() => CGlobals.Silent, v => CGlobals.Silent = v, false);
            Save(() => CGlobals.IMPORT, v => CGlobals.IMPORT = v, false);
            Save(() => CGlobals.REMOTEEXEC, v => CGlobals.REMOTEEXEC = v, false);
            Save(() => CGlobals.REMOTEHOST, v => CGlobals.REMOTEHOST = v, string.Empty);
            Save(() => CGlobals.ReportDays, v => CGlobals.ReportDays = v, 7);
            Save(() => CGlobals.Scrub, v => CGlobals.Scrub = v, false);
            Save(() => CGlobals.EXPORTPDF, v => CGlobals.EXPORTPDF = v, false);
            Save(() => CGlobals.TargetProductType, v => CGlobals.TargetProductType = v, TargetProduct.Auto);
            Save(() => CGlobals.Notifier, v => CGlobals.Notifier = v, Stub = new StubUiNotifier());

            // Mutated by the GUI but not baselined: keep the current value.
            Save(() => CGlobals.VBRServerName, v => CGlobals.VBRServerName = v);
            Save(() => CGlobals.OpenHtml, v => CGlobals.OpenHtml = v);
            Save(() => CGlobals.OpenExplorer, v => CGlobals.OpenExplorer = v);
            Save(() => CGlobals.ClearStoredCreds, v => CGlobals.ClearStoredCreds = v);
            Save(() => CGlobals.RescanHosts, v => CGlobals.RescanHosts = v);
            Save(() => CGlobals.desiredPath, v => CGlobals.desiredPath = v);
            Save(() => AvaloniaHost.MainWindow, v => AvaloniaHost.MainWindow = v);
            Save(() => Application.Current?.RequestedThemeVariant, v =>
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = v;
                }
            });

            _dir = Path.Combine(Path.GetTempPath(), "vhcgui-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            Save(() => CAppSettings.StorePath, v => CAppSettings.StorePath = v);
            Save(() => CredentialStore.StorePath, v => CredentialStore.StorePath = v);
            IsolatedStores.Redirect(_dir);
            SettingsPath = CAppSettings.StorePath;
        }

        protected StubUiNotifier Stub { get; }

        protected string SettingsPath { get; }

        protected string TempDir => _dir;

        /// <summary>Registers a window so Dispose closes it; returns it for chaining.</summary>
        protected T Track<T>(T window) where T : Window
        {
            _windows.Add(window);
            return window;
        }

        /// <summary>Seeds the persisted server list (default setup is one server, "vbr01").</summary>
        protected static void SeedServers(params string[] servers) => CAppSettings.SetServers(servers);

        /// <summary>The persisted server list, never null.</summary>
        protected static List<string> ServersInSettings() => CAppSettings.Get().Servers ?? new List<string>();

        /// <summary>Constructs and shows a VhcGui, tracked for cleanup.</summary>
        protected VhcGui ShowMainWindow()
        {
            var w = Track(new VhcGui());
            w.Show();
            return w;
        }

        /// <summary>Seeds servers (default "vbr01"), shows the main window and waits for startup to finish.</summary>
        protected async Task<VhcGui> ShowReadyWindow(params string[] servers)
        {
            SeedServers(servers.Length > 0 ? servers : new[] { "vbr01" });
            var w = ShowMainWindow();
            await UiDriver.WaitForStartup(w);
            return w;
        }

        /// <summary>Shows a plain tracked window and makes it the host's main window, as the owner for dialogs.</summary>
        protected Window ShowOwner()
        {
            var owner = Track(new Window());
            owner.Show();
            AvaloniaHost.MainWindow = owner;
            return owner;
        }

        /// <summary>Replaces the Run seam; the returned list receives each call's import flag.</summary>
        protected static List<bool> CaptureRuns(VhcGui w)
        {
            var calls = new List<bool>();
            w.RunAction = calls.Add;
            return calls;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var w in _windows.AsEnumerable().Reverse())
            {
                CloseWithOwned(w);
            }

            Dispatcher.UIThread.RunJobs();

            foreach (var undo in _undo.AsEnumerable().Reverse())
            {
                undo();
            }

            CredentialStore.InitializeCache();

            try
            {
                if (Directory.Exists(_dir))
                {
                    Directory.Delete(_dir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        private void Save<T>(Func<T> get, Action<T> set, T baseline)
        {
            Save(get, set);
            set(baseline);
        }

        private void Save<T>(Func<T> get, Action<T> set)
        {
            var original = get();
            _undo.Add(() => set(original));
        }

        private static void CloseWithOwned(Window w)
        {
            foreach (var owned in w.OwnedWindows.ToList())
            {
                CloseWithOwned(owned);
            }

            try
            {
                w.Close();
            }
            catch
            {
                // Already closed
            }
        }
    }
}
