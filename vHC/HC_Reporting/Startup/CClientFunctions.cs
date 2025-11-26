// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
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
            Application.Current.Dispatcher.Invoke(delegate
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

        public void PreRunCheck()
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

        private void VbrVersionSupportCheck()
        {
            //GetVbrVersion();

            // get the version of the current vhc software:
            if(CGlobals.VBRMAJORVERSION < 12)
            {
                string[] vhcVersionSections = CGlobals.VHCVERSION.Split('.'); 
                int.TryParse(vhcVersionSections[0], out int vhcMajorVersion);
                int.TryParse(vhcVersionSections[3], out int vhcBuildVersion);

                if(vhcMajorVersion >= 2 && vhcBuildVersion > 546)
                {
                    string msg = String.Format("Veeam Health Check version {0} does not support Veeam Backup & Replication Versions prior to v12. To check systems prior to v12, Please download 2.0.0.546: https://github.com/VeeamHub/veeam-healthcheck/releases/tag/2.0.0.546", CGlobals.VHCVERSION);

                    LOG.Error(msg, false);

                    if (CGlobals.GUIEXEC)
                    {
                        MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    }
                    Environment.Exit(0);

                }
            }

        }

        public string ModeCheck()
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

                    // now get the VBR Version and store as global variable to help direct which script(s) to use
                    //VbrVersionSupportCheck();
                }

            }
            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
            {
                CGlobals.Logger.Error("No Veeam Software detected. Is this server the VBR or VB365 management server?", false);
                CGlobals.Logger.Warning("\tTry connecting to a remote server with /remote /host=hostname");
                return "fail";
            }

            if (CGlobals.IsVbr && CGlobals.IsVb365)
                return title + " - " + VbrLocalizationHelper.GuiTitleBnR + " & " + VbrLocalizationHelper.GuiTitleVB365;
            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
                return title + " - " + VbrLocalizationHelper.GuiImportModeOnly;
            if (CGlobals.IsVbr)
                return title + " - " + VbrLocalizationHelper.GuiTitleBnR;
            if (CGlobals.IsVb365)
                return title + " - " + VbrLocalizationHelper.GuiTitleVB365;
            else
                return title;
        }

        public bool AcceptTerms()
        {
            string message = VbrLocalizationHelper.GuiAcceptText;

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo,MessageBoxImage.Question);
            if (res.ToString() == "Yes")
                return true;
            else return false;
        }

        public int StartPrimaryFunctions()
        {
            LogUserSettings();
            StartCollections();
            return StartAnalysis();
        }

        public void RunHotfixDetector(string path, string remoteServer)
        {
            LOG.Info(logStart + "Starting Hotfix Detector", false);
            GetVbrVersion();
            if (!String.IsNullOrEmpty(path))
            {
                if (!VerifyPath(path))
                {
                    string error = String.Format("Entered path \"{0}\" is invalid or doesn't exist. Try a different path", path);
                    LOG.Error(logStart + error, false);
                    return;
                    //LOG.Warning(logStart + "This option will collect support logs to some local directory and then check for hotfixes", false);
                    //LOG.Warning(logStart + "Please enter local path with adequate space for log files:", false);
                    //path = Console.ReadLine();
                }

            }
            else
            {
                LOG.Warning(logStart + "/path= variable is empty or missing.");
                //    "\nPlease retry with syntax:" +
                //    "\nVeeamHealthCheck.exe /hotfix /path:C:\\examplepath", false);
                LOG.Warning(logStart + "This option will collect support logs to some local directory and then check for hotfixes", false);
                LOG.Warning(logStart + "Please enter local path with adequate space for log files:", false);
                path = Console.ReadLine();
            }

            CHotfixDetector hfd = new(path);
            hfd.Run();
        }

        public bool VerifyPath(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            if (path.StartsWith("\\\\")) return false;
            if (Directory.Exists(path)) return true;
            if(TryCreateDir(path)) return true;
            else return false;
        }

        private bool TryCreateDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch {
                LOG.Error("Failed to create directory.", false);
                return false; }

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

                if (CGlobals.REMOTEEXEC && CGlobals.RunSecReport)
                {
                    CImpersonation cImpersonation = new CImpersonation();
                    cImpersonation.RunCollection();

                }
                else if (CGlobals.REMOTEEXEC)
                {
                    CCollections collect = new();
                    collect.Run();
                }
                else
                {
                    CCollections collect = new();
                    collect.Run();
                }

                LOG.Info(logStart + "Init Collections...done!", false);
            }
        }

        private int StartAnalysis()
        {
            LOG.Info(logStart + "Init Data analysis & report creations", false);
            int res = Import();

            LOG.Info(logStart + "Init Data analysis & report creations...done!", false);
            return res;
        }

        public int CliRun(string targetForOutput)
        {
            CGlobals.Logger.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            CGlobals.OpenExplorer = false;
            //CGlobals.OpenHtml = false;
            CGlobals._desiredPath = targetForOutput;
            if(!CGlobals.IMPORT)
                PreRunCheck();
            //GetVbrVersion();
            
            
            try // REST TEST AREA
            {
                //RestInvoker restInvoker = new RestInvoker();
                //restInvoker.Run();
            }
            catch(Exception ex)
            {

            }
            
            return StartPrimaryFunctions();
        }

        public void GetVbrVersion()
        {
            try
            {

            CRegReader reg = new();
            LOG.Info(logStart + "VBR Version: " + reg.GetVbrVersionFilePath(),  false);
            }
            catch(Exception e) { }
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

        public int Import()
        {
            CReportModeSelector cMode = new();
            int res = cMode.Run();
            cMode.Dispose();
            return res;
        }

        private string ClientSettingsString()
        {
            return string.Format(
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
                CGlobals.Logger.Info("\tInput: " + arg);
        }

        public void LogVersionAndArgs(string[] args)
        {
            WriteVhcVersion();
            WriteCliArgs(args);
        }

    }
}
