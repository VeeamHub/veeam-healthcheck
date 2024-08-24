﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VeeamHealthCheck.Functions.Collection.PSCollections;
//using VeeamHealthCheck.Reporting.vsac;
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

        private readonly string[] _args;
        private CClientFunctions functions = new();


        public CArgsParser(string[] args)
        {
            _args = args;
            CGlobals.TOOLSTART = DateTime.Now;
        }
        public void ParseArgs()
        {
            //CGlobals.RunFullReport = true;
            LogInitialInfo();

            PSInvoker p = new();
            p.TryUnblockFiles();

            if (_args.Length == 0)
                ParseZeroArgs();
            else if (_args != null && _args.Length > 0)
                ParseAllArgs(_args);
            else
                LaunchUi(Handle(), true);




        }
        private void LogInitialInfo()
        {
            CClientFunctions f = new CClientFunctions();
            f.LogVersionAndArgs(_args);
            try { f.GetVbrVersion(); }
            catch (Exception ex) { }
            f.Dispose();
        }

        private void LaunchUi(IntPtr handle, bool hide)
        {
            CGlobals.Logger.Info("Executing GUI", false);
            CGlobals.RunFullReport = true;
            CGlobals.GUIEXEC = true;
            if (hide)
                ShowWindow(handle, SW_HIDE);
            var app = new System.Windows.Application();
            app.Run(new VhcGui());
        }
        private IntPtr Handle()
        {
            return GetConsoleWindow();
        }

        private void ParseZeroArgs()
        {
            var pos = Console.GetCursorPosition();
            //CGlobals.Logger.Warning("pos = " + pos.ToString(), false);
            if (pos == (0, 1) || pos == (0, 2))
            {
                CGlobals.Logger.Info("0s");
                LaunchUi(Handle(), true);
            }
            else
            {
                CGlobals.Logger.Info("not 0");
                Console.WriteLine(CMessages.helpMenu);
            }
        }
        private void ParseAllArgs(string[] args)
        {
            bool run = false;
            bool ui = false;
            bool runHfd = false;
            string _hfdPath = "";



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
                        //functions.RunHotfixDetector();
                        runHfd = true;
                        //Environment.Exit(0);
                        break;
                    case "/pdf":
                        CGlobals.EXPORTPDF = true;
                        break;
                    case var match when new Regex("/path=.*").IsMatch(a):
                        _hfdPath = ParsePath(a);
                        CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/PATH=.*").IsMatch(a):
                        _hfdPath = ParsePath(a);
                        CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/HOST=.*").IsMatch(a):
                        CGlobals.REMOTEEXEC = true;
                        CGlobals.REMOTEHOST = ParsePath(a);
                        //CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/host=.*").IsMatch(a):
                        CGlobals.REMOTEEXEC = true;
                        CGlobals.REMOTEHOST = ParsePath(a);
                        //CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                        //case var match when new Regex("outdir:.*").IsMatch(a):
                        //    string[] outputDir = a.Split(":");
                        //    targetDir = outputDir[1];
                        //    CGlobals.Logger.Info("Output directory: " + targetDir);
                        //    break;
                }
            }

            if (runHfd)
            {
                if(CGlobals.REMOTEEXEC)
                {
                    functions.RunHotfixDetector(_hfdPath, CGlobals.REMOTEHOST);
                }
                functions.RunHotfixDetector(_hfdPath, "");
            }

            else if (ui)
                LaunchUi(Handle(), false);
            else if (run)
            {
                if (CGlobals.IMPORT)
                    FullRun(targetDir);
                else if (CGlobals.REMOTEEXEC && CGlobals.REMOTEHOST == "")
                {
                    CGlobals.Logger.Warning("Remote execution selected but no host defined. Please define host: " +
                        "/host=HOSTNAME", false);
                    Environment.Exit(0);
                }
                else if(CGlobals.REMOTEEXEC && !CGlobals.RunSecReport)
                {
                    CGlobals.Logger.Warning("Remote execution not available for general Health Check. Please run the tool from a server hosting Veeam Backup & Replication", false);
                    Environment.Exit(0);
                }

                else if (CGlobals.REMOTEHOST != "" && CGlobals.RunSecReport)
                    FullRun(targetDir);
                else
                {
                    if (functions.ModeCheck() == "fail")
                    {
                        CGlobals.Logger.Error("No compatible software detected or remote host specified. Exiting.", false);
                        Environment.Exit(0);
                    }
                    else
                        FullRun(targetDir);
                }


            }

        }
        private string ParsePath(string input)
        {
            try
            {
                string[] outputDir = input.Split("=");
                return outputDir[1];
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Input path is invalide. Try again.");
                return null;
            }
        }
        private void Run(string targetDir)
        {
            CClientFunctions functions = new();
            functions.CliRun(targetDir);
        }
        private void FullRun(string targetDir)
        {
            CGlobals.Logger.Info("Starting RUN...", false);
            Run(targetDir);


            CGlobals.Logger.Info("Starting RUN...complete!", false);
            CGlobals.Logger.Info("Output is stored in " + targetDir, false);

        }

    }
}
