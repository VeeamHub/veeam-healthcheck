using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Resources
{
    internal class CClientFunctions : IDisposable
    {


        public CClientFunctions()
        {

        }
        public void Dispose() { }
        public void KbLinkAction(System.Windows.Navigation.RequestNavigateEventArgs args)
        {
            CGlobals.Logger.Info("[GUI]\tOpening KB Link");
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                WebBrowser w1 = new();

                var p = new Process();
                p.StartInfo = new ProcessStartInfo(args.Uri.ToString())
                {
                    UseShellExecute = true
                };
                p.Start();
            });
            CGlobals.Logger.Info("[GUI]\tOpening KB Link...done!");
        }
        public  void PreRunCheck()
        {
            CGlobals.Logger.Info("Starting Admin Check");
            CAdminCheck priv = new();
            if (!priv.IsAdmin())
            {
                string message = "Please run program as Administrator";
                MessageBox.Show(message);
                CGlobals.Logger.Error(message);
                Environment.Exit(0);
            }

            CGlobals.Logger.Info("Starting Admin Check...done!");
        }
        public  string ModeCheck()
        {
            CGlobals.Logger.Info("Checking processes to determine execution mode..");
            string title = ResourceHandler.GuiTitle;
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                if (process.ProcessName == "Veeam.Archiver.Service")
                {

                    CGlobals.IsVb365 = true;
                    CGlobals.Logger.Info("VB365 software detected");
                }
                if (process.ProcessName == "Veeam.Backup.Service")
                {
                    CGlobals.IsVbr = true;
                    CGlobals.Logger.Info("VBR software detected");
                }

            }
            if (CGlobals.IsVbr)
                return title + " - " + ResourceHandler.GuiTitleBnR;
            if (CGlobals.IsVb365)
                return title + " - " + ResourceHandler.GuiTitleVB365;
            if (CGlobals.IsVbr && CGlobals.IsVb365)
                return title + " - " + ResourceHandler.GuiTitleBnR + " & " + ResourceHandler.GuiTitleVB365;
            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
                return title + " - " + ResourceHandler.GuiImportModeOnly;
            else
                return title;
        }
        public  void ExecPSScripts()
        {
            CGlobals.Logger.Info("Starting PS Invoke", false);
            PSInvoker p = new PSInvoker();

            if (CGlobals.IsVbr)
            {
                try
                {
                    CGlobals.Logger.Info("Entering vbr ps invoker", false);
                    p.Invoke();
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error(ex.Message);
                }
            }
            if (CGlobals.IsVb365)
            {
                try
                {
                    CGlobals.Logger.Info("Entering vb365 ps invoker", false);
                    p.InvokeVb365Collect();
                }
                catch (Exception ex) { CGlobals.Logger.Error(ex.Message); }
            }

            CGlobals.Logger.Info("Starting PS Invoke...done!", false);
        }
        public  bool AcceptTerms()
        {
            string message = ResourceHandler.GuiAcceptText;

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo);
            if (res.ToString() == "Yes")
                return true;
            else return false;
        }
        public  void RunClickAction()
        {
            CGlobals.Logger.Info("Starting Run");
            ExecPSScripts();
            PopulateWaits();
            if (CGlobals.IsVbr)
            {
                Collection.LogParser.CLogOptions logOptions = new("vbr");
            }
            if (CGlobals.IsVb365)
            {
                Collection.LogParser.CLogOptions logOptions = new("vb365");
            }





            CGlobals.Logger.Info(ClientSettingsString());
            Import();


            CGlobals.Logger.Info("Creating Report done!");




            CGlobals.Logger.Info("Starting Run..done!");
        }
        public  void CliRun(string targetForOutput)
        {
            CGlobals.Logger.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            CGlobals.OpenExplorer = false;
            CGlobals.OpenHtml = false;
            CGlobals._desiredPath = targetForOutput;
            RunClickAction();
        }
        public bool VerifyPath()
        {
            try
            {
                if (!Directory.Exists(CGlobals._desiredPath))
                    Directory.CreateDirectory(CGlobals._desiredPath);
                return true;
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("[UI] Desired dir does not exist and cannot be created. Error: ");
                CGlobals.Logger.Error("\t" + e.Message);
                return false;
            }
        }
        private  void PopulateWaits()
        {
            try
            {
                FilesParser.CLogParser lp = new();
                lp.GetWaitsFromFiles();
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Error checking log files:");
                CGlobals.Logger.Error(e.Message);
            }

        }
        public  void Import()
        {
            CReportModeSelector cMode = new();
            cMode.Run();
            cMode.Dispose();
        }
        private  string ClientSettingsString()
        {
            return String.Format(
                "User Settings:\n" +
                "\t\t\t\t\t\t\t\t\tScrub = {0}\n" +
                "\t\t\t\t\t\t\t\t\tOpen HTML = {1}\n" +
                "\t\t\t\t\t\t\t\t\tOpen Explorer = {2}\n" +
                "\t\t\t\t\t\t\t\t\tPath = {3}\n" +
                "\t\t\t\t\t\t\t\t\tInterval = {4}",
                CGlobals.Scrub, CGlobals.OpenHtml, CGlobals.OpenExplorer, CGlobals._desiredPath, CGlobals.ReportDays.ToString()
                );
        }
        public void LogUIAction(string message)
        {
            string s = string.Format("[Veeam.HC.UI]\tSelected:" + message);
            CGlobals.Logger.Info(s);
        }
        private void WriteVhcVersion()
        {
            CGlobals.Logger.Info("vHC Version: " + CVersionSetter.GetFileVersion());
        }
        private void WriteCliArgs(string[] args)
        {
            CGlobals.Logger.Info("Args count = " + args.Count().ToString());
            foreach (var arg in args)
                CGlobals.Logger.Info("Input: " + arg);
        }
        public void LogVersionAndArgs(string[] args)
        {
            WriteVhcVersion();
            WriteCliArgs(args);
        }

    }
}
