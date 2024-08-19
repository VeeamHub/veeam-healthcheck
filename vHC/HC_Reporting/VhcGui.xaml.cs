﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
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
        private CClientFunctions _functions = new();

        public VhcGui()
        {
            InitializeComponent();

            SetUi();
            pathBox.IsEnabled = false;

        }
        private void SetUi()
        {
            SetImportRelease();

#if DEBUG
            SetImportDebug();
#endif
            this.Title = _functions.ModeCheck();
            _functions.PreRunCheck();

            SetUiText();
            scrubBox.IsChecked = true;
            hideProgressBar();
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

            SetPathBoxText(CVariables.outDir);
            CGlobals._desiredPath = CVariables.outDir;
        }

        private void SetPathBoxText(string text)
        {
            pathBox.Text = text;
        }

        private void hideProgressBar()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                //run.IsEnabled = true;
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
            _functions.LogUIAction("Import");
            CGlobals.IMPORT = true;
            DisableGuiAndStartProgressBar();
            Run(true);
        }



        private void run_Click(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Run");

            if (!_functions.VerifyPath())
            {
                MessageBox.Show("Error: Failed to validate desired output path. Please try a different path.");
            }
            if (_functions.VerifyPath())
            {
                DisableGuiAndStartProgressBar();
                Run(false);
            }
        }
        private void Run(bool import)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {

                _functions.StartPrimaryFunctions();
                Environment.Exit(0);

            }).ContinueWith(t =>
            {
                hideProgressBar();
            });
        }
        private void DisableGuiAndStartProgressBar()
        {
            DisableButtons();
            showProgressBar();
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
            _functions.LogUIAction("Accept");
            run.IsEnabled = _functions.AcceptTerms();
        }

        #endregion




        #region Check Boxes
        private void HandleCheck(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Scrub = true");
            CGlobals.Scrub = true;
        }
        private void htmlChecked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Open HTML = true");
            CGlobals.OpenHtml = true;
        }
        private void htmlUnchecked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Open HTML = false");
            CGlobals.OpenHtml = false;
        }
        private void HandleUnchecked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Scrub = false");
            CGlobals.Scrub = false;
        }
        private void HandleThirdState(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Scrub 3rd state = false");
            CGlobals.Scrub = false;
        }
        private void explorerShowBox_Checked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Show Explorer = true");
            CGlobals.OpenExplorer = true;
        }
        private void explorerShowBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Show Explorer = false");
            CGlobals.OpenExplorer = false;
        }
        private void pdfCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Export PDF = true");
            CGlobals.EXPORTPDF = true;
        }
        private void pdfCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _functions.LogUIAction("Export PDF = false");
            CGlobals.EXPORTPDF = false;
        }

        #endregion

        private void kbLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            _functions.LogUIAction("KB Link");
            _functions.KbLinkAction(e);
        }
        private void pathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CGlobals.Logger.Info("Changing path from " + CGlobals._desiredPath + " to " + pathBox.Text);
            CGlobals._desiredPath = pathBox.Text;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (daysSelector.SelectedIndex)
            {
                case 0:
                    SetReportDays(7);
                    break;
                case 1:
                    SetReportDays(30);
                    break;
                case 2:
                    SetReportDays(90);
                    break;
                default:
                    SetReportDays(7);
                    break;
            }
        }
        private void SetReportDays(int days)
        {
            CGlobals.ReportDays = days;
            _functions.LogUIAction("Interval set to " + CGlobals.ReportDays);
        }
    }
}
