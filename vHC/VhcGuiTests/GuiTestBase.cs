using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
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
        private readonly string _dir;
        private readonly string _origSettingsPath;
        private readonly string _origCredsPath;
        private readonly Window _origMainWindow;
        private readonly ThemeVariant _origTheme;

        private readonly bool _isVbr, _isVb365, _isVbrInstalled, _remoteExec, _guiExec, _silent, _import;
        private readonly bool _scrub, _openHtml, _openExplorer, _exportPdf, _clearCreds, _rescan;
        private readonly int _reportDays;
        private readonly string _remoteHost, _vbrServerName, _desiredPath;
        private readonly TargetProduct _targetProduct;
        private readonly IUiNotifier _notifier;

        protected GuiTestBase()
        {
            _isVbr = CGlobals.IsVbr;
            _isVb365 = CGlobals.IsVb365;
            _isVbrInstalled = CGlobals.IsVbrInstalled;
            _remoteExec = CGlobals.REMOTEEXEC;
            _remoteHost = CGlobals.REMOTEHOST;
            _vbrServerName = CGlobals.VBRServerName;
            _reportDays = CGlobals.ReportDays;
            _scrub = CGlobals.Scrub;
            _openHtml = CGlobals.OpenHtml;
            _openExplorer = CGlobals.OpenExplorer;
            _exportPdf = CGlobals.EXPORTPDF;
            _clearCreds = CGlobals.ClearStoredCreds;
            _rescan = CGlobals.RescanHosts;
            _targetProduct = CGlobals.TargetProductType;
            _desiredPath = CGlobals.desiredPath;
            _import = CGlobals.IMPORT;
            _guiExec = CGlobals.GUIEXEC;
            _silent = CGlobals.Silent;
            _notifier = CGlobals.Notifier;
            _origMainWindow = AvaloniaHost.MainWindow;
            _origTheme = Application.Current?.RequestedThemeVariant;
            _origSettingsPath = CAppSettings.StorePath;
            _origCredsPath = CredentialStore.StorePath;

            CGlobals.IsVbr = false;
            CGlobals.IsVb365 = false;
            CGlobals.IsVbrInstalled = false;
            CGlobals.GUIEXEC = false;
            CGlobals.Silent = false;
            CGlobals.IMPORT = false;
            CGlobals.REMOTEEXEC = false;
            CGlobals.REMOTEHOST = string.Empty;
            CGlobals.ReportDays = 7;
            CGlobals.Scrub = false;
            CGlobals.EXPORTPDF = false;
            CGlobals.TargetProductType = TargetProduct.Auto;
            CGlobals.Notifier = Stub = new StubUiNotifier();

            _dir = Path.Combine(Path.GetTempPath(), "vhcgui-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SettingsPath = Path.Combine(_dir, "settings.json");
            CAppSettings.StorePath = SettingsPath;
            CredentialStore.StorePath = Path.Combine(_dir, "creds.json");
            CredentialStore.InitializeCache();
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

        /// <summary>Constructs and shows a VhcGui, tracked for cleanup.</summary>
        protected VhcGui ShowMainWindow()
        {
            var w = Track(new VhcGui());
            w.Show();
            return w;
        }

        public void Dispose()
        {
            foreach (var w in _windows.AsEnumerable().Reverse())
            {
                CloseWithOwned(w);
            }

            Dispatcher.UIThread.RunJobs();

            CGlobals.IsVbr = _isVbr;
            CGlobals.IsVb365 = _isVb365;
            CGlobals.IsVbrInstalled = _isVbrInstalled;
            CGlobals.REMOTEEXEC = _remoteExec;
            CGlobals.REMOTEHOST = _remoteHost;
            CGlobals.VBRServerName = _vbrServerName;
            CGlobals.ReportDays = _reportDays;
            CGlobals.Scrub = _scrub;
            CGlobals.OpenHtml = _openHtml;
            CGlobals.OpenExplorer = _openExplorer;
            CGlobals.EXPORTPDF = _exportPdf;
            CGlobals.ClearStoredCreds = _clearCreds;
            CGlobals.RescanHosts = _rescan;
            CGlobals.TargetProductType = _targetProduct;
            CGlobals.desiredPath = _desiredPath;
            CGlobals.IMPORT = _import;
            CGlobals.GUIEXEC = _guiExec;
            CGlobals.Silent = _silent;
            CGlobals.Notifier = _notifier;
            AvaloniaHost.MainWindow = _origMainWindow;
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = _origTheme;
            }

            CAppSettings.StorePath = _origSettingsPath;
            CredentialStore.StorePath = _origCredsPath;
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
