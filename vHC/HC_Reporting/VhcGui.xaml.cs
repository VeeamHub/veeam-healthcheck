// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Html;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using VeeamHealthCheck;
using VeeamHealthCheck.DB;
using VeeamHealthCheck.Shared.Logging;
using System.Threading;
using System.Globalization;
using VeeamHealthCheck.Scrubber;
using System.ComponentModel.Composition.Primitives;
using System.IO;

namespace VeeamHealthCheck
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class VhcGui : System.Windows.Window
    {
        public static CLogger log = new("HealthCheck");
        public static bool _scrub = true;
        private bool _collectSessionData = true;
        public static bool _openHtml = false;
        public static bool _openExplorer = true;
        public static string _desiredPath = CVariables.unsafeDir;
        private string _path;

        public static CScrubHandler _scrubberMain = new();
        public static bool _import;
        public static int _reportDays = 7;
        private bool _isVbr = false;
        private bool _isVb365 = false;


        public VhcGui()
        {
            //CultureInfo.CurrentCulture = Thread.CurrentThread.CurrentCulture;
            //CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-cn");
            //MessageBox.Show(ResourceHandler.GuiAcceptText);
            //Thread.CurrentThread.CurrentCulture = CultureInfo.CurrentCulture.Name;
            InitializeComponent();

            SetUi();


        }
        private void SetUi()
        {
            SetImportRelease();

#if DEBUG
            SetImportDebug();
#endif
            ModeCheck();
            PreCheck();

            SetUiText();
            scrubBox.IsChecked = true;
            hideProgressBar();
            run.IsEnabled = false;
        }
        private void SetImportRelease()
        {
            importButton.IsEnabled = false;
            importButton.Width = 0;
            //run.HorizontalAlignment = HorizontalAlignment.Center;
            //run.Margin.Right = new Thickness(10);
            //termsBtn.HorizontalAlignment = HorizontalAlignment.Left;
        }
        private void SetImportDebug()
        {
            importButton.IsEnabled = true;
            importButton.Width = 100;
        }

        #region UI Functions

        private void SetUiText()
        {
            this.InsHeader.Text = ResourceHandler.GuiInstHeader;
            this.line1.Text = ResourceHandler.GuiInstLine1;
            this.line2.Text = ResourceHandler.GuiInstLine2;
            this.line3.Text = ResourceHandler.GuiInstLine3;
            this.line4.Text = ResourceHandler.GuiInstLine4;
            this.line5.Text = ResourceHandler.GuiInstLine5;
            this.line6.Text = ResourceHandler.GuiInstLine6;
            this.Cav1Part1.Text = ResourceHandler.GuiInstCaveat1;
            this.Cav2.Text = ResourceHandler.GuiInstCaveat2;
            this.OptHdr.Text = ResourceHandler.GuiOptionsHeader;
            this.htmlCheckBox.Content = ResourceHandler.GuiShowHtml;
            this.scrubBox.Content = ResourceHandler.GuiSensData;
            this.explorerShowBox.Content = ResourceHandler.GuiShowFiles;
            this.outPath.Text = ResourceHandler.GuiOutPath;
            this.termsBtn.Content = ResourceHandler.GuiAcceptButton;
            this.run.Content = ResourceHandler.GuiRunButton;
            this.importButton.Content = ResourceHandler.GuiImportButton;

            //this.Title = ResourceHandler.GuiTitle;


            SetPathBoxText(CVariables.outDir);
            _desiredPath = CVariables.outDir;

        }

        private void SetPathBoxText(string text)
        {
            pathBox.Text = text;
            _path = text;
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
        private void Import()
        {

            CReportModeSelector cMode = new();
            cMode.Run();
            cMode.Dispose();
            //}
        }
        private void Import_click(object sender, RoutedEventArgs e)
        {
            //   Import();
            LogUIAction("Import");
            _import = true;
            showProgressBar();

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                Import();
            }).ContinueWith(t =>
            {
                hideProgressBar();
            });

        }
        private void PopulateWaits()
        {
            try
            {
                FilesParser.CLogParser lp = new();
                lp.GetWaitsFromFiles();
            }
            catch (Exception e)
            {
                log.Error("Error checking log files:");
                log.Error(e.Message);
            }

        }
        private void RunAction()
        {
            log.Info("Starting Run");
            ExecPsScripts();
            PopulateWaits();
            if (_isVbr)
            {
                Collection.LogParser.CLogOptions logOptions = new("vbr");
            }
            if (_isVb365)
            {
                Collection.LogParser.CLogOptions logOptions = new("vb365");
            }

            bool userSetScrub = _scrub;
            bool userOpenHtml = _openHtml;
            bool userOpenExplorer = _openExplorer;
            string userPath = _desiredPath;

            
            string userSettingsString = String.Format(
                "User Settings:\n"+
                "\t\t\t\t\t\t\t\t\tScrub = {0}\n"+
                "\t\t\t\t\t\t\t\t\tOpen HTML = {1}\n"+
                "\t\t\t\t\t\t\t\t\tOpen Explorer = {2}\n"+
                "\t\t\t\t\t\t\t\t\tPath = {3}\n" +
                "\t\t\t\t\t\t\t\t\tInterval = {4}",
                _scrub,_openHtml, _openExplorer, _desiredPath, _reportDays.ToString()
                );

            log.Info(userSettingsString);
            Import();
            //    _openHtml = false;
            //_openExplorer = false;
            //if (_scrub)
            //{
            //    _desiredPath = userPath + CVariables._unsafeSuffix;
            //    _scrub = false;

            //    log.Info("Creating Original Report to directory: " + _desiredPath);

            //    Import();
            //    log.Info("Creating Original Report to directory...done! ");
            //    _scrub = true;
            //    _desiredPath = userPath + CVariables._safeSuffix;
            //}
            //else if (!_scrub)
            //{
            //    _desiredPath = userPath + CVariables._safeSuffix;
            //    _scrub = true;
            //    log.Info("Creating Anonymous Report to directory: " + _desiredPath);
            //    Import();
            //    log.Info("Creating Anonymous Report to directory...done!");
            //    _scrub = false;
            //    _desiredPath = userPath + CVariables._unsafeSuffix;
            //}
            ////_scrub = userSetScrub;
            //_openHtml = userOpenHtml;
            //_openExplorer = userOpenExplorer;
            //if(_scrub)
            //    log.Info("Creating Anonymous Report to directory: " + _desiredPath);
            //if (!_scrub)
            //    log.Info("Creating Original Report to directory: " + _desiredPath);
            //Import();

            log.Info("Creating Report done!");


            //// change scrub to opposite + gen 2nd report
            //if (_scrub)
            //{
            //    _desiredPath = CVariables.unsafeDir;
            //    _scrub = false;
            //}
            //else if (!_scrub)
            //{
            //    _desiredPath = CVariables.safeDir;
            //    _scrub = true;
            //}
            //Import();

            log.Info("Starting Run..done!");

        }
        public void CliRun(string targetForOutput)
        {
            log.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            _openExplorer = false;
            _openHtml = false;
            _desiredPath = targetForOutput;
            RunAction();
        }
        private bool VerifyPath()
        {
            try
            {
                if (!Directory.Exists(_desiredPath))
                    Directory.CreateDirectory(_desiredPath);
                return true;
            }
            catch(Exception e)
            {
                log.Error("[UI] Desired dir does not exist and cannot be created. Error: ");
                log.Error("\t" + e.Message);
                return false;
            }
        }
        private void run_Click(object sender, RoutedEventArgs e)
        {

            LogUIAction("Run");

            if (!VerifyPath())
            {
                //MessageBox mb = new();
                
                MessageBox.Show("Error: Failed to validate desired output path. Please try a different path.");

            }
            if (VerifyPath())
            {


                // disable UI Buttons and boxes
                DisableButtons();


                _import = false;
                showProgressBar();

                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    RunAction();
                    // should be done here?
                    Environment.Exit(0);

                }).ContinueWith(t =>
                {
                    hideProgressBar();
                });
            }
            //EnableButtons();
        }
        private void DisableButtons()
        {
            explorerShowBox.IsEnabled = false;
            htmlCheckBox.IsEnabled = false;
            scrubBox.IsEnabled = false;
            termsBtn.IsEnabled = false;
            importButton.IsEnabled = false;
            pathBox.IsEnabled = false;
        }
        private void EnableButtons()
        {
            explorerShowBox.IsEnabled = true;
            htmlCheckBox.IsEnabled = true;
            scrubBox.IsEnabled = true;
            termsBtn.IsEnabled = true;
            importButton.IsEnabled = true;
        }
        private void AcceptButton_click(object sender, RoutedEventArgs e)
        {
            LogUIAction("Accecpt");
            string message = ResourceHandler.GuiAcceptText;

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo);
            if (res.ToString() == "Yes")
            {
                EnableRun();
            }

        }

        #endregion
        private void ExecPsScripts()
        {
            log.Info("Starting PS Invoke", false);
            PSInvoker p = new PSInvoker();

            if (_isVbr)
            {
                try
                {
                    log.Info("Entering vbr ps invoker", false);
                    p.Invoke(_collectSessionData);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                }
            }
            if (_isVb365)
            {
                try
                {
                    log.Info("Entering vb365 ps invoker", false);
                    p.InvokeVb365Collect();
                }
                catch (Exception ex) { log.Error(ex.Message); }
            }

            log.Info("Starting PS Invoke...done!", false);
        }
        private void ModeCheck()
        {
            log.Info("Checking processes to determine execution mode..");
            string title = ResourceHandler.GuiTitle;
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                if (process.ProcessName == "Veeam.Archiver.Service")
                {

                    _isVb365 = true;
                    log.Info("VB365 software detected");
                }
                if (process.ProcessName == "Veeam.Backup.Service")
                {
                    _isVbr = true;
                    log.Info("VBR software detected");
                }

            }
            if (_isVbr)
                this.Title = title + " - " + ResourceHandler.GuiTitleBnR;
            if (_isVb365)
                this.Title = title + " - " + ResourceHandler.GuiTitleVB365;
            if (_isVbr && _isVb365)
                this.Title = title + " - " + ResourceHandler.GuiTitleBnR + " & " + ResourceHandler.GuiTitleVB365;
            if (!_isVb365 && !_isVbr)
                this.Title = title + " - " + ResourceHandler.GuiImportModeOnly;
        }
        private void PreCheck()
        {
            log.Info("Starting Admin Check");
            CAdminCheck priv = new();
            if (!priv.IsAdmin())
            {
                string message = "Please run program as Administrator";
                MessageBox.Show(message);
                log.Error(message);
                //Console.ReadKey();
                Environment.Exit(0);
            }

            log.Info("Starting Admin Check...done!");
        }
        private bool IsVbrServer()
        {
            try
            {
                if (_isVbr)
                    return true;
                else
                    return false;
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return false;
            }
        }
        private void LogUIAction(string message)
        {
            string s = string.Format("[Veeam.HC.UI]\tSelected:" + message);
            log.Info(s);
        }

        #region Check Boxes
        private void HandleCheck(object sender, RoutedEventArgs e)
        {
            LogUIAction("Scrub = true");
            _scrub = true;
            //SetPathBoxText(_desiredPath + CVariables._safeSuffix);
        }
        private void htmlChecked(object sender, RoutedEventArgs e)
        {
            LogUIAction("Open HTML = true");
            //export HTML
            _openHtml = true;
        }
        private void htmlUnchecked(object sender, RoutedEventArgs e)
        {
            LogUIAction("Open HTML = false");
            //don't export html
            _openHtml = false;
        }
        private void HandleUnchecked(object sender, RoutedEventArgs e)
        {
            LogUIAction("Scrub = false");
            //SetPathBoxText(_desiredPath + CVariables._unsafeSuffix);
            _scrub = false;
        }
        private void HandleThirdState(object sender, RoutedEventArgs e)
        {
            LogUIAction("Scrub 3rd state = false");
            // do nothing
            _scrub = false;
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
        private void explorerShowBox_Checked(object sender, RoutedEventArgs e)
        {
            LogUIAction("Show Explorer = true");
            _openExplorer = true;
        }
        private void explorerShowBox_Unchecked(object sender, RoutedEventArgs e)
        {
            LogUIAction("Show Explorer = false");
            _openExplorer = false;
        }

        #endregion

        private void EnableRun()
        {
            run.IsEnabled = true;
        }
        private void kbLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            //Process.Start(e.Uri.ToString());
            LogUIAction("KB Link");
            log.Info("opening kb_link");
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                WebBrowser w1 = new();

                var p = new Process();
                p.StartInfo = new ProcessStartInfo(e.Uri.ToString())
                {
                    UseShellExecute = true
                };
                p.Start();
            });

            log.Info("opening kb_link..done!");
        }
        private void pathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            log.Info("Changing path from " + _desiredPath + " to " + pathBox.Text);
            _desiredPath = pathBox.Text;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(daysSelector.SelectedIndex == 0)
            {
                _reportDays = 7;
                LogUIAction("Interval set to " + _reportDays);
            }
            if (daysSelector.SelectedIndex == 1)
            {
                _reportDays = 30;
                LogUIAction("Interval set to " + _reportDays);
            }
            if (daysSelector.SelectedIndex == 2)
            {
                _reportDays = 90;
                LogUIAction("Interval set to " + _reportDays);
            }
        }
    }
}
