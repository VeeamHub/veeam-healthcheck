// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.CredsWindow;

// using VeeamHealthCheck.Reporting.vsac;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
{
    internal class CArgsParser
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private readonly string[] args;
        private readonly CClientFunctions functions = new();

        public CArgsParser(string[] args)
        {
            this.args = args;
            CGlobals.TOOLSTART = DateTime.Now;
        }

        public int InitializeProgram()
        {
            // CGlobals.RunFullReport = true;
            this.LogInitialInfo();

            PSInvoker p = new();
            p.TryUnblockFiles();

            if (this.args.Length == 0){
                CGlobals.Logger.Debug("No arguments provided. Launching GUI", false);
                return this.LaunchUi(this.Handle(), true);
            }
            else if (this.args != null && this.args.Length > 0)
                return this.ParseAllArgs(this.args);
            else
                return this.LaunchUi(this.Handle(), false);
        }

        private void LogInitialInfo()
        {
            CClientFunctions f = new CClientFunctions();
            f.LogVersionAndArgs(this.args);
            // Note: VBR version detection is deferred until after argument parsing
            // so that REMOTEEXEC flag can be properly set first
            f.Dispose();
        }

        private int LaunchUi(IntPtr handle, bool hide)
        {
            CGlobals.Logger.Info("Executing GUI", false);
            CGlobals.RunFullReport = true;
            CGlobals.GUIEXEC = true;

            // if (hide)
            //     ShowWindow(handle, SW_HIDE);
            var app = new System.Windows.Application();
            return app.Run(new VhcGui());
        }

        private IntPtr Handle()
        {
            return GetConsoleWindow();
        }

        // private int ParseZeroArgs()
        // {
        //    var pos = Console.GetCursorPosition();
        //    CGlobals.Logger.Debug("pos = " + pos.ToString(), false);
        //    if (pos == (0, 1) || pos == (0, 2))
        //    {
        //        CGlobals.Logger.Info("0s");
        //         return LaunchUi(Handle(), true);
        //    }
        //    else
        //    {
        //        CGlobals.Logger.Info("not 0");
        //        Console.WriteLine(CMessages.helpMenu);
        //        return 0;
        //    }
        // }
        private int ParseAllArgs(string[] args)
        {
            bool run = false;
            bool ui = false;
            bool runHfd = false;
            string _hfdPath = string.Empty;

            string targetDir = @"C:\temp\vHC";
            foreach (var a in args)
            {
                switch (a)
                {
                    case "/help":
                        CGlobals.Logger.Info("entering help menu", false);
                        Console.WriteLine(CMessages.helpMenu);
                        break;
                    case "/run":
                        run = true;
                        CGlobals.RunFullReport = true;
                        CGlobals.Logger.Info("Run = true");
                        break;
                    case "/show:files":
                        CGlobals.OpenExplorer = true;
                        break;
                    case "/show:report":
                        CGlobals.OpenHtml = true;
                        break;
                    case "/show:all":
                        break;
                    case "/days:7":
                        CGlobals.Logger.Info("Days set to 7");
                        CGlobals.ReportDays = 7;
                        break;
                    case "/days:30":
                        CGlobals.Logger.Info("Days set to 30");
                        CGlobals.ReportDays = 30;
                        break;
                    case "/days:90":
                        CGlobals.Logger.Info("Days set to 90");
                        CGlobals.ReportDays = 90;
                        break;
                    case "/days:12":
                        CGlobals.Logger.Info("Days set to 12");
                        CGlobals.ReportDays = 12;
                        break;
                    case "/gui":
                        CGlobals.RunFullReport = true;
                        ui = true;
                        break;
                    case "/lite":
                        run = true;
                        CGlobals.EXPORTINDIVIDUALJOBHTMLS = false;
                        break;
                    case "/import":
                        run = true;
                        CGlobals.IMPORT = true;
                        CGlobals.RunFullReport = true;
                        break;
                    case var importMatch when new Regex("^/import[:=](.+)$", RegexOptions.IgnoreCase).IsMatch(a):
                        run = true;
                        CGlobals.IMPORT = true;
                        CGlobals.RunFullReport = true;
                        CGlobals.IMPORT_PATH = this.ParseImportPath(a);
                        if (!string.IsNullOrEmpty(CGlobals.IMPORT_PATH))
                        {
                            CGlobals.Logger.Info("Import path set to: " + CGlobals.IMPORT_PATH);
                        }
                        break;
                    case "/security":
                        run = true;
                        CGlobals.EXPORTINDIVIDUALJOBHTMLS = false;
                        CGlobals.RunSecReport = true;
                        break;
                    case "/remote":
                        CGlobals.REMOTEEXEC = true;
                        break;
                    case "/scrub:true":
                        CGlobals.Logger.Info("Setting SCRUB = true", false);
                        CGlobals.Scrub = true;
                        break;
                    case "/scrub:false":
                        CGlobals.Logger.Info("Setting SCRUB = false", false);
                        CGlobals.Scrub = false;
                        break;
                    case "/hotfix":
                        // functions.RunHotfixDetector();
                        runHfd = true;

                        // Environment.Exit(0);
                        break;
                    case "/clearcreds":
                        CGlobals.ClearStoredCreds = true;
                        CGlobals.Logger.Info("Clear stored credentials flag set", false);
                        break;
                    case "/pdf":
                        CGlobals.EXPORTPDF = true;
                        break;
                    case "/pptx":
                        CGlobals.EXPORTPPTX = true;
                        break;
                    case "/debug":
                        CGlobals.DEBUG = true;
                        break;
                    case var match when new Regex("/path=.*").IsMatch(a):
                        _hfdPath = this.ParsePath(a);
                        CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/PATH=.*").IsMatch(a):
                        _hfdPath = this.ParsePath(a);
                        CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/outdir=.*", RegexOptions.IgnoreCase).IsMatch(a):
                        string parsedOutDir = this.ParsePath(a);
                        if (!string.IsNullOrEmpty(parsedOutDir))
                        {
                            targetDir = parsedOutDir;
                            CGlobals.Logger.Info("Output directory overridden: " + targetDir, false);
                        }
                        break;
                    case var match when new Regex("/host=.*", RegexOptions.IgnoreCase).IsMatch(a):
                        string providedHost = this.ParsePath(a);

                        // Check if the provided host is actually the local machine (Issue #82)
                        if (CHostNameHelper.IsLocalHost(providedHost))
                        {
                            CGlobals.Logger.Info($"Detected /host={providedHost} is local machine - using local execution mode", false);
                            CGlobals.REMOTEEXEC = false;
                            CGlobals.REMOTEHOST = "localhost";
                        }
                        else
                        {
                            CGlobals.REMOTEEXEC = true;
                            CGlobals.REMOTEHOST = providedHost;
                        }
                        break;
                }
            }

            // Now that arguments are parsed, detect VBR version
            // This must happen after parsing so REMOTEEXEC flag is properly set
            try { this.functions.GetVbrVersion(); }
            catch (Exception ex)
            {
                CGlobals.Logger.Debug($"VBR version detection skipped: {ex.Message}");
            }

            int result = 0;
            if (string.IsNullOrEmpty(CGlobals.REMOTEHOST))
            {
                //CGlobals.REMOTEHOST = "localhost";
            }

            if (runHfd)
            {
                if(CGlobals.REMOTEEXEC)
                {
                    this.functions.RunHotfixDetector(_hfdPath, CGlobals.REMOTEHOST);
                }

                this.functions.RunHotfixDetector(_hfdPath, string.Empty);
            }
            else if (ui)
                this.LaunchUi(this.Handle(), false);
            else if (run)
            {
                if (CGlobals.IMPORT)
                     result = this.FullRun(targetDir);
                else if (CGlobals.REMOTEEXEC && CGlobals.REMOTEHOST == string.Empty)
                {
                    CGlobals.Logger.Warning("Remote execution selected but no host defined. Please define host: " +
                        "/host=HOSTNAME", false);
                    Environment.Exit(0);
                }

                // else if(CGlobals.REMOTEEXEC && !CGlobals.RunSecReport)
                // {
                //    CGlobals.Logger.Warning("Remote execution not available for general Health Check. Please run the tool from a server hosting Veeam Backup & Replication", false);
                //    Environment.Exit(0);
                // }
                else if (CGlobals.REMOTEHOST != string.Empty && CGlobals.RunSecReport)
                {
                    CGlobals.Logger.Debug("Remote execution selected with host: " + CGlobals.REMOTEHOST, false);
                    result = this.FullRun(targetDir);
                }
                else if(CGlobals.REMOTEHOST != string.Empty)
                {
                    CGlobals.Logger.Debug("Remote execution selected with host: " + CGlobals.REMOTEHOST, false);
                    result = this.FullRun(targetDir);
                }
                else
                {
                    if (this.functions.ModeCheck() == "fail")
                    {
                        CGlobals.Logger.Error("No compatible software detected or remote host specified. Exiting.", false);
                        Environment.Exit(0);
                    }
                    else
                        result = this.FullRun(targetDir);
                }
            }

            return result;
        }

        private string ParsePath(string input)
        {
            try
            {
                string[] outputDir = input.Split('=', 2);
                return outputDir[1];
            }
            catch (Exception)
            {
                CGlobals.Logger.Error("Input path is invalide. Try again.");
                return null;
            }
        }

        /// <summary>
        /// Parse import path from /import:path or /import=path format.
        /// </summary>
        private string ParseImportPath(string input)
        {
            try
            {
                // Handle both /import:path and /import=path formats
                char separator = input.Contains('=') ? '=' : ':';
                int separatorIndex = input.IndexOf(separator);

                if (separatorIndex < 0 || separatorIndex >= input.Length - 1)
                {
                    CGlobals.Logger.Error("Import path parameter is empty. Usage: /import:C:\\path\\to\\csvs");
                    return null;
                }

                string path = input.Substring(separatorIndex + 1).Trim();

                // Validate path exists
                if (!Directory.Exists(path))
                {
                    CGlobals.Logger.Error($"Import path does not exist: {path}");
                    CGlobals.Logger.Info("Please verify the path and try again.");
                    return null;
                }

                return path;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error($"Error parsing import path: {ex.Message}");
                return null;
            }
        }

        private int Run(string targetDir)
        {
            CClientFunctions functions = new();
            return functions.CliRun(targetDir);
        }

        private int FullRun(string targetDir)
        {
            CGlobals.Logger.Info("Starting RUN...", false);
            var res = this.Run(targetDir);

            CGlobals.Logger.Info("Starting RUN...complete!", false);
            CGlobals.Logger.Info("Output is stored in " + targetDir, false);

            return res;
        }
    }
}
