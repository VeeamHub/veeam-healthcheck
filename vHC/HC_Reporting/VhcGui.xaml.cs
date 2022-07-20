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
        public static bool _import;

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


            SetPathBoxText(CVariables.unsafeDir);

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
            LogUIAction("Import clicked");
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
        private void RunAction()
        {
            log.Info("Starting Run");
            ExecPsScripts();

            bool userSetScrub = _scrub;
            bool userOpenHtml = _openHtml;
            bool userOpenExplorer = _openExplorer;
            string userPath = _desiredPath;

            _openHtml = false;
            _openExplorer = false;


            if (_scrub)
            {
                _desiredPath = userPath + CVariables._unsafeSuffix;
                _scrub = false;
                Import();
                _scrub = true;
                _desiredPath = userPath + CVariables._safeSuffix;
            }
            else if (!_scrub)
            {
                _desiredPath = userPath + CVariables._safeSuffix;
                _scrub = true;
                Import();
                _scrub = false;
                _desiredPath = userPath + CVariables._unsafeSuffix;
            }
            //_scrub = userSetScrub;
            _openHtml = userOpenHtml;
            _openExplorer = userOpenExplorer;
            Import();


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
        public void CliRun()
        {
            log.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            _openExplorer = false;
            _openHtml = false;
            RunAction();
        }
        private void run_Click(object sender, RoutedEventArgs e)
        {

            LogUIAction("Run selected");

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
            _scrub = true;
            SetPathBoxText(CVariables.safeDir);
        }
        private void htmlChecked(object sender, RoutedEventArgs e)
        {
            //export HTML
            _openHtml = true;
        }
        private void htmlUnchecked(object sender, RoutedEventArgs e)
        {
            //don't export html
            _openHtml = false;
        }
        private void HandleUnchecked(object sender, RoutedEventArgs e)
        {
            SetPathBoxText(CVariables.unsafeDir);
            _scrub = false;
        }
        private void HandleThirdState(object sender, RoutedEventArgs e)
        {
            // do nothing
            _scrub = false;
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
        private void explorerShowBox_Checked(object sender, RoutedEventArgs e)
        {
            _openExplorer = true;
        }
        private void explorerShowBox_Unchecked(object sender, RoutedEventArgs e)
        {
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
            _desiredPath = pathBox.Text;
        }
    }
}
