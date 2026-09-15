// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using VeeamHealthCheck.Functions.AboutDialog;
using VeeamHealthCheck.Functions.ManageServers;
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

        private const string LocalhostName = "localhost";

        // Resolved inside SetUiSync(), right after ModeCheck() runs - see that
        // method's comments for why it can't be resolved any earlier. Caching it in
        // a field is what lets SetUiSync's own hasRemoteServers scan see the real
        // list instead of a not-yet-populated control. This is the PERSISTED list:
        // it never contains localhost on an injecting machine.
        private List<string> _persistedServers = new();

        // What the picker actually shows: _persistedServers plus injected localhost.
        // Kept as a field rather than read back off serverSelector.ItemsSource, because
        // casting ItemsSource back to a concrete collection type is a runtime cast that
        // silently depends on what InitializeServerList happened to assign.
        private List<string> _displayServers = new();

        // The single predicate that owns localhost policy. Injection, pinning and the
        // seed filter all derive from it; writing those three rules independently is how
        // they drift apart and produce a blank picker on a VB365-only machine.
        //
        // IsVbrInstalled alone is not enough: it is set only for a running
        // Veeam.Backup.Service (CClientFunctions.cs:106), so it is false on a VB365-only
        // box - which can still legitimately hold a localhost credential, because
        // RunSaveCredsFlow defaults its host to "localhost".
        private static bool LocalhostIsInjected =>
            CGlobals.IsVbrInstalled || CGlobals.IsVb365;

        public VhcGui()
        {
            // Captured before InitializeComponent() touches it - see the resync
            // immediately below for why.
            int reportDaysAtStartup = CGlobals.ReportDays;

            InitializeComponent();

            // days7's IsChecked="True" in XAML raises Checked synchronously during the
            // InitializeComponent() call above, unconditionally running
            // PeriodRadio_Checked -> SetReportDays(7) -> CGlobals.ReportDays = 7. That
            // silently stomps a /days:N CLI value set before this window was ever
            // constructed (CArgsParser.cs's /days:7|30|90|12 cases all run before
            // LaunchUi gets anywhere near `new VhcGui()`). The old period-selection
            // control this replaced did not have this problem: its SelectionChanged
            // handler guarded on its own field being null, and that field was not yet
            // assigned to itself at the moment its own initial SelectionChanged fired -
            // but a RadioButton's `sender` on its OWN Checked event is never null, so
            // the same guard shape does not carry over to this control. Restore the
            // value captured above, now that construction has settled.
            switch (reportDaysAtStartup)
            {
                case 30:
                    days30.IsChecked = true; // re-fires PeriodRadio_Checked, restoring 30
                    break;
                case 90:
                    days90.IsChecked = true; // re-fires PeriodRadio_Checked, restoring 90
                    break;
                case 7:
                    break; // already correct; days7 is already checked
                default:
                    // No pill represents this value (e.g. /days:12) - the segmented
                    // control only ever offers 7/30/90, the same three the ComboBox it
                    // replaced offered. Restore the value directly so the report still
                    // uses it; the UI is left showing days7 checked, the same cosmetic
                    // mismatch the ComboBox's SelectedIndex="0" default showed for this
                    // same case.
                    //
                    // Goes through SetReportDays rather than a raw assignment so the log
                    // also gets a correcting "Interval set to N" entry - otherwise the
                    // unconditional SetReportDays(7) that already ran during
                    // InitializeComponent() above leaves "Interval set to 7" as the log's
                    // last word on this even though CGlobals.ReportDays ends up N.
                    this.SetReportDays(reportDaysAtStartup);
                    break;
            }

            // Establishes SelectTab as the single source of truth for the Ad-hoc-tab
            // default (XAML alone encodes it three separate ways: AdHocTabPanel's
            // implicit IsVisible=true, AdHocTabButton's tab-active class, and
            // termsCheckBox/run's implicit default Opacity/IsHitTestVisible/Focusable) -
            // without this call, a future edit to one could silently drift from the
            // others, and termsCheckBox/run would start Focusable=true from XAML alone.
            SelectTab(isAdHoc: true);

            ThemeToggleButton.Content = ThemeLabelFor(NextThemeVariant(Application.Current!.RequestedThemeVariant));

            // AvaloniaUiNotifier passes this as the ShowDialog owner. Set it
            // here (rather than waiting for Task 12's App.axaml.cs) because
            // termsCheckBox_Checked's Task.Run(AcceptTerms) can raise a dialog
            // before that wiring exists.
            AvaloniaHost.MainWindow = this;

            this.SetUiSync();
            pathBox.IsEnabled = true;
            this.InitializeServerList();
            this.InitializeMonitorStatus();

            // pdfCheckBox.IsEnabled = false;

            this.Loaded += async (s, e) => await this.SetUiAsync();
        }

        // Safe to call from the constructor (e.g. InitializeMonitorStatus above, before Show()):
        // resolution reaches Application.Resources at construction time, not tree-attach time.
        // Only holds because App.axaml keeps the four Status*Brush keys flat, outside
        // ResourceDictionary.ThemeDictionaries - moving them into per-theme dictionaries would
        // make this lookup theme-variant-sensitive before a variant can be resolved pre-attachment.
        private IBrush GetStatusBrush(string resourceKey) => (IBrush)this.FindResource(resourceKey);

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var app = Application.Current!;
            ThemeVariant next = NextThemeVariant(app.RequestedThemeVariant);

            app.RequestedThemeVariant = next;
            CAppSettings.Set(CThemePreference.FromVariant(next));

            // Labels the button with what a FURTHER click will do from this new
            // current state - not this state itself. A toggle button showing its
            // own current state ("you are in Light mode") reads as a status
            // indicator, not a control; showing the target of the next click is
            // the standard convention and is what actually tells the user what
            // pressing it does.
            ThemeToggleButton.Content = ThemeLabelFor(NextThemeVariant(next));
        }

        private static ThemeVariant NextThemeVariant(ThemeVariant current) => current switch
        {
            var v when v == ThemeVariant.Dark => ThemeVariant.Light,
            var v when v == ThemeVariant.Light => ThemeVariant.Default,
            _ => ThemeVariant.Dark, // System (Default) -> Dark
        };

        private static string ThemeLabelFor(ThemeVariant variant) =>
            variant == ThemeVariant.Dark ? "🌙 Dark" :
            variant == ThemeVariant.Light ? "☀ Light" : "🖥 System";

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            await new AboutDisclaimerDialog().ShowDialog(this);
        }

        private void AdHocTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(isAdHoc: true);

        private void MonitoringTabButton_Click(object sender, RoutedEventArgs e) => SelectTab(isAdHoc: false);

        // termsCheckBox/run belong to the ad-hoc workflow and follow the active tab; the
        // progress stack does not (see the bottom-bar comment in VhcGui.axaml) - a
        // real run can take minutes and must stay visible from either tab.
        //
        // termsCheckBox and run each sit alone in their own Auto column of the bottom
        // bar's Grid. IsVisible=false removes a control from layout entirely, so
        // an Auto column with nothing else to measure collapses to zero width -
        // the same reflow this plan already fixed for progressText, just
        // triggered by a tab switch instead of a run starting/stopping, and
        // horizontal instead of vertical. Toggling Opacity/IsHitTestVisible
        // instead (matching pBar's existing pattern) keeps both Auto columns at
        // their reserved width always, so the progress stack's position never
        // shifts when switching tabs.
        private void SelectTab(bool isAdHoc)
        {
            AdHocTabPanel.IsVisible = isAdHoc;
            MonitoringTabPanel.IsVisible = !isAdHoc;

            // Opacity/IsHitTestVisible alone block pointer input, not keyboard focus -
            // without Focusable=false too, Tab navigation could still land on and
            // activate the invisible button from the wrong tab.
            termsCheckBox.Opacity = isAdHoc ? 1 : 0;
            termsCheckBox.IsHitTestVisible = isAdHoc;
            termsCheckBox.Focusable = isAdHoc;
            run.Opacity = isAdHoc ? 1 : 0;
            run.IsHitTestVisible = isAdHoc;
            run.Focusable = isAdHoc;

            AdHocTabButton.Classes.Set("tab-active", isAdHoc);
            MonitoringTabButton.Classes.Set("tab-active", !isAdHoc);
        }

        // preserveSelection distinguishes the two callers, and the distinction is
        // load-bearing. At startup there is no selection to keep and localhost-first is
        // the right default. After the dialog commits, silently reasserting that default
        // would move a user who was sitting on vbr01 back to localhost - flipping
        // REMOTEEXEC to false and pointing the next run at the local box - even if they
        // pressed Done having changed nothing. Both tabs read that selection
        // (monitorQuickSetupBtn_Click), so it must survive a repopulate.
        private void InitializeServerList(bool preserveSelection = false)
        {
            string previous = preserveSelection
                ? serverSelector.SelectedItem?.ToString()
                : null;

            // The persisted list is authoritative; GetAllServers() is consulted only by
            // LoadOrSeedServers' one-time seed and the post-commit refresh.
            var display = new List<string>();

            if (LocalhostIsInjected)
            {
                display.Add(LocalhostName);
            }

            // Case-insensitive de-dup, matching the .Distinct() this method used to
            // apply. LoadOrSeedServers already strips localhost when it is injected;
            // this is belt and braces so a stray entry can never render twice.
            foreach (var server in _persistedServers)
            {
                if (!display.Any(s => s.Equals(server, StringComparison.OrdinalIgnoreCase)))
                {
                    display.Add(server);
                }
            }

            _displayServers = display;
            serverSelector.ItemsSource = _displayServers;

            // Restore the prior selection when it survived the commit; otherwise fall
            // back to localhost-first, then first-entry - the precedence this method has
            // always used at startup.
            string keep = previous == null
                ? null
                : _displayServers.FirstOrDefault(
                    s => s.Equals(previous, StringComparison.OrdinalIgnoreCase));

            if (keep != null)
            {
                serverSelector.SelectedItem = keep;
            }
            else if (_displayServers.Any(s => s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase)))
            {
                serverSelector.SelectedItem = _displayServers.First(
                    s => s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
            }
            else if (_displayServers.Count > 0)
            {
                serverSelector.SelectedIndex = 0;
            }

            UpdateSelectedServersGlobal();
        }

        private void UpdateSelectedServersGlobal()
        {
            if (serverSelector.SelectedItem != null)
            {
                CGlobals.VBRServerName = serverSelector.SelectedItem.ToString();
                CGlobals.REMOTEHOST = serverSelector.SelectedItem.ToString();
            }
            else if (_displayServers.Count > 0)
            {
                CGlobals.VBRServerName = _displayServers[0];
                CGlobals.REMOTEHOST = CGlobals.VBRServerName;
            }
            else
            {
                CGlobals.VBRServerName = LocalhostName;
                CGlobals.REMOTEHOST = LocalhostName;
            }

            CGlobals.REMOTEEXEC = !CGlobals.VBRServerName.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase);
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
        // NOTE: the hasRemoteServers scan used to always see an empty list
        // (it read the old server list control, and SetUiSync() ran before
        // InitializeServerList() populated it), so the hasRemoteServers branch below - and the title
        // it sets - could never actually run. Task 12 fixed the scan to read
        // _persistedServers, resolved above, instead of that not-yet-populated
        // control - which makes the branch reachable for the first time. That
        // exposed a second, previously-dormant bug: "this.Title = modeCheckResult;"
        // a few lines below unconditionally overwrote whatever title was just
        // set, including "Remote Mode", with the literal string "fail". Guarded
        // below so "Remote Mode" survives.
        //
        // Making this branch live also means SetUiAsync() now reaches
        // Task.Run(() => PreRunCheck()) on a machine with no local Veeam. That is a
        // no-op: modeCheckResult can only be "fail" when both CGlobals.IsVbr and
        // CGlobals.IsVb365 are false (CClientFunctions.cs:130), and both of
        // PreRunCheck's dialog branches are gated on one of those flags
        // (CClientFunctions.cs:36, :72).
        private void SetUiSync()
        {
            this.SetImportRelease();

            string modeCheckResult = this.functions.ModeCheck();

            // Resolved HERE, and not one line earlier in the constructor. ModeCheck() is
            // the only thing that populates CGlobals.IsVbrInstalled / IsVb365 on the GUI
            // path, so evaluating LocalhostIsInjected before this call reads both as
            // false on every machine - which would make excludeLocalhost permanently
            // false, persist localhost into the one-time seed on injecting machines, and
            // render the read-time filter inert. The same predicate evaluates correctly
            // in manageServersBtn_Click (which runs later), so getting this wrong makes
            // two calls to one function disagree.
            //
            // Placed before the fail branch below so hasRemoteServers still sees the
            // resolved list. CredentialStore.GetAllServers() is safe at any point - a
            // static constructor initialises its cache (CredentialStore.cs:34-36).
            _persistedServers = CAppSettings.LoadOrSeedServers(
                CredentialStore.GetAllServers(),
                excludeLocalhost: LocalhostIsInjected);

            if (modeCheckResult == "fail")
            {
                // Reads the resolved list rather than a control that has not been
                // populated yet. The old scan iterated an empty list control, because
                // SetUiSync() runs before InitializeServerList() - so this branch could
                // never fire, and a machine with no local Veeam but remote servers
                // configured always got the abort the branch exists to prevent.
                //
                // The localhost filter is kept rather than relying on the "localhost is
                // never persisted" invariant: on a non-injecting machine localhost IS
                // legitimately persisted, and counting it as a remote server would put a
                // local-only box into Remote Mode.
                bool hasRemoteServers = _persistedServers
                    .Any(s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));

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

            if (modeCheckResult != "fail")
            {
                this.Title = modeCheckResult;
            }

            if (CGlobals.IsVb365 && CGlobals.IsVbr)
            {
                pdfCheckBox.IsEnabled = false;
                ToolTip.SetTip(pdfCheckBox, "PDF Export not available when both VB365 & VBR are detected on the same machine.");
            }

            // Originally the tail of the single synchronous SetUi(), which ran
            // entirely before the WPF window was ever shown. SetUiAsync() below
            // now sets these again after Loaded, but doing it here too closes
            // the window between first paint and SetUiAsync's completion during
            // which the axaml's declared state (run enabled, pBar spinning at
            // full opacity) would otherwise be visible.
            run.IsEnabled = false;
            this.hideProgressBar();
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
            // moved off the UI thread here, same as termsCheckBox_Checked below.
            await Task.Run(() => this.functions.PreRunCheck());

            this.SetUiText();
            scrubBox.IsChecked = true;
            RescanBox.IsChecked = false;
            Console.WriteLine("Value: " + VbrLocalizationHelper.GuiRescanHosts);
            this.hideProgressBar();
            run.IsEnabled = false;
        }

        // Width=0 alone was enough when importButton was a standalone element, but in
        // the header row it sits in its own Auto column with Margin="0,0,12,0", and a
        // zero-width control still contributes its margin - leaving a 12px gap next to
        // the (already hidden) divider in every shipped build. IsVisible=false zeroes
        // the whole DesiredSize including margin, collapsing the Auto column completely.
        // The Width lines stay so SetImportDebug still gets its 100px button.
        private void SetImportRelease()
        {
            importButton.IsEnabled = false;
            importButton.Width = 0;
            importButton.IsVisible = false;
            importDivider.IsVisible = false;
        }

        private void SetImportDebug()
        {
            importButton.IsEnabled = true;
            importButton.Width = 100;
            importButton.IsVisible = true;
            importDivider.IsVisible = true;
        }

        #region UI Functions

        private void SetUiText()
        {
            this.OptHdr.Text = VbrLocalizationHelper.GuiOptionsHeader;
            this.htmlCheckBox.Content = VbrLocalizationHelper.GuiShowHtml;
            this.scrubBox.Content = VbrLocalizationHelper.GuiSensData;
            this.explorerShowBox.Content = VbrLocalizationHelper.GuiShowFiles;
            this.pdfCheckBox.Content = "Export PDF";
            // this.pptxCheckBox.Content = "Export PowerPoint";
            this.clearCredsCheckBox.Content = "Clear Saved Credentials";
            this.outPath.Text = VbrLocalizationHelper.GuiOutPath;
            this.termsCheckBox.Content = VbrLocalizationHelper.GuiAcceptButton;
            this.run.Content = VbrLocalizationHelper.GuiRunButton;
            this.importButton.Content = VbrLocalizationHelper.GuiImportButton;
            this.RescanBox.Content = VbrLocalizationHelper.GuiRescanHosts;
            this.days7.Content = VbrLocalizationHelper.GuiPeriod7;
            this.days30.Content = VbrLocalizationHelper.GuiPeriod30;
            this.days90.Content = VbrLocalizationHelper.GuiPeriod90;

            this.SetPathBoxText(CVariables.outDir);
            CGlobals.desiredPath = CVariables.outDir;
            ToolTip.SetTip(this.browseFolderBtn, VbrLocalizationHelper.GuiBrowseFolderTooltip);

            this.serverLabel.Text = VbrLocalizationHelper.GuiServerLabel;
            ToolTip.SetTip(this.manageServersBtn, VbrLocalizationHelper.GuiManageServersTooltip);
        }

        private void SetPathBoxText(string text)
        {
            pathBox.Text = text;
        }

        // Both pBar and progressText hide via Opacity, not IsVisible. Opacity is a
        // paint-time effect only - it never changes an element's measured size, so
        // nothing in the bottom bar can reflow when progress shows or hides.
        // IsVisible=false would zero progressText's DesiredSize and let the bottom
        // bar's layout change on every run start/stop.
        private void hideProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                // run.IsEnabled = true;
                pBar.Opacity = 0;
                pBar.IsHitTestVisible = false;
                progressText.Opacity = 0;
            });
        }

        private void showProgressBar()
        {
            Dispatcher.UIThread.Post(() =>
            {
                run.IsEnabled = false;
                pBar.Opacity = 1;
                pBar.IsHitTestVisible = true;
                progressText.Opacity = 1;
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

        // The try/catch and ReportRunFailure() below did not exist in the real
        // WPF file when this method was first ported (an earlier reference
        // draft had invented them, and was correctly rejected at the time).
        // dev/master then landed a real fix (surface report-phase exceptions
        // instead of silently exiting 0) independently while this migration
        // was in flight; it's ported here during the merge with dev, adapted
        // to Avalonia's dialog/dispatcher idioms - see ReportRunFailure's own
        // comment below. hideProgressBar() in the ContinueWith still runs
        // either way (it only skips on TaskContinuationOptions.
        // OnlyOnRanToCompletion, which this doesn't specify).
        private void Run(bool import)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                // The whole run happens on a background task. Historically the body
                // had no try/catch, so any exception in analysis/report generation was
                // stored on the faulted task and never observed: no report was written,
                // no error surfaced, Environment.Exit(0) was skipped, and the user was
                // left staring at a hung-looking GUI. Catch here so failures are logged,
                // surfaced, and the GUI returns to a usable state. Only exit(0) on success.
                try
                {
                    this.functions.StartPrimaryFunctions();
                    this.UpdateCollectionStatusText();
                    this.OfferMonitorSetupIfNeeded();
                    this.ShowCollectionWarningsIfAny();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    this.ReportRunFailure(ex);
                }
            }).ContinueWith(t =>
            {
                this.hideProgressBar();
            });
        }

        // Runs only from Run()'s catch block, on the same background thread
        // as the rest of Run()'s Task.Factory.StartNew body. The blocking
        // CGlobals.Notifier.ShowError wrapper is called directly (unwrapped)
        // for the same reason as ShowCollectionWarningsIfAny below - safe off
        // the UI thread, and blocks until the user dismisses it so the
        // failure is actually seen before this method returns. run.IsEnabled
        // is a raw UI property mutation, not a dialog, so it still needs its
        // own Dispatcher.UIThread.Invoke, same as UpdateCollectionStatusText
        // below - it would throw/misbehave if set directly from this thread.
        private void ReportRunFailure(Exception ex)
        {
            CGlobals.Logger.Error("Run failed: " + ex.Message, true);
            CGlobals.Logger.Error("Stack trace: " + ex.StackTrace, false);
            if (ex.InnerException != null)
            {
                CGlobals.Logger.Error("Inner exception: " + ex.InnerException.Message, false);
                CGlobals.Logger.Error("Inner stack trace: " + ex.InnerException.StackTrace, false);
            }

            CGlobals.Notifier.ShowError(
                "The health check failed before a report was generated:\n\n" +
                ex.Message +
                "\n\nNo report was produced. See the log for the full stack trace.",
                "Health Check Failed");

            // Re-enable the run button so the user can retry without restarting.
            Dispatcher.UIThread.Invoke(() => run.IsEnabled = true);
        }

        // Both UpdateCollectionStatusText() and ShowCollectionWarningsIfAny() are
        // called only from the background thread inside Run()'s
        // Task.Factory.StartNew, immediately before Environment.Exit(0). The
        // real WPF file used Dispatcher.Invoke (synchronous - blocks the
        // calling thread until the UI thread finishes, and for the MessageBox
        // case, until the user dismisses it) specifically so Exit(0) couldn't
        // race ahead of the UI update / warning dialog. Dispatcher.UIThread.Post
        // is fire-and-forget and would let Exit(0) tear down the process before
        // the queued work - or the warning dialog - ever ran, silently dropping
        // the collection-warnings notification. Dispatcher.UIThread.Invoke (the
        // Avalonia analog of WPF's Dispatcher.Invoke) and the notifier's
        // blocking ShowError wrapper preserve the original blocking semantics.
        private void UpdateCollectionStatusText()
        {
            var failed = CGlobals.CollectionManifest?.Where(e => !e.Success).ToList();
            if (failed != null && failed.Count > 0)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    progressText.Text = $"Collection complete — {failed.Count} collector warning(s)";
                    progressText.Foreground = GetStatusBrush("StatusWarningBrush");
                });
            }
            else
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    progressText.Text = "Collection complete";
                    progressText.Foreground = GetStatusBrush("StatusSuccessBrush");
                });
            }
        }

        private void ShowCollectionWarningsIfAny()
        {
            var failed = CGlobals.CollectionManifest?.Where(e => !e.Success).ToList();
            if (failed != null && failed.Count > 0)
            {
                var names = string.Join(", ", failed.Select(e => e.Name));
                CGlobals.Notifier.ShowError(
                    $"{failed.Count} collector(s) reported errors: {names}\n\nThe report may have incomplete sections. Check the log for details.",
                    "Collection Warnings");
            }
        }

        private void DisableGuiAndStartProgressBar()
        {
            this.DisableButtons();
            this.showProgressBar();
        }

        // Set once and never cleared. Every control DisableButtons() touches, other than
        // run.IsEnabled (which ReportRunFailure explicitly restores on failure), stays
        // disabled for the rest of this process's life once a run or import starts:
        // success exits the process outright (Run()'s Task.Factory.StartNew body calls
        // Environment.Exit(0)), and on failure only Run is meant to work again. This flag
        // makes that pre-existing one-way-ratchet invariant checkable rather than
        // implicit, specifically so termsCheckBox_Checked's async continuation can tell
        // whether a run/import started while its own accept flow was still in flight -
        // Import_click does not wait on terms acceptance, so it can race ahead of one.
        //
        // Set HERE, synchronously inside DisableButtons() - not inside showProgressBar(),
        // which posts its own IsEnabled writes via Dispatcher.UIThread.Post and is
        // therefore NOT synchronous with the caller. A terms-accept continuation resuming
        // on the UI thread could dequeue ahead of a posted-but-not-yet-run action and
        // still observe the pre-lock state. Setting the flag directly in the synchronous
        // call path both Import_click and run_Click share (via DisableGuiAndStartProgressBar)
        // is what makes the check in termsCheckBox_Checked airtight; moving it into
        // showProgressBar (or anything else Dispatcher-posted) would silently reopen this.
        private bool _guiLockedForRun;

        private void DisableButtons()
        {
            _guiLockedForRun = true;
            explorerShowBox.IsEnabled = false;
            htmlCheckBox.IsEnabled = false;
            pdfCheckBox.IsEnabled = false;
            scrubBox.IsEnabled = false;
            termsCheckBox.IsEnabled = false;
            importButton.IsEnabled = false;
            pathBox.IsEnabled = false;
            browseFolderBtn.IsEnabled = false;
            clearCredsCheckBox.IsEnabled = false;
            serverSelector.IsEnabled = false;
            manageServersBtn.IsEnabled = false;
            productTypeSelector.IsEnabled = false;
            RescanBox.IsEnabled = false;
        }

        // Guards the programmatic revert below. A plain bool is sufficient ONLY because
        // Avalonia raises Unchecked synchronously inside the IsChecked assignment, while
        // this flag is still set - which is also why termsCheckBox_Unchecked must not be
        // async void.
        private bool _suppressTermsHandler;

        // Blocks re-entrancy while an accept flow's await is pending. Without this (and
        // the termsCheckBox.IsEnabled=false below), a fast uncheck-then-recheck during
        // that window starts a SECOND overlapping AcceptTerms() flow: two confirm
        // dialogs, and two continuations racing to write run.IsEnabled with no
        // coordination between them - whichever resolves last wins, regardless of the
        // checkbox's actual state by then, or of whether a run has since started off the
        // first flow's own (still valid) acceptance. Disabling the checkbox for the
        // duration closes this at the source: the user cannot trigger a second Checked
        // while this one is in flight, so run.IsEnabled can only ever be written by the
        // one accept flow that is allowed to be running at a time.
        private bool _termsAcceptInFlight;

        // AcceptTerms() stays synchronous - but this handler runs directly on the UI
        // thread, and AcceptTerms() reaches the notifier's BLOCKING wrapper, which
        // deadlocks there. Task.Run moves it off the UI thread first, exactly like
        // SetUiAsync's PreRunCheck call. Do not "simplify" this away: the deadlock
        // cannot reproduce on a non-Windows machine.
        //
        // The checkbox is visibly checked while the modal is open and springs back only
        // on decline. That is intended, not a bug.
        private async void termsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressTermsHandler || _termsAcceptInFlight)
            {
                return;
            }

            _termsAcceptInFlight = true;
            termsCheckBox.IsEnabled = false;

            try
            {
                this.functions.LogUIAction("Accept");
                bool accepted = await Task.Run(() => this.functions.AcceptTerms());

                // A run or import may have started (and locked the GUI, see
                // _guiLockedForRun) while this await was pending - Import_click does not
                // wait on terms acceptance, so it can race ahead of an in-flight accept
                // flow. Once that happens this flow's result is stale and must not touch
                // anything DisableButtons() already fixed in place: writing run.IsEnabled
                // here could silently re-enable Run while a collection is already active,
                // which is the one outcome this whole guard exists to prevent.
                if (_guiLockedForRun)
                {
                    return;
                }

                // Belt-and-braces, not load-bearing given the IsEnabled=false above: while
                // this flow owns the checkbox, nothing else can change IsChecked. Kept so
                // this write's correctness does not silently start depending on that
                // invariant holding if this method is ever restructured.
                if (termsCheckBox.IsChecked == true)
                {
                    run.IsEnabled = accepted;
                }

                if (!accepted)
                {
                    _suppressTermsHandler = true;
                    termsCheckBox.IsChecked = false;
                    _suppressTermsHandler = false;
                }
            }
            finally
            {
                _termsAcceptInFlight = false;
                if (!_guiLockedForRun)
                {
                    termsCheckBox.IsEnabled = true;
                }
            }
        }

        // Deliberately NOT async void. An await before the guard check would resume the
        // continuation after _suppressTermsHandler has been reset to false, silently
        // disabling the guard. There is nothing to await here anyway.
        //
        // Logs unconditionally, including while _suppressTermsHandler is set: "terms are
        // no longer accepted" is equally true whether this fired from a real user click
        // or the programmatic revert on decline, so there is exactly one log line for
        // that fact either way rather than the decline path needing its own separate one.
        private void termsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Accept = false");

            if (_suppressTermsHandler)
            {
                return;
            }

            run.IsEnabled = false;
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
            CGlobals.desiredPath = pathBox.Text ?? string.Empty;
        }

        // First use of Avalonia's StorageProvider anywhere in this application. Ported
        // from the spike verbatim, including all three guards. In production `this` IS
        // the Window, so GetTopLevel cannot return null once the constructor has run -
        // the guard is retained anyway.
        //
        // Assigning pathBox.Text is all that is needed: the existing pathBox_TextChanged
        // handler propagates it to CGlobals.desiredPath.
        private async void browseFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = VbrLocalizationHelper.GuiBrowseFolderTitle,
                AllowMultiple = false,
            });

            if (folders.Count == 0)
            {
                return;
            }

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            this.SetPathBoxText(path);
        }

        // Reads sender, never the days7/days30/days90 fields. days7's IsChecked="True"
        // in XAML raises Checked DURING InitializeComponent(), when the other two named
        // fields may not be assigned yet - inspecting them to find the checked one would
        // throw a NullReferenceException at construction. This is the same class of
        // timing hazard the ComboBox handler this replaces guarded against with a null
        // check.
        //
        // This also makes SetReportDays reachable during InitializeComponent() for the
        // first time, which the old null guard suppressed. That is safe with respect to
        // initialization order: `functions` is a field initializer so it runs before the
        // constructor body, and LogUIAction only writes to the static CGlobals.mainlog.
        // It is NOT harmless with respect to a /days:N CLI override, though - writing 7
        // here unconditionally stomps a /days:30 or /days:90 value set before this window
        // was ever constructed. That case is real and is fixed immediately after
        // InitializeComponent() in the constructor (capture reportDaysAtStartup first,
        // then resync) - do not remove that capture/resync block as "redundant" just
        // because this handler's own write looks harmless in isolation.
        //
        // The value comes from Tag rather than Name or Content because Content is
        // localized, and parsing a localized label as data is exactly the mistake
        // notifSeverityBox already makes.
        private void PeriodRadio_Checked(object sender, RoutedEventArgs e)
        {
            // "7" is listed explicitly rather than folded into the `_` default, so `_`
            // means only "unreachable" (sender wasn't a RadioButton, or Tag wasn't one
            // of the three set in XAML) - a future fourth pill with a different Tag
            // hits `_` and lands on this comment instead of silently behaving like "7".
            int days = (sender as RadioButton)?.Tag switch
            {
                "7" => 7,
                "30" => 30,
                "90" => 90,
                _ => 7,
            };

            this.SetReportDays(days);
        }

        private void SetReportDays(int days)
        {
            CGlobals.ReportDays = days;
            this.functions.LogUIAction("Interval set to " + CGlobals.ReportDays);
        }

        #region Server Management

        // Guard preserved from the ListBox version: Avalonia's generated
        // InitializeComponent() can raise SelectionChanged while assigning named fields
        // as the tree is built, so serverSelector can still be null on first raise.
        private void serverSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (serverSelector == null) return;

            UpdateSelectedServersGlobal();

            if (serverSelector.SelectedItem != null)
            {
                this.functions.LogUIAction($"Selected server: {serverSelector.SelectedItem}");
            }
        }

        private async void manageServersBtn_Click(object sender, RoutedEventArgs e)
        {
            var pinned = LocalhostIsInjected
                ? new[] { LocalhostName }
                : System.Array.Empty<string>();

            // Pinned names are passed in `initial` as well - they are displayed rows,
            // and that is what makes Add("localhost") a plain duplicate rather than a
            // second row.
            //
            // Feed this from _displayServers, NOT from CAppSettings.Get().Servers.
            // The direct read looks equivalent and is not: ServerListEditor's ctor
            // filters whitespace but does not TRIM, while _displayServers has already
            // been through Filter's trim. Wire it to the raw settings and a hand-edited
            // "  vbr01  " enters the editor untrimmed, Add's comparison misses it
            // against "vbr01", Commit().FinalServers carries both, and the picker
            // renders two identical rows.
            var initial = _displayServers.ToList();

            var dialog = new ManageServersDialog(initial, pinned);
            bool committed = await dialog.ShowDialog<bool>(this);

            if (!committed)
            {
                return;
            }

            // Re-resolve and repopulate. Without this, a removed server would remain in
            // CGlobals.VBRServerName/REMOTEHOST and the next run would target a host the
            // user just deleted.
            _persistedServers = CAppSettings.LoadOrSeedServers(
                CredentialStore.GetAllServers(),
                excludeLocalhost: LocalhostIsInjected);

            this.InitializeServerList(preserveSelection: true);
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
                monitorStatusText.Foreground = GetStatusBrush("StatusNeutralBrush");
                monitorQuickSetupBtn.IsEnabled = false;
                monitorVhcSetupBtn.IsEnabled = false;
                monitorRunBtn.IsEnabled = false;
            }
            else if (!installed || !taskActive)
            {
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
                monitorQuickSetupBtn.IsEnabled = true;
                monitorRunBtn.IsEnabled = false;
            }
            else
            {
                string version = CVhcMonitorIntegration.GetInstalledVersion();
                monitorStatusText.Text = $"Running ({version})";
                monitorStatusText.Foreground = GetStatusBrush("StatusSuccessBrush");
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
            string notifType = (notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "ntfy";
            string notifUrl = notifUrlBox.Text?.Trim() ?? string.Empty;
            string minSeverity = (notifSeverityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "warning";
            return (notifType, notifUrl, minSeverity);
        }

        private void monitorQuickSetupBtn_Click(object sender, RoutedEventArgs e)
        {
            string server = serverSelector.SelectedItem?.ToString() ?? CGlobals.VBRServerName;
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
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
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
                        monitorStatusText.Foreground = GetStatusBrush("StatusErrorBrush");
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
            string type = (notifTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ntfy";
            notifUrlBox.Tag = type switch
            {
                "Teams" => "https://org.webhook.office.com/...",
                "Slack" => "https://hooks.slack.com/services/...",
                "PagerDuty" => "https://events.pagerduty.com/...",
                _ => "https://ntfy.sh/your-topic"
            };
        }

        // Same Invoke-not-Post reasoning as UpdateCollectionStatusText/
        // ShowCollectionWarningsIfAny above - called from the background
        // thread right before Environment.Exit(0) in Run().
        private void OfferMonitorSetupIfNeeded()
        {
            if (!CVhcMonitorIntegration.IsExePresentInBundle()) return;
            if (CVhcMonitorIntegration.IsTaskRegistered()) return;

            Dispatcher.UIThread.Invoke(() =>
            {
                monitorVhcSetupBtn.IsEnabled = true;
                monitorLastRunText.Text = "Health check complete — click 'Setup from VHC' to configure continuous monitoring with auto-detected server settings.";
                monitorLastRunText.IsVisible = true;
                monitorStatusText.Text = "Available — not set up";
                monitorStatusText.Foreground = GetStatusBrush("StatusWarningBrush");
            });
        }

        #endregion
    }
}
