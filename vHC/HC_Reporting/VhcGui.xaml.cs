// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
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
            pathBox.IsEnabled = false;

            // pdfCheckBox.IsEnabled = false;
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
            }));
        }

        private void showProgressBar()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                run.IsEnabled = false;
                pBar.Visibility = Visibility.Visible;
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
    }
}
