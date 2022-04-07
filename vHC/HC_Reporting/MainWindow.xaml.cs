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
        public MainWindow(string[] args)
        {
            InitializeComponent();
            SetPathBoxText(CVariables.unsafeDir);
            //testing new UI
            //Wizard_1 w = new();
            //w.Show();

            //testing password lock
            //DecryptSystem();

            //maybe disable for mass distrib?
            //importButton.IsEnabled = false;

            //core functions
            PreCheck();
            hideProgressBar();
            run.IsEnabled = false;
        }
        public MainWindow()
        {
            InitializeComponent();
            SetPathBoxText(CVariables.unsafeDir);
            //testing new UI
            //Wizard_1 w = new();
            //w.Show();

            //testing password lock
            //DecryptSystem();

            //maybe disable for mass distrib?
            //importButton.IsEnabled = false;

            //core functions
            PreCheck();
            hideProgressBar();
            run.IsEnabled = false;
        }
        private void SetPathBoxText(string text)
        {
            pathBox.Text = text;
            _path = text;
        }
        private void DecryptSystem()
        {
    //        string input =
    //Microsoft.VisualBasic.Interaction.InputBox("My Prompt",
    //                                           "The Title",
    //                                           "",
    //                                           -1, -1);


            //if (!String.IsNullOrEmpty(input))
            //{
            //    if (input != "HealthCheck!")
            //        Environment.Exit(0);
            //}
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
        private void Import()
        {
            CModeSelector cMode = new(_desiredPath, _scrub, _openHtml);
            cMode.Run();
            
            //CCsvToXml c = new();

            ////choose VBO or VBR
            //c.ConvertToXml(_scrub, false, _openHtml, true);
            //c.Dispose();
        }

        private void JustRun()
        {
            log.Info("Starting Run");
            Run();

            CCsvToXml c = new();
            c.ConvertToXml(_scrub, true, _openHtml, false);
            c.Dispose();
            log.Info("Starting Run..done!");
            
        }
        private void Run()
        {
            log.Info("Starting PS Invoke");
            PSInvoker p = new PSInvoker();

            try
            {
                p.Invoke(_collectSessionData);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                log.Error(ex.Message);
            }
            log.Info("Starting PS Invoke...done!");
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
                CRegReader rr = new();
                return true;
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


        private void run_Click(object sender, RoutedEventArgs e)
        {
            if (!IsVbrServer())
            {
                string msg = "Server is not VBR Server. Please run the program on the actual VBR server";
                MessageBox.Show(msg);
                log.Error(msg);
                Environment.Exit(0);
            }
            LogUIAction("PopulateData");
            showProgressBar();

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                JustRun();
                //Import();
                //JustRunNoPs();
            }).ContinueWith(t =>
            {
                hideProgressBar();
            });
        }





        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string message = "This tool is offered as a free utility to assess your Veeam Configuration. It is offered \"at your own risk\"." +
                "\n\nThe tool will engage PowerShell and SQL to gather data and then output files and folders to local disk." +
                "\n\nThe files may be anonymized to hide sensitive data. Accepting the Terms means that the user agrees to execute this program in its full capacity.";

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo);
            if (res.ToString() == "Yes")
            {
                EnableRun();
            }

        }
        private void EnableRun()
        {
            run.IsEnabled = true;
        }
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
        private void Import(object sender, RoutedEventArgs e)
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
