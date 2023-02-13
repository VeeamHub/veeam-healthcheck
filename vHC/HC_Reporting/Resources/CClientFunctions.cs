﻿using System;
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
            if (!CGlobals.IMPORT)
            {
                CCollections collect = new();
                collect.Run();
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