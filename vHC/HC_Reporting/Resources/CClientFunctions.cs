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
using VeeamHealthCheck.Collection;
using VeeamHealthCheck.Security;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Resources
{
    internal class CClientFunctions : IDisposable
    {

        private CLogger LOG = CGlobals.Logger;
        private string logStart = "[Functions]\t";
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
            CGlobals.Logger.Info("Starting Admin Check", false);
            CAdminCheck priv = new();
            if (!priv.IsAdmin())
            {
                string message = "Please run program as Administrator";
                MessageBox.Show(message);
                CGlobals.Logger.Error(message, false);
                Environment.Exit(0);
            }

            CGlobals.Logger.Info("Starting Admin Check...done!");
        }
        public  string ModeCheck()
        {
            CGlobals.Logger.Info("Checking processes to determine execution mode..", false);
            string title = VbrLocalizationHelper.GuiTitle;
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                //LOG.Warning(logStart + "process name: " + process.ProcessName);
                if (process.ProcessName == "Veeam.Archiver.Service")
                {

                    CGlobals.IsVb365 = true;
                    LOG.Info("VB365 software detected", false);
                }
                if (process.ProcessName == "Veeam.Backup.Service")
                {
                    CGlobals.IsVbr = true;
                    LOG.Info("VBR software detected", false);
                }

            }
            if (CGlobals.IsVbr)
                return title + " - " + VbrLocalizationHelper.GuiTitleBnR;
            if (CGlobals.IsVb365)
                return title + " - " + VbrLocalizationHelper.GuiTitleVB365;
            if (CGlobals.IsVbr && CGlobals.IsVb365)
                return title + " - " + VbrLocalizationHelper.GuiTitleBnR + " & " + VbrLocalizationHelper.GuiTitleVB365;
            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
                return title + " - " + VbrLocalizationHelper.GuiImportModeOnly;
            else
                return title;
        }

        public  bool AcceptTerms()
        {
            string message = VbrLocalizationHelper.GuiAcceptText;

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo);
            if (res.ToString() == "Yes")
                return true;
            else return false;
        }

        public  void StartPrimaryFunctions()
        {
            LogUserSettings();
            StartCollections();
            StartAnalysis();
        }
        private void LogUserSettings()
        {
            LOG.Info(ClientSettingsString(), false);

        }
        private void StartCollections()
        {
            if (!CGlobals.IMPORT)
            {
                LOG.Info(logStart + "Init Collections", false);
                CCollections collect = new();
                collect.Run();
                LOG.Info(logStart + "Init Collections...done!", false);
            }
        }
        private void StartAnalysis()
        {
            LOG.Info(logStart + "Init Data analysis & report creations", false);
            Import();

            LOG.Info(logStart + "Init Data analysis & report creations...done!", false);
        }
        public  void CliRun(string targetForOutput)
        {
            CGlobals.Logger.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            CGlobals.OpenExplorer = false;
            //CGlobals.OpenHtml = false;
            CGlobals._desiredPath = targetForOutput;
            StartPrimaryFunctions();
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
                "\t\t\t\t\tScrub = {0}\n" +
                "\t\t\t\t\tOpen HTML = {1}\n" +
                "\t\t\t\t\tOpen Explorer = {2}\n" +
                "\t\t\t\t\tPath = {3}\n" +
                "\t\t\t\t\tInterval = {4}",
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
            CGlobals.Logger.Info("vHC Version: " + CVersionSetter.GetFileVersion(), false);
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
