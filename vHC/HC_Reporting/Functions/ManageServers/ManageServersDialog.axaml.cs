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

        // Required by the XAML loader. The real entry point is the two-argument
        // constructor; nothing should show a dialog built this way, and leaving _editor
        // null would NRE on the first click rather than failing where the mistake was.
        public ManageServersDialog()
        {
            InitializeComponent();
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
                await CGlobals.Notifier.ShowErrorAsync(
                    VbrLocalizationHelper.GuiManageServersSaveFailed,
                    VbrLocalizationHelper.GuiManageServersSaveFailedTitle);
                return;
            }

            Close(true);
        }
    }
}
