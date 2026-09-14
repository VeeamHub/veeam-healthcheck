// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck.Functions.ManageServers
{
    // Membership only - this dialog never changes the active server selection. The
    // spike's version returned the selected server as its dialog result; this one
    // returns true if changes were committed and false otherwise, and leaves the
    // caller to repopulate its own picker. That matters because the selection is read
    // by BOTH tabs (monitorQuickSetupBtn_Click reads it too).
    public partial class ManageServersDialog : Window
    {
        private readonly ServerListEditor _editor;

        // Guards the native window-close button (OS title-bar X, Alt+F4, etc.), which
        // has no Button to disable and is not blocked by doneBtn/cancelBtn.IsEnabled.
        // See OnClosing below for why that gap matters and how this flag closes it.
        private bool _busy;

        // Required by the XAML loader. The real entry point is the two-argument
        // constructor; nothing should show a dialog built this way, and leaving _editor
        // null would NRE on the first click rather than failing where the mistake was.
        public ManageServersDialog()
        {
            InitializeComponent();
        }

        // Blocks a user-initiated close while any CGlobals.Notifier await below is in
        // flight. This dialog stays fully interactive during those awaits -
        // AvaloniaUiNotifier owns its dialogs with AvaloniaHost.MainWindow, not this
        // window (see the confirm-guard comment in doneBtn_Click) - and the in-app
        // doneBtn/cancelBtn disables only block clicks reaching THIS dialog's own
        // buttons. The native close button drives CloseCore through a separate path
        // with no button in it to disable, so without this override the exact hazard
        // those disables exist to prevent - resolving the caller's ShowDialog<bool>
        // with a stale result while a commit is still being decided, or has already
        // happened - is reachable through that second door.
        //
        // Close(...) calls made from this class's own code (cancelBtn_Click, both
        // Close(true) sites in doneBtn_Click) route through Window.Close/CloseCore with
        // isProgrammatic: true (verified against the actual Avalonia 11.3.20 source,
        // not assumed), so IsProgrammatic reliably distinguishes "this class closing
        // itself" from "the user closing it" regardless of _busy's exact value at that
        // instant - a programmatic close is never blocked here.
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (_busy && !e.IsProgrammatic)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        public ManageServersDialog(IEnumerable<string> initial, IEnumerable<string> pinned)
            : this()
        {
            this.Title = VbrLocalizationHelper.GuiManageServersTitle;
            this.newServerBox.Watermark = VbrLocalizationHelper.GuiManageServersAddWatermark;
            this.addBtn.Content = VbrLocalizationHelper.GuiManageServersAddButton;
            this.cancelBtn.Content = VbrLocalizationHelper.GuiManageServersCancel;
            this.doneBtn.Content = VbrLocalizationHelper.GuiManageServersDone;

            _editor = new ServerListEditor(
                initial,
                pinned,
                name => CredentialStore.Get(name) != null);

            this.RenderRows();
            this.doneBtn.IsEnabled = true;
        }

        // Rebuilt wholesale on every mutation. The list is a handful of rows and this
        // avoids an ObservableCollection plus per-row change notification for state
        // that only ever changes in response to a click in this same dialog.
        private void RenderRows()
        {
            this.serverRows.Items.Clear();

            foreach (var row in _editor.Rows)
            {
                this.serverRows.Items.Add(this.BuildRow(row));
            }

            int pending = _editor.PendingChangeCount;
            this.pendingText.Text = pending == 0
                ? VbrLocalizationHelper.GuiManageServersNoPending
                : string.Format(
                    CultureInfo.CurrentCulture,
                    VbrLocalizationHelper.GuiManageServersPending,
                    pending);
        }

        private Control BuildRow(ServerRow row)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };

            var label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Text = row.HasCredentials
                    ? $"{row.Name}  ({VbrLocalizationHelper.GuiManageServersCredsSaved})"
                    : row.Name,
            };

            if (row.IsPendingRemoval)
            {
                // Staged removals stay VISIBLE and struck through rather than
                // disappearing. If rows vanished on click, a staged dialog would look
                // identical to today's immediate one and the user would have no way to
                // see what Done is about to destroy - which is what makes Cancel legible.
                label.TextDecorations = TextDecorations.Strikethrough;
                label.Opacity = 0.5;
            }

            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            if (row.IsRemovable)
            {
                var button = new Button
                {
                    Tag = row.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                if (row.IsPendingRemoval)
                {
                    button.Content = "↶";
                    button.Classes.Add("undo-server");
                    ToolTip.SetTip(button, VbrLocalizationHelper.GuiManageServersUndoTooltip);
                    button.Click += this.UndoRow_Click;
                }
                else
                {
                    button.Content = "✕";
                    button.Classes.Add("remove-server");
                    ToolTip.SetTip(button, VbrLocalizationHelper.GuiManageServersRemoveTooltip);
                    button.Click += this.RemoveRow_Click;
                }

                Grid.SetColumn(button, 1);
                grid.Children.Add(button);
            }

            return grid;
        }

        private void addBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = this.newServerBox.Text;
            var result = _editor.Add(name);

            switch (result)
            {
                case AddResult.Invalid:
                    _ = CGlobals.Notifier.ShowErrorAsync(
                        VbrLocalizationHelper.GuiManageServersInvalid,
                        VbrLocalizationHelper.GuiManageServersTitle);
                    return;

                case AddResult.Duplicate:
                    _ = CGlobals.Notifier.ShowErrorAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            VbrLocalizationHelper.GuiManageServersDuplicate,
                            name?.Trim()),
                        VbrLocalizationHelper.GuiManageServersTitle);
                    this.newServerBox.Text = string.Empty;
                    return;

                default:
                    this.newServerBox.Text = string.Empty;
                    this.RenderRows();
                    return;
            }
        }

        // Removals and undos go through the editor's Remove/UndoRemove ONLY. Never set
        // ServerRow.IsPendingRemoval directly: that setter is internal, so this dialog
        // can reach it, and doing so bypasses Remove's IsRemovable short-circuit - the
        // only thing stopping a pinned row from being staged. Commit's
        // CredentialsToDelete filter carries no pinned guard of its own (it does not
        // need one, because a pinned row cannot acquire the flag through the public
        // API), so a directly-set flag on a pinned, credentialed localhost would delete
        // its stored credentials. Unreachable today; one line of dialog code away.
        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            _editor.Remove((string)((Button)sender).Tag);
            this.RenderRows();
        }

        private void UndoRow_Click(object sender, RoutedEventArgs e)
        {
            _editor.UndoRemove((string)((Button)sender).Tag);
            this.RenderRows();
        }

        // Cancel and the OS close button both discard every staged change and touch
        // nothing. Close(false) is also what an unhandled window close yields, so the
        // two paths agree without extra wiring.
        private void cancelBtn_Click(object sender, RoutedEventArgs e) => Close(false);

        private async void doneBtn_Click(object sender, RoutedEventArgs e)
        {
            var plan = _editor.Commit();

            if (plan.CredentialsToDelete.Count > 0)
            {
                // Both buttons are disabled across the await, and this is not just
                // double-click hygiene. AvaloniaUiNotifier always owns its dialogs with
                // AvaloniaHost.MainWindow (AvaloniaUiNotifier.cs:23, :32), which is
                // VhcGui - NOT this window. So the confirm below disables the main
                // window and leaves THIS dialog fully interactive: without the guard the
                // user can click Cancel while the confirm is up, Close(false) resolves
                // the caller's ShowDialog<bool> so it skips its refresh, and then this
                // suspended handler resumes and deletes credentials anyway - after a
                // cancel, with the list still showing the servers it just unlinked.
                this.doneBtn.IsEnabled = false;
                this.cancelBtn.IsEnabled = false;

                bool confirmed;
                _busy = true;
                try
                {
                    confirmed = await CGlobals.Notifier.ConfirmAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            VbrLocalizationHelper.GuiManageServersConfirmBody,
                            plan.PendingRemovalCount,
                            plan.CredentialsToDelete.Count),
                        VbrLocalizationHelper.GuiManageServersConfirmTitle);
                }
                finally
                {
                    _busy = false;
                    this.doneBtn.IsEnabled = true;
                    this.cancelBtn.IsEnabled = true;
                }

                if (!confirmed)
                {
                    return;
                }
            }

            // ServerListCommitter owns credentials-first-then-settings ordering AND the
            // reinstatement that makes "recoverable by retrying" actually true: without
            // it, plan.FinalServers already excludes every removed row regardless of
            // whether its deletion succeeded, so a failed deletion would still drop the
            // host from the persisted list while orphaning its credential. See
            // ServerListCommitter.cs for why.
            var committer = new ServerListCommitter(
                CredentialStore.Remove,
                CAppSettings.SetServers);
            var outcome = committer.Execute(plan);

            foreach (var host in outcome.FailedCredentialRemovals)
            {
                CGlobals.Logger.Error($"Failed to remove stored credentials for host: {host}");
            }

            if (!outcome.SettingsSaved)
            {
                _busy = true;
                try
                {
                    await CGlobals.Notifier.ShowErrorAsync(
                        VbrLocalizationHelper.GuiManageServersSaveFailed,
                        VbrLocalizationHelper.GuiManageServersSaveFailedTitle);
                }
                finally
                {
                    _busy = false;
                }

                // Permanently disable Done rather than leave it clickable for a retry
                // from this same dialog instance. A retry would recompute the identical
                // CommitPlan from unchanged _editor state, so any host whose credential
                // WAS actually deleted just above gets submitted to CredentialStore.Remove
                // a second time - which now correctly returns false ("nothing left to
                // remove"), indistinguishable to ServerListCommitter from a genuine
                // failure, so it silently reinstates that host into the persisted list on
                // the next successful save. The user asked to remove it; retrying would
                // bring it back with no indication why. Cancel (still enabled) and
                // reopening the dialog is the only safe way to retry: a fresh
                // ServerListEditor re-queries CredentialStore.Get per row, so a host
                // that really was deleted now shows HasCredentials = false and is
                // correctly excluded from CredentialsToDelete next time.
                this.doneBtn.IsEnabled = false;
                return;
            }

            if (outcome.FailedCredentialRemovals.Count > 0)
            {
                // Both buttons off before this await and deliberately NOT re-enabled -
                // unlike the confirm guard above, this path closes unconditionally right
                // after. AvaloniaUiNotifier owns its dialogs with AvaloniaHost.MainWindow
                // (VhcGui), not this window (see the confirm-guard comment above), so
                // THIS dialog stays fully interactive while the message is on screen. A
                // Cancel click during that window would resolve the caller's
                // ShowDialog<bool> with false - which skips manageServersBtn_Click's
                // repopulate for a commit that DID succeed, leaving the picker stale -
                // and would then leave this suspended handler to Close(true) a window
                // that is already closed. Disabling Cancel here, with no re-enable,
                // removes that path entirely.
                //
                // outcome.SettingsSaved is true here (the false case already returned
                // above), so the list genuinely saved - this is telling the user which
                // host(s) were kept in it because their credential survived, not
                // blocking the close.
                this.doneBtn.IsEnabled = false;
                this.cancelBtn.IsEnabled = false;

                // Close(true) lives in `finally`, not after this block, so it still runs
                // even if ShowErrorAsync itself throws: the commit already succeeded on
                // disk by this point, and leaving the dialog open with both buttons
                // disabled forever - reachable if the notification call throws - would
                // both strand the user AND, since _editor's state is unchanged, leave
                // Done re-enabled-by-nothing for a retry that would re-trip the exact
                // reinstatement hazard the doneBtn-disable above this block exists to
                // prevent. Closing regardless is strictly safer than staying open.
                _busy = true;
                try
                {
                    await CGlobals.Notifier.ShowErrorAsync(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            VbrLocalizationHelper.GuiManageServersPartialFailureBody,
                            string.Join(", ", outcome.FailedCredentialRemovals)),
                        VbrLocalizationHelper.GuiManageServersPartialFailureTitle);
                }
                finally
                {
                    _busy = false;
                    Close(true);
                }

                return;
            }

            Close(true);
        }
    }
}
