// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class VhcGui : System.Windows.Window
    {
        private readonly CClientFunctions functions = new();

        public VhcGui()
        {
            InitializeComponent();

            this.SetUi();
            pathBox.IsEnabled = true;
            this.InitializeServerList();

            // pdfCheckBox.IsEnabled = false;
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

        private void SetUi()
        {
            this.SetImportRelease();

            string modeCheckResult = this.functions.ModeCheck();
            
            if (modeCheckResult == "fail")
            {
                string errorMessage = "No Veeam Software detected on this machine.\n\n" +
                                     "This tool requires Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 (VB365) to be installed.\n\n" +
                                     "To connect to a remote Veeam server:\n" +
                                     "1. Close this window\n" +
                                     "2. Run from command line with: VeeamHealthCheck.exe /remote /host=your-vbr-server\n\n" +
                                     "For more information, see the documentation.";
                
                MessageBox.Show(errorMessage, "Veeam Software Not Detected", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Close the application
                Application.Current.Shutdown();
                return;
            }
            
            this.Title = modeCheckResult;
            if(CGlobals.IsVb365 && CGlobals.IsVbr)
            {
                pdfCheckBox.IsEnabled = false;
                pdfCheckBox.ToolTip = "PDF Export not available when both VB365 & VBR are detected on the same machine.";
            }

            this.functions.PreRunCheck();

            this.SetUiText();
            scrubBox.IsChecked = true;
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
            this.clearCredsCheckBox.Content = "Clear Saved Credentials";
            this.outPath.Text = VbrLocalizationHelper.GuiOutPath;
            this.termsBtn.Content = VbrLocalizationHelper.GuiAcceptButton;
            this.run.Content = VbrLocalizationHelper.GuiRunButton;
            this.importButton.Content = VbrLocalizationHelper.GuiImportButton;

            this.SetPathBoxText(CVariables.outDir);
            CGlobals.desiredPath = CVariables.outDir;
        }

        private void SetPathBoxText(string text)
        {
            pathBox.Text = text;
        }

        private void hideProgressBar()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                // run.IsEnabled = true;
                pBar.Visibility = Visibility.Hidden;
                progressText.Visibility = Visibility.Collapsed;
            }));
        }

        private void showProgressBar()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                run.IsEnabled = false;
                pBar.Visibility = Visibility.Visible;
                progressText.Visibility = Visibility.Visible;
            }));
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
                MessageBox.Show("Error: Failed to validate desired output path. Please try a different path.");
            }

            if (this.functions.VerifyPath())
            {
                this.DisableGuiAndStartProgressBar();
                this.Run(false);
            }
        }

        private void Run(bool import)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                this.functions.StartPrimaryFunctions();
                Environment.Exit(0);
            }).ContinueWith(t =>
            {
                this.hideProgressBar();
            });
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
        }

        private void AcceptButton_click(object sender, RoutedEventArgs e)
        {
            this.functions.LogUIAction("Accept");
            run.IsEnabled = this.functions.AcceptTerms();
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

        #endregion

        private void kbLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            this.functions.LogUIAction("KB Link");
            this.functions.KbLinkAction(e);
        }

        private void pathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CGlobals.Logger.Info("Changing path from " + CGlobals.desiredPath + " to " + pathBox.Text);
            CGlobals.desiredPath = pathBox.Text;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
                MessageBox.Show("Please enter a server name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Check if server already exists in list
            foreach (var item in serverListBox.Items)
            {
                if (item.ToString().Equals(serverName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Server '{serverName}' is already in the list.", "Duplicate Server", MessageBoxButton.OK, MessageBoxImage.Information);
                    serverTextBox.Clear();
                    return;
                }
            }
            
            // Add server to list
            serverListBox.Items.Add(serverName);
            serverTextBox.Clear();
            UpdateSelectedServersGlobal();
            
            this.functions.LogUIAction($"Added server: {serverName}");
        }

        private void removeServerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (serverListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a server to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string selectedServer = serverListBox.SelectedItem.ToString();
            
            // Don't allow removing localhost if it's the only item
            if (serverListBox.Items.Count == 1 && selectedServer.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot remove the last server. At least one server must remain in the list.", "Cannot Remove", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Ask for confirmation if this server has stored credentials
            bool hasCredentials = CredentialStore.Get(selectedServer) != null;
            if (hasCredentials)
            {
                var result = MessageBox.Show(
                    $"Remove '{selectedServer}' from the list?\n\nThis will also delete any stored credentials for this server.",
                    "Confirm Remove",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
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

        private void clearServersBtn_Click(object sender, RoutedEventArgs e)
        {
            if (serverListBox.Items.Count == 0)
            {
                MessageBox.Show("Server list is already empty.", "Empty List", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var result = MessageBox.Show(
                "Are you sure you want to clear all servers from the list?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
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

        private void serverListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedServersGlobal();
            
            if (serverListBox.SelectedItem != null)
            {
                this.functions.LogUIAction($"Selected server: {serverListBox.SelectedItem}");
            }
        }

        #endregion
    }
}
