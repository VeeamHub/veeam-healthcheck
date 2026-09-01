// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using VeeamHealthCheck.Functions.Monitor;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck
{
    /// <summary>
    /// Interaction logic for VhcGui.axaml
    /// </summary>
    public partial class VhcGui : Window
    {
        private readonly CClientFunctions functions = new();
        private bool _modeCheckFailed;

        public VhcGui()
        {
            InitializeComponent();

            // AvaloniaUiNotifier passes this as the ShowDialog owner. Set it
            // here (rather than waiting for Task 12's App.axaml.cs) because
            // AcceptButton_click's Task.Run(AcceptTerms) can raise a dialog
            // before that wiring exists.
            AvaloniaHost.MainWindow = this;

            this.SetUiSync();
            pathBox.IsEnabled = true;
            this.InitializeServerList();
            this.InitializeMonitorStatus();

            // pdfCheckBox.IsEnabled = false;

            this.Loaded += async (s, e) => await this.SetUiAsync();
        }

        private void InitializeServerList()
        {
            // Load saved servers from credentials
            var savedServers = CredentialStore.GetAllServers();

            // Add localhost if VBR is installed
            if (CGlobals.IsVbrInstalled && !savedServers.Contains("localhost"))
            {
                savedServers.Insert(0, "localhost");
            }

            // Populate dropdown with unique servers
            foreach (var server in savedServers.Distinct())
            {
                if (!string.IsNullOrWhiteSpace(server))
                {
                    serverListBox.Items.Add(server);
                }
            }

            // Select localhost by default if it exists
            if (serverListBox.Items.Contains("localhost"))
            {
                serverListBox.SelectedItem = "localhost";
            }
            else if (serverListBox.Items.Count > 0)
            {
                serverListBox.SelectedIndex = 0;
            }

            UpdateSelectedServersGlobal();
        }

        private void UpdateSelectedServersGlobal()
        {
            // Set the VBR server name from the selected item
            if (serverListBox.SelectedItem != null)
            {
                CGlobals.VBRServerName = serverListBox.SelectedItem.ToString();
                CGlobals.REMOTEHOST = serverListBox.SelectedItem.ToString();
            }
            else if (serverListBox.Items.Count > 0)
            {
                // If nothing selected but items exist, use first item
                CGlobals.VBRServerName = serverListBox.Items[0].ToString();
                CGlobals.REMOTEHOST = serverListBox.Items[0].ToString();
            }
            else
            {
                // Fallback to localhost
                CGlobals.VBRServerName = "localhost";
                CGlobals.REMOTEHOST = "localhost";
            }

            // Set REMOTEEXEC flag if not localhost
            CGlobals.REMOTEEXEC = !CGlobals.VBRServerName.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        // Split from the original single SetUi(): everything here is synchronous
        // and must resolve immediately in the constructor (title, mode-check-fail
        // detection, pdfCheckBox state) so the window renders correctly the first
        // time. Only the mode-check-fail dialog, PreRunCheck (which internally
        // calls the notifier's blocking wrapper), and everything after them in
        // the original method depend on being off the UI thread / on the async
        // notifier primitives - that part moves to SetUiAsync, run from Loaded
        // instead of the constructor.
        //
        // NOTE: preserved verbatim from the real WPF file - SetUiSync() runs
        // before InitializeServerList() in the constructor, so the
        // hasRemoteServers scan below always sees an empty serverListBox, and
        // (when it doesn't fail) "this.Title = modeCheckResult;" immediately
        // overwrites the "Remote Mode" title set a few lines above. Both are
        // pre-existing bugs in the original file, not introduced by this port -
        // left intact rather than silently fixed.
        private void SetUiSync()
        {
            this.SetImportRelease();

            string modeCheckResult = this.functions.ModeCheck();

            if (modeCheckResult == "fail")
            {
                // If remote servers are configured, don't exit — let user select product type
                bool hasRemoteServers = false;
                foreach (var item in serverListBox.Items)
                {
                    if (!item.ToString().Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRemoteServers = true;
                        break;
                    }
                }

                if (hasRemoteServers)
                {
                    this.Title = "Veeam Health Check - Remote Mode";
                    CGlobals.Logger.Info("No local Veeam detected, but remote servers configured.", false);
                }
                else
                {
                    _modeCheckFailed = true;
                    return;
                }
            }

            this.Title = modeCheckResult;
            if (CGlobals.IsVb365 && CGlobals.IsVbr)
            {
                pdfCheckBox.IsEnabled = false;
                ToolTip.SetTip(pdfCheckBox, "PDF Export not available when both VB365 & VBR are detected on the same machine.");
            }
        }

        private async Task SetUiAsync()
        {
            if (_modeCheckFailed)
            {
                string errorMessage = "No Veeam Software detected on this machine.\n\n" +
                                     "This tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 (VB365) to be installed.\n\n" +
                                     "To connect to a remote Veeam server:\n" +
                                     "1. Close this window\n" +
                                     "2. Run from command line with: VeeamHealthCheck.exe /remote /host=your-vbr-server\n\n" +
                                     "For more information, see the documentation.";

                await CGlobals.Notifier.ShowErrorAsync(errorMessage, "Veeam Software Not Detected");

                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                return;
            }

            // PreRunCheck() stays synchronous (Part 1) but calls the notifier's
            // blocking wrapper (IUiNotifier.Confirm/ShowError) internally.
            // Calling that directly from the UI thread would deadlock, so it's
            // moved off the UI thread here, same as AcceptButton_click below.
            await Task.Run(() => this.functions.PreRunCheck());

            this.SetUiText();
            scrubBox.IsChecked = true;
            RescanBox.IsChecked = false;
            Console.WriteLine("Value: " + VbrLocalizationHelper.GuiRescanHosts);
            this.hideProgressBar();
            run.IsEnabled = false;
        }

        private void SetImportRelease()
        {
            importButton.IsEnabled = false;
            importButton.Width = 0;
        }

        private void SetImportDebug()
        {
            importButton.IsEnabled = true;
            importButton.Width = 100;
        }

        #region UI Functions

        private void SetUiText()
        {
            this.InsHeader.Text = VbrLocalizationHelper.GuiInstHeader;
            this.line1.Text = VbrLocalizationHelper.GuiInstLine1;
            this.line2.Text = VbrLocalizationHelper.GuiInstLine2;
            this.line3.Text = VbrLocalizationHelper.GuiInstLine3;
            this.line4.Text = VbrLocalizationHelper.GuiInstLine4;
            this.line5.Text = VbrLocalizationHelper.GuiInstLine5;
            this.line6.Text = VbrLocalizationHelper.GuiInstLine6;
            this.Cav1Part1.Text = VbrLocalizationHelper.GuiInstCaveat1;
            this.Cav2.Text = VbrLocalizationHelper.GuiInstCaveat2;
            this.Cav3.Text = "*** This tool is community supported and not an officially supported Veeam product.\r\n";
            this.Cav4.Text = "**** The tool does not automatically phone home, or reach out to any network infrastructure beyond the Veeam Backup and Replication components or the Veeam Backup for 365 components if appropriate.";
            this.OptHdr.Text = VbrLocalizationHelper.GuiOptionsHeader;
            this.htmlCheckBox.Content = VbrLocalizationHelper.GuiShowHtml;
            this.scrubBox.Content = VbrLocalizationHelper.GuiSensData;
            this.explorerShowBox.Content = VbrLocalizationHelper.GuiShowFiles;
            this.pdfCheckBox.Content = "Export PDF";
            // this.pptxCheckBox.Content = "Export PowerPoint";
            this.clearCredsCheckBox.Content = "Clear Saved Credentials";
            this.outPath.Text = VbrLocalizationHelper.GuiOutPath;
            this.termsBtn.Content = VbrLocalizationHelper.GuiAcceptButton;
            this.run.Content = VbrLocalizationHelper.GuiRunButton;
            this.importButton.Content = VbrLocalizationHelper.GuiImportButton;
            this.RescanBox.Content = VbrLocalizationHelper.GuiRescanHosts;

            this.SetPathBoxText(CVariables.outDir);
            CGlobals.desiredPath = CVariables.outDir;
        }

        private void SetPathBoxText(string text)
        {
            pathBox.Text = text;
        }

        // pBar deliberately never gets IsVisible=false - WPF's Visibility.Hidden
        // (what this replaces) keeps the control's layout slot reserved so the
        // progress bar area doesn't reflow when hidden; only Opacity/hit-testing
        // toggle. progressText mirrors WPF's Visibility.Collapsed, which does
        // remove it from layout - IsVisible is the correct match there.
        private void hideProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                // run.IsEnabled = true;
                pBar.Opacity = 0;
                pBar.IsHitTestVisible = false;
                progressText.IsVisible = false;
            });
        }

        private void showProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                run.IsEnabled = false;
                pBar.Opacity = 1;
                pBar.IsHitTestVisible = true;
                progressText.IsVisible = true;
            });
        }
        #endregion

        #region Buttons

        private void Import_click(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Import");
            CGlobals.IMPORT = true;
            this.DisableGuiAndStartProgressBar();
            this.Run(true);
        }

        private void run_Click(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Run");

            // Ensure the selected server is set before running
            UpdateSelectedServersGlobal();

            if (!this.functions.VerifyPath())
            {
                _ = CGlobals.Notifier.ShowErrorAsync("Error: Failed to validate desired output path. Please try a different path.", "Error");
            }

            if (this.functions.VerifyPath())
            {
                this.DisableGuiAndStartProgressBar();
                this.Run(false);
            }
        }

        // Ported verbatim from the real WPF file - no try/catch here (the
        // reference draft for this task invented one, plus a ReportRunFailure
        // method, that don't exist in reality). StartPrimaryFunctions() is
        // fully synchronous (Part 1); if it throws, the antecedent task
        // faults, but the ContinueWith below still runs by default (it only
        // skips on TaskContinuationOptions.OnlyOnRanToCompletion, which this
        // doesn't specify), so hideProgressBar() still executes exactly like
        // the original.
        private void Run(bool import)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                this.functions.StartPrimaryFunctions();
                this.UpdateCollectionStatusText();
                this.OfferMonitorSetupIfNeeded();
                this.ShowCollectionWarningsIfAny();
                Environment.Exit(0);
            }).ContinueWith(t =>
            {
                this.hideProgressBar();
            });
        }

        private void UpdateCollectionStatusText()
        {
            var failed = CGlobals.CollectionManifest?.Where(e => !e.Success).ToList();
            if (failed != null && failed.Count > 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    progressText.Text = $"Collection complete — {failed.Count} collector warning(s)";
                    progressText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
                });
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    progressText.Text = "Collection complete";
                    progressText.Foreground = new SolidColorBrush(Color.FromRgb(0x5c, 0xb8, 0x5c));
                });
            }
        }

        private void ShowCollectionWarningsIfAny()
        {
            var failed = CGlobals.CollectionManifest?.Where(e => !e.Success).ToList();
            if (failed != null && failed.Count > 0)
            {
                var names = string.Join(", ", failed.Select(e => e.Name));
                Dispatcher.UIThread.Post(async () =>
                {
                    await CGlobals.Notifier.ShowErrorAsync(
                        $"{failed.Count} collector(s) reported errors: {names}\n\nThe report may have incomplete sections. Check the log for details.",
                        "Collection Warnings");
                });
            }
        }

        private void DisableGuiAndStartProgressBar()
        {
            this.DisableButtons();
            this.showProgressBar();
        }

        private void DisableButtons()
        {
            explorerShowBox.IsEnabled = false;
            htmlCheckBox.IsEnabled = false;
            pdfCheckBox.IsEnabled = false;
            scrubBox.IsEnabled = false;
            termsBtn.IsEnabled = false;
            importButton.IsEnabled = false;
            pathBox.IsEnabled = false;
            clearCredsCheckBox.IsEnabled = false;
            serverTextBox.IsEnabled = false;
            addServerBtn.IsEnabled = false;
            removeServerBtn.IsEnabled = false;
            clearServersBtn.IsEnabled = false;
            serverListBox.IsEnabled = false;
            productTypeSelector.IsEnabled = false;
            RescanBox.IsEnabled = false;
        }

        // AcceptTerms() stays synchronous (Part 1) - but this handler runs
        // directly on the UI thread, so calling its blocking wrapper form here
        // would deadlock. Task.Run moves it off the UI thread first, exactly
        // like SetUiAsync's PreRunCheck call above.
        private async void AcceptButton_click(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Accept");
            run.IsEnabled = await Task.Run(() => this.functions.AcceptTerms());
        }

        #endregion

        #region Check Boxes
        private void HandleCheck(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Scrub = true");
            CGlobals.Scrub = true;
        }

        private void htmlChecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Open HTML = true");
            CGlobals.OpenHtml = true;
        }

        private void htmlUnchecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Open HTML = false");
            CGlobals.OpenHtml = false;
        }

        private void HandleUnchecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Scrub = false");
            CGlobals.Scrub = false;
        }

        // Retained dead code from the real WPF file: not wired to any control
        // event in either the original XAML or the Task 10 AXAML (scrubBox has
        // no IsThreeState/Indeterminate wiring in either) - pre-existing, not
        // introduced by this port.
        private void HandleThirdState(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Scrub 3rd state = false");
            CGlobals.Scrub = false;
        }

        private void explorerShowBox_Checked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Show Explorer = true");
            CGlobals.OpenExplorer = true;
        }

        private void explorerShowBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Show Explorer = false");
            CGlobals.OpenExplorer = false;
        }

        private void pdfCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Export PDF = true");
            CGlobals.EXPORTPDF = true;
        }

        private void pdfCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Export PDF = false");
            CGlobals.EXPORTPDF = false;
        }

        private void clearCredsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Clear Stored Creds = true");
            CGlobals.ClearStoredCreds = true;
        }

        private void clearCredsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Clear Stored Creds = false");
            CGlobals.ClearStoredCreds = false;
        }

        private void RescanBox_Checked(object sender, RoutedEventArgs e)
        {
            CGlobals.RescanHosts = true;
            this.functions.LogUIAction("Rescan Hosts = true");
        }

        private void RescanBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CGlobals.RescanHosts = false;
            this.functions.LogUIAction("Rescan Hosts = false");
        }

        #endregion

        private void pathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CGlobals.Logger.Info("Changing path from " + CGlobals.desiredPath + " to " + pathBox.Text);
            CGlobals.desiredPath = pathBox.Text;
        }

        // Guard added (not present in the real WPF file): Avalonia's generated
        // InitializeComponent() can raise SelectionChanged while assigning
        // named fields as the tree is built, so daysSelector could still be
        // null on first raise. Same defensive pattern already used by
        // productTypeSelector_SelectionChanged/notifTypeBox_SelectionChanged
        // below - not testable on macOS, but zero behavior change once
        // daysSelector is non-null.
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (daysSelector == null) return;
            switch (daysSelector.SelectedIndex)
            {
                case 0:
                    this.SetReportDays(7);
                    break;
                case 1:
                    this.SetReportDays(30);
                    break;
                case 2:
                    this.SetReportDays(90);
                    break;
                default:
                    this.SetReportDays(7);
                    break;
            }
        }

        private void SetReportDays(int days)
        {
            CGlobals.ReportDays = days;
            this.functions.LogUIAction("Interval set to " + CGlobals.ReportDays);
        }

        #region Server Management

        private void addServerBtn_Click(object sender, RoutedEventArgs e)
        {
            string serverName = serverTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(serverName))
            {
                _ = CGlobals.Notifier.ShowErrorAsync("Please enter a server name.", "Input Required");
                return;
            }

            // Check if server already exists in list
            foreach (var item in serverListBox.Items)
            {
                if (item.ToString().Equals(serverName, StringComparison.OrdinalIgnoreCase))
                {
                    _ = CGlobals.Notifier.ShowErrorAsync($"Server '{serverName}' is already in the list.", "Duplicate Server");
                    serverTextBox.Text = string.Empty;
                    return;
                }
            }

            // Add server to list
            serverListBox.Items.Add(serverName);
            serverTextBox.Text = string.Empty;
            UpdateSelectedServersGlobal();

            this.functions.LogUIAction($"Added server: {serverName}");
        }

        private async void removeServerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (serverListBox.SelectedItem == null)
            {
                await CGlobals.Notifier.ShowErrorAsync("Please select a server to remove.", "No Selection");
                return;
            }

            string selectedServer = serverListBox.SelectedItem.ToString();

            // Don't allow removing localhost if it's the only item
            if (serverListBox.Items.Count == 1 && selectedServer.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                await CGlobals.Notifier.ShowErrorAsync("Cannot remove the last server. At least one server must remain in the list.", "Cannot Remove");
                return;
            }

            // Ask for confirmation if this server has stored credentials
            bool hasCredentials = CredentialStore.Get(selectedServer) != null;
            if (hasCredentials)
            {
                bool confirmed = await CGlobals.Notifier.ConfirmAsync(
                    $"Remove '{selectedServer}' from the list?\n\nThis will also delete any stored credentials for this server.",
                    "Confirm Remove");

                if (!confirmed)
                {
                    return;
                }
            }

            // Remove from UI list
            serverListBox.Items.Remove(serverListBox.SelectedItem);

            // Remove credentials if they exist
            if (hasCredentials)
            {
                CredentialStore.Remove(selectedServer);
            }

            UpdateSelectedServersGlobal();

            this.functions.LogUIAction($"Removed server: {selectedServer}");
        }

        private async void clearServersBtn_Click(object sender, RoutedEventArgs e)
        {
            if (serverListBox.Items.Count == 0)
            {
                await CGlobals.Notifier.ShowErrorAsync("Server list is already empty.", "Empty List");
                return;
            }

            bool confirmed = await CGlobals.Notifier.ConfirmAsync(
                "Are you sure you want to clear all servers from the list?",
                "Confirm Clear");

            if (confirmed)
            {
                serverListBox.Items.Clear();

                // Add localhost back if VBR is installed locally
                if (CGlobals.IsVbrInstalled)
                {
                    serverListBox.Items.Add("localhost");
                }

                UpdateSelectedServersGlobal();
                this.functions.LogUIAction("Cleared all servers from list");
            }
        }

        // Guard added (not present in the real WPF file): same
        // InitializeComponent()-timing rationale as ComboBox_SelectionChanged
        // above - serverListBox could still be null on a SelectionChanged
        // raised during tree construction.
        private void serverListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (serverListBox == null) return;

            UpdateSelectedServersGlobal();

            if (serverListBox.SelectedItem != null)
            {
                this.functions.LogUIAction($"Selected server: {serverListBox.SelectedItem}");
            }
        }

        private void productTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (productTypeSelector == null) return;
            switch (productTypeSelector.SelectedIndex)
            {
                case 0: CGlobals.TargetProductType = TargetProduct.Auto; break;
                case 1: CGlobals.TargetProductType = TargetProduct.Vbr; break;
                case 2: CGlobals.TargetProductType = TargetProduct.Vb365; break;
                case 3: CGlobals.TargetProductType = TargetProduct.Both; break;
            }
            this.functions.LogUIAction("Product type set to " + CGlobals.TargetProductType);
        }

        #endregion

        #region Monitor Integration

        private void InitializeMonitorStatus()
        {
            bool bundled = CVhcMonitorIntegration.IsExePresentInBundle();
            bool installed = CVhcMonitorIntegration.IsInstalled();
            bool taskActive = CVhcMonitorIntegration.IsTaskRegistered();

            if (!bundled)
            {
                monitorStatusText.Text = "Not bundled";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
                monitorQuickSetupBtn.IsEnabled = false;
                monitorVhcSetupBtn.IsEnabled = false;
                monitorRunBtn.IsEnabled = false;
            }
            else if (!installed || !taskActive)
            {
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = false;
            }
            else
            {
                string version = CVhcMonitorIntegration.GetInstalledVersion();
                monitorStatusText.Text = $"Running ({version})";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x5c, 0xb8, 0x5c));
                monitorQuickSetupBtn.Content = "Reconfigure";
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = true;

                var status = CVhcMonitorIntegration.GetLastRunStatus();
                if (status != null)
                {
                    monitorLastRunText.Text = $"Last run: {status.Timestamp:g} — {status.Summary}";
                    monitorLastRunText.IsVisible = true;
                }
            }
        }

        private (string notifType, string notifUrl, string minSeverity) GetNotifSettings()
        {
            string notifType = (notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "ntfy";
            string notifUrl = notifUrlBox.Text?.Trim() ?? string.Empty;
            string minSeverity = (notifSeverityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "warning";
            return (notifType, notifUrl, minSeverity);
        }

        private void monitorQuickSetupBtn_Click(object sender, RoutedEventArgs e)
        {
            string server = serverListBox.SelectedItem?.ToString() ?? CGlobals.VBRServerName;
            var creds = CredentialStore.Get(server);
            string username = creds?.Username ?? string.Empty;
            string password = creds?.Password ?? string.Empty;

            if (string.IsNullOrEmpty(username))
            {
                _ = CGlobals.Notifier.ShowErrorAsync(
                    $"No stored credentials found for '{server}'.\nPlease add credentials by running a health check first, or use the credential prompt.",
                    "Credentials Required");
                return;
            }

            if (!CVhcMonitorIntegration.IsExePresentInBundle())
            {
                _ = CGlobals.Notifier.ShowErrorAsync("vhc-monitor.exe not found in the VHC installation directory.", "Monitor Not Found");
                return;
            }

            monitorQuickSetupBtn.IsEnabled = false;
            monitorStatusText.Text = "Installing...";

            var (notifType, notifUrl, minSeverity) = this.GetNotifSettings();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    CVhcMonitorIntegration.Install(server, username, password, notifType, notifUrl, minSeverity);
                    Dispatcher.UIThread.Post(this.InitializeMonitorStatus);
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error($"Monitor setup failed: {ex.Message}", false);
                    Dispatcher.UIThread.Post(() =>
                    {
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xd9, 0x53, 0x4f));
                        monitorQuickSetupBtn.IsEnabled = true;
                    });
                }
            });
        }

        private void monitorVhcSetupBtn_Click(object sender, RoutedEventArgs e)
        {
            monitorVhcSetupBtn.IsEnabled = false;
            monitorStatusText.Text = "Installing from VHC data...";

            var (notifType, notifUrl, minSeverity) = this.GetNotifSettings();

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    CVhcMonitorIntegration.InstallFromVhcData(notifType, notifUrl, minSeverity);
                    Dispatcher.UIThread.Post(this.InitializeMonitorStatus);
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error($"Monitor VHC-assisted setup failed: {ex.Message}", false);
                    Dispatcher.UIThread.Post(() =>
                    {
                        monitorStatusText.Text = "Setup failed — check log";
                        monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xd9, 0x53, 0x4f));
                        monitorVhcSetupBtn.IsEnabled = true;
                    });
                }
            });
        }

        private void monitorRunBtn_Click(object sender, RoutedEventArgs e)
        {
            monitorRunBtn.IsEnabled = false;
            monitorLastRunText.Text = "Running...";
            monitorLastRunText.IsVisible = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                var (exitCode, output) = CVhcMonitorIntegration.RunNow();
                Dispatcher.UIThread.Post(() =>
                {
                    this.InitializeMonitorStatus();
                    monitorRunBtn.IsEnabled = CVhcMonitorIntegration.IsTaskRegistered();
                });
            });
        }

        private void notifTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (notifUrlBox == null) return;
            string type = (notifTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ntfy";
            notifUrlBox.Tag = type switch
            {
                "Teams" => "https://org.webhook.office.com/...",
                "Slack" => "https://hooks.slack.com/services/...",
                "PagerDuty" => "https://events.pagerduty.com/...",
                _ => "https://ntfy.sh/your-topic"
            };
        }

        private void OfferMonitorSetupIfNeeded()
        {
            if (!CVhcMonitorIntegration.IsExePresentInBundle()) return;
            if (CVhcMonitorIntegration.IsTaskRegistered()) return;

            Dispatcher.UIThread.Post(() =>
            {
                monitorVhcSetupBtn.IsEnabled = true;
                monitorLastRunText.Text = "Health check complete — click 'Setup from VHC' to configure continuous monitoring with auto-detected server settings.";
                monitorLastRunText.IsVisible = true;
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
            });
        }

        #endregion
    }
}
