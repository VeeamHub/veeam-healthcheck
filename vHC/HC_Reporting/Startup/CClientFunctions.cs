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
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
{
    internal class CClientFunctions : IDisposable
    {
        private readonly CLogger LOG = CGlobals.Logger;
        private readonly string logStart = "[Functions]\t";

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
                // Admin check is only required for local VBR execution (not remote)
                // Remote execution does not require admin privileges
                if (CGlobals.IsVbr && !CGlobals.REMOTEEXEC)
                {
                    // Local VBR execution without admin - offer to continue with limitations
                    if (CGlobals.GUIEXEC)
                    {
                        // GUI execution - show dialog
                        string message = "Administrator privileges are recommended when running locally against Veeam Backup & Replication.\n\n" +
                                       "Running without administrator privileges will:\n" +
                                       "• Skip some registry checks\n" +
                                       "• Skip some security assessments\n" +
                                       "• May result in incomplete data collection\n\n" +
                                       "For best results, please:\n" +
                                       "1. Close this window\n" +
                                       "2. Right-click VeeamHealthCheck.exe\n" +
                                       "3. Select 'Run as Administrator'\n\n" +
                                       "Do you want to continue without administrator privileges?";
                        
                        var result = MessageBox.Show(message, "Administrator Privileges Recommended", 
                                                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        
                        if (result == MessageBoxResult.No)
                        {
                            CGlobals.Logger.Info("User declined to run without admin privileges", false);
                            Environment.Exit(0);
                        }
                        
                        // User chose to continue without admin
                        CGlobals.RunningWithoutAdmin = true;
                        CGlobals.Logger.Warning("Running without administrator privileges - some features will be limited", false);
                    }
                    else
                    {
                        // CLI execution - just warn and continue
                        CGlobals.RunningWithoutAdmin = true;
                        CGlobals.Logger.Warning("Running without administrator privileges - some registry checks and security assessments will be skipped", false);
                    }
                }
                else if (CGlobals.IsVb365 && !CGlobals.REMOTEEXEC)
                {
                    // Local VB365 requires admin
                    string message = "Please run program as Administrator";
                    if (CGlobals.GUIEXEC)
                    {
                        MessageBox.Show(message);
                    }
                    CGlobals.Logger.Error(message, false);
                    Environment.Exit(0);
                }
                // else: Remote execution - no admin required, continue normally
            }

            CGlobals.Logger.Info("Starting Admin Check...done!");
        }

        private void VbrVersionSupportCheck()
        {
            // GetVbrVersion();

            // get the version of the current vhc software:
            if(CGlobals.VBRMAJORVERSION < 12)
            {
                string[] vhcVersionSections = CGlobals.VHCVERSION.Split('.'); 
                int.TryParse(vhcVersionSections[0], out int vhcMajorVersion);
                int.TryParse(vhcVersionSections[3], out int vhcBuildVersion);

                if(vhcMajorVersion >= 2 && vhcBuildVersion > 546)
                {
                    string msg = String.Format("Veeam Health Check version {0} does not support Veeam Backup & Replication Versions prior to v12. To check systems prior to v12, Please download 2.0.0.546: https://github.com/VeeamHub/veeam-healthcheck/releases/tag/2.0.0.546", CGlobals.VHCVERSION);

                    this.LOG.Error(msg, false);

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
                // LOG.Warning(logStart + "process name: " + process.ProcessName);
                if (process.ProcessName == "Veeam.Archiver.Service")
                {
                    CGlobals.IsVb365 = true;
                    this.LOG.Info("VB365 software detected", false);
                }

                if (process.ProcessName == "Veeam.Backup.Service")
                {
                    CGlobals.IsVbr = true;
                    CGlobals.IsVbrInstalled = true;
                    this.LOG.Info("VBR software detected", false);

                    // Get VBR version to determine correct PowerShell version (PS7 for VBR 13+, PS5 for VBR 12-)
                    this.GetVbrVersion();
                }
            }

            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
            {
                CGlobals.Logger.Error("No Veeam Software detected. Is this server the VBR or VB365 management server?", false);
                CGlobals.Logger.Warning("\tTry connecting to a remote server with /remote /host=hostname");
                return "fail";
            }

            if (CGlobals.IsVbr && CGlobals.IsVb365)
            {

                return title + " - " + VbrLocalizationHelper.GuiTitleBnR + " & " + VbrLocalizationHelper.GuiTitleVB365;
            }


            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
            {

                return title + " - " + VbrLocalizationHelper.GuiImportModeOnly;
            }

            if (CGlobals.IsVbr)
            {
                return title + " - " + VbrLocalizationHelper.GuiTitleBnR;
            }


            if (CGlobals.IsVb365)
            {

                return title + " - " + VbrLocalizationHelper.GuiTitleVB365;
            }
            else
            {

                return title;
            }
        }

        public bool AcceptTerms()
        {
            string message = VbrLocalizationHelper.GuiAcceptText;

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo,MessageBoxImage.Question);
            if (res.ToString() == "Yes")
            {

                return true;
            }
            else
            {
                return false;
            }
        }

        public int StartPrimaryFunctions()
        {
            // Single server execution - CGlobals.VBRServerName should already be set from the selected server in the UI
            this.LogUserSettings();
            this.StartCollections();
            return this.StartAnalysis();
        }

        public void RunHotfixDetector(string path, string remoteServer)
        {
            this.LOG.Info(this.logStart + "Starting Hotfix Detector", false);
            this.GetVbrVersion();
            if (!String.IsNullOrEmpty(path))
            {
                if (!this.VerifyPath(path))
                {
                    string error = String.Format("Entered path \"{0}\" is invalid or doesn't exist. Try a different path", path);
                    this.LOG.Error(this.logStart + error, false);
                    return;

                    // LOG.Warning(logStart + "This option will collect support logs to some local directory and then check for hotfixes", false);
                    // LOG.Warning(logStart + "Please enter local path with adequate space for log files:", false);
                    // path = Console.ReadLine();
                }
            }
            else
            {
                this.LOG.Warning(this.logStart + "/path= variable is empty or missing.");

                // "\nPlease retry with syntax:" +
                //    "\nVeeamHealthCheck.exe /hotfix /path:C:\\examplepath", false);
                this.LOG.Warning(this.logStart + "This option will collect support logs to some local directory and then check for hotfixes", false);
                this.LOG.Warning(this.logStart + "Please enter local path with adequate space for log files:", false);
                path = Console.ReadLine();
            }

            CHotfixDetector hfd = new(path);
            hfd.Run();
        }

        public bool VerifyPath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }


            if (path.StartsWith("\\\\"))
            {
                return false;
            }


            if (Directory.Exists(path))
            {
                return true;
            }

            if (this.TryCreateDir(path))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool TryCreateDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch {
                this.LOG.Error("Failed to create directory.", false);
                return false; }
        }

        private void LogUserSettings()
        {
            this.LOG.Info(this.ClientSettingsString(), false);
        }

        private void StartCollections()
        {
            if (!CGlobals.IMPORT)
            {
                this.LOG.Info(this.logStart + "Init Collections", false);

                if (CGlobals.REMOTEEXEC && CGlobals.RunSecReport)
                {
                    CImpersonation cImpersonation = new CImpersonation();
                    cImpersonation.RunCollection();
                }

                else
                {
                    CCollections collect = new();
                    collect.Run();
                }

                this.LOG.Info(this.logStart + "Init Collections...done!", false);
            }
        }

        private int StartAnalysis()
        {
            this.LOG.Info(this.logStart + "Init Data analysis & report creations", false);
            
            // Validate CSV files before report generation
            try
            {
                this.LOG.Info(this.logStart + "Validating collected CSV files...", false);
                var validator = new CCsvValidator(CVariables.vbrDir);
                CGlobals.CsvValidationResults = validator.ValidateVbrCsvFiles();
                this.LOG.Info(this.logStart + "CSV validation complete.", false);
            }
            catch (Exception ex)
            {
                this.LOG.Warning(this.logStart + $"CSV validation encountered an error: {ex.Message}. Continuing with report generation.", false);
            }
            
            int res = this.Import();

            this.LOG.Info(this.logStart + "Init Data analysis & report creations...done!", false);
            return res;
        }

        public int CliRun(string targetForOutput)
        {
            CGlobals.Logger.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            CGlobals.OpenExplorer = false;

            // CGlobals.OpenHtml = false;
            CGlobals.desiredPath = targetForOutput;
            if(!CGlobals.IMPORT)
            {
                this.PreRunCheck();
            }

            try // REST TEST AREA
            {
                // RestInvoker restInvoker = new RestInvoker();
                // restInvoker.Run();
            }
            catch (Exception)
            {
            }
            
            return this.StartPrimaryFunctions();
        }

        public void GetVbrVersion()
        {
            try
            {
            CRegReader reg = new();
            this.LOG.Info(this.logStart + "VBR Version: " + reg.GetVbrVersionFilePath(),  false);
            CGlobals.PowerShellVersion = CGlobals.VBRMAJORVERSION >= 13 ? 7 : 5;
            this.LOG.Info(this.logStart + "Using PowerShell version: " + CGlobals.PowerShellVersion.ToString(), false);
            
            // If PowerShell 7 is required AND we're doing remote execution, ensure we have credentials available
            // For local VBR (IsVbr=true, REMOTEEXEC=false), credentials are NOT required - Windows auth is used
            if (CGlobals.PowerShellVersion == 7 && CGlobals.REMOTEEXEC)
            {
                this.LOG.Info(this.logStart + "PowerShell 7 with remote execution requires credentials for VBR connection", false);
                this.EnsureCredentialsAvailable();
            }
            else if (CGlobals.PowerShellVersion == 7)
            {
                this.LOG.Info(this.logStart + "PowerShell 7 detected, but local VBR will use Windows authentication (no credentials required)", false);
            }
            }
            catch(Exception) { }
        }

        private void EnsureCredentialsAvailable()
        {
            string host = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;

            // Check if credentials are already stored for this host
            var storedCreds = CredentialStore.Get(host);
            if (storedCreds != null)
            {
                this.LOG.Info(this.logStart + $"Stored credentials found for host: {host}", false);
                return;
            }

            // No stored credentials found - log appropriate message based on mode
            // Note: Don't try to prompt here during early initialization.
            // The credential prompt will happen later when GetCreds() is called during collection,
            // at which point the GUI will be fully initialized (if in GUI mode).
            if (!CGlobals.GUIEXEC)
            {
                this.LOG.Warning(this.logStart + "No stored credentials found for PowerShell 7 connection.", false);
                this.LOG.Warning(this.logStart + "Credentials will be required. Please provide credentials via /creds=username:password parameter, or credentials will be prompted during collection.", false);
            }
            else
            {
                this.LOG.Info(this.logStart + "No stored credentials found. Credentials will be prompted when collection starts.", false);
            }
        }

        public bool VerifyPath()
        {
            try
            {
                if (!Directory.Exists(CGlobals.desiredPath))
                {
                    Directory.CreateDirectory(CGlobals.desiredPath);
                }


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
                CGlobals.Scrub, CGlobals.OpenHtml, CGlobals.OpenExplorer, CGlobals.desiredPath, CGlobals.ReportDays.ToString()
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
            {
                CGlobals.Logger.Info("\tInput: " + arg);
            }
        }

        public void LogVersionAndArgs(string[] args)
        {
            this.WriteVhcVersion();
            this.WriteCliArgs(args);
        }
    }
}
