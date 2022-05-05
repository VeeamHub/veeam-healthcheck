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


namespace VeeamHealthCheck
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static CLogger log = new("HealthCheck");
        private bool _scrub = false;
        private bool _collectSessionData = true;
        private bool _openHtml = false;
        public static bool _openExplorer = true;
        public static string _desiredPath = CVariables.unsafeDir;
        private string _path;

        private bool _isVbr = false;
        private bool _isVb365 = false;
        public MainWindow(string[] args)
        {

        }
        public MainWindow()
        {
            InitializeComponent();
            
            SetUi();


        }
        private void SetUi()
        {
            ModeCheck();
            PreCheck();

            SetUiText();

            hideProgressBar();
            run.IsEnabled = false;
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
            CReportModeSelector cMode = new(_desiredPath, _scrub, _openHtml, true);
            cMode.Run();

            //CCsvToXml c = new();

            ////choose VBO or VBR
            //c.ConvertToXml(_scrub, false, _openHtml, true);
            //c.Dispose();
        }
        private void Import_click(object sender, RoutedEventArgs e)
        {
            //   Import();
            LogUIAction("PopulateData");
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


            CReportModeSelector cMode = new(_desiredPath, _scrub, _openHtml, false); ;
            cMode.Run();
            //CCsvToXml c = new();
            //c.ConvertToXml(_scrub, true, _openHtml, false);
            //c.Dispose();
            log.Info("Starting Run..done!");

        }
        private void run_Click(object sender, RoutedEventArgs e)
        {
            //if (!IsVbrServer())
            //{
            //    string msg = "Server is not VBR Server. Please run the program on the actual VBR server";
            //    MessageBox.Show(msg);
            //    log.Error(msg);
            //    Environment.Exit(0);
            //}
            LogUIAction("PopulateData");
            showProgressBar();

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                RunAction();
                //Import();
                //JustRunNoPs();
            }).ContinueWith(t =>
            {
                hideProgressBar();
            });
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
            log.Info("Starting PS Invoke");
            PSInvoker p = new PSInvoker();

            if (_isVbr)
            {
                try
                {
                    log.Info("Entering vbr ps invoker");
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
                    log.Info("Entering vb365 ps invoker");
                    p.InvokeVb365Collect();
                }
                catch (Exception ex) { log.Error(ex.Message); }
            }

            log.Info("Starting PS Invoke...done!");
        }
        private void ModeCheck()
        {
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
            if(_isVbr)
                this.Title = title + " - " + ResourceHandler.GuiTitleBnR;
            if(_isVb365)
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
