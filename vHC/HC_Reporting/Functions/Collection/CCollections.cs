// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
//using DocumentFormat.OpenXml.Drawing;
using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection
{
    internal class CCollections
    {
        public bool SCRIPTSUCCESS;
        public CCollections() { }
        /* All collection utilities should run through here:
         * - powershell
         * - SQL
         * - Logs
         * - Other?
         * 
         */
        public void Run()
        {
            if (CGlobals.RunSecReport)
                ExecSecurityCollection();

            ExecPSScripts();
            // run diagnostic of CSV output and sizes, dump to logs:
            if (CGlobals.IsVbr)
                GetCsvFileSizesToLog();
            // GetCsvFileSizesToLog();
            CheckRecon();



            if (!CGlobals.RunSecReport && CGlobals.IsVbr)
            {
                PopulateWaits();
            }

            if (CGlobals.IsVbr)
            {
                ExecVmcReader();
                GetRegistryDbInfo();
                if (CGlobals.DBTYPE != CGlobals.PgTypeName)
                    ExecSqlQueries();
            }


        }
        private void CheckRecon()
        {
            if (CGlobals.DEBUG)
            {
                CGlobals.Logger.Debug("Checking for Coveware Recon Task");

            }
            CReconChecker rc = new();
            rc.Check();
        }
        private void GetCsvFileSizesToLog()
        {
            if (CGlobals.DEBUG)
                CGlobals.Logger.Debug("Logging CSV File Sizes:");
            var files = Directory.GetFiles(CVariables.vbrDir, "*.csv", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileSize = fileInfo.Length;
                if (fileSize > 0)
                {
                    CGlobals.Logger.Info($"\tFile: {fileInfo.Name} Size: {fileSize}");

                }
                else
                {
                    CGlobals.Logger.Warning($"\tFile: {fileInfo.Name} Size: {fileSize}");
                }

            }

        }

        private void ExecSecurityCollection()
        {
            CSecurityInit securityInit = new CSecurityInit();
            securityInit.Run();
        }
        private void ExecVmcReader()
        {
            if (CGlobals.IsVbr)
            {
                CLogOptions logOptions = new("vbr");

            }
            if (CGlobals.IsVb365)
            {
                CLogOptions logOptions = new("vb365");
            }
        }
        private void GetRegistryDbInfo()
        {
            CRegReader reg = new CRegReader();
            reg.GetDbInfo();

            if (CGlobals.REMOTEEXEC)
                CGlobals.DEFAULTREGISTRYKEYS = reg.DefaultVbrKeysRemote();
            else
                CGlobals.DEFAULTREGISTRYKEYS = reg.DefaultVbrKeys();
        }
        private void ExecSqlQueries()
        {
            CSqlExecutor sql = new();
            sql.Run();
        }
        private void ExecPSScripts()
        {
            CGlobals.Logger.Info("Starting PS Invoke", false);
            PSInvoker p = new PSInvoker();

            if (!CGlobals.RunSecReport)
            {
                try
                {

                    if (CGlobals.IsVbr || CGlobals.REMOTEEXEC)
                    {
                        CGlobals.Logger.Info("Checking VBR MFA Access...", false);
                        if (MfaTestPassed(p))
                        {
                            // add debug logging to help troubleshoot MFA issues
                            CGlobals.Logger.Debug("MFA Not detected, continuing...");
                            //if (CGlobals.IsVbr || CGlobals.REMOTEEXEC)
                                ExecVbrScripts(p);
                        }
                        else
                        {
                            WeighSuccessContinuation();
                        }
                    }
                    if (CGlobals.IsVb365)
                    {
                        CGlobals.Logger.Info("Checking VB365 MFA Access...", false);
                        if (!TestPsMfaVb365(p))
                        {
                            if (CGlobals.IsVb365)
                                ExecVb365Scripts(p);
                        }
                        else
                        {
                            WeighSuccessContinuation();
                        }
                    }


                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error(ex.Message);
                }
            }
            else if (CGlobals.RunSecReport)
            {
                ExecVbrConfigOnly(p);
            }

            //WeighSuccessContinuation();

            CGlobals.Logger.Info("Starting PS Invoke...done!", false);
        }
        private void WeighSuccessContinuation()
        {
            string error = "Script execution has failed. Exiting program. See log for details:\n\t " + CGlobals.Logger._logFile;


            if (CGlobals.GUIEXEC && !SCRIPTSUCCESS)
            {
                CGlobals.Logger.Error(error, false);

                MessageBox.Show(error, "Error", button: MessageBoxButton.OK, icon: MessageBoxImage.Error, MessageBoxResult.Yes);

                Environment.Exit(1);
            }
            else if (!SCRIPTSUCCESS)
            {
                CGlobals.Logger.Error(error, false);
                Environment.Exit(1);
            }
        }

        private bool MfaTestPassed(PSInvoker p)
        {
            if ((CGlobals.IsVbr))
            {
                return RunLocalMfaCheck(p);
            }
            else
            {
                CredsHandler ch = new();
                var creds = ch.GetCreds();
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    @"Functions\Collection\PSCollections\Scripts\TestMfa.ps1");
                bool result = false;
                string error = "";
                List<string> output = new();

                string pwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
                if (!File.Exists(pwshPath))
                {
                    CGlobals.Logger.Debug("PowerShell 7 not found at: " + pwshPath, false);

                }

                try
                {
                    // Build PowerShell arguments to call the script with parameters
                    string args =
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Server {CGlobals.REMOTEHOST} -Username {creds.Value.Username} -Password {creds.Value.Password}";
                    //Ps7Executor ps7 = new();
                    //ps7.LogPowerShellVersion();
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = pwshPath, // Use Windows PowerShell for Veeam module
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    // Log processInfo settings
                    CGlobals.Logger.Debug($"ProcessStartInfo Settings:\n  FileName: {processInfo.FileName}\n  Arguments: {processInfo.Arguments}\n  RedirectStandardOutput: {processInfo.RedirectStandardOutput}\n  RedirectStandardError: {processInfo.RedirectStandardError}\n  UseShellExecute: {processInfo.UseShellExecute}\n  CreateNoWindow: {processInfo.CreateNoWindow}");
                    using var process = System.Diagnostics.Process.Start(processInfo);
                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    error = stdErr;
                    if (!string.IsNullOrWhiteSpace(stdOut))
                        output.Add(stdOut);
                    if (!string.IsNullOrWhiteSpace(stdErr))
                        output.Add(stdErr);
                    result = process.ExitCode == 0;
                    // Log output for debugging
                    CGlobals.Logger.Debug($"MFA Test Output (ExitCode={process.ExitCode}):");
                    foreach (var line in output)
                    {
                        CGlobals.Logger.Debug(line);
                    }
                }
                catch (Exception ex)
                {
                    CGlobals.Logger.Error("Error during MFA test:");
                    CGlobals.Logger.Error(ex.Message);
                    result = false;
                }

                if (!result)
                {
                    RunLocalMfaCheck(p);

                }
            return result;

            }
        }
        private bool RunLocalMfaCheck(PSInvoker p)
        {
            CGlobals.Logger.Warning("Failing over to PowerShell 5", false);
            try
            {
                var result = p.TestMfa();
                CGlobals.Logger.Info("[MFA Test] Result: " + result.ToString(), false);
                return result;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error("Error during MFA test:");
                CGlobals.Logger.Error(ex.Message);
                return false;
            }
        }
        private bool TestPsMfaVb365(PSInvoker p)
        {
            //CScripts scripts = new();

            return p.TestMfaVB365();
        }
        private void ExecVbrScripts(PSInvoker p)
        {
            //debug log evaluation of what to run
            CGlobals.Logger.Debug("DEBUG: Evaluating PS Script Execution Conditions:");
            CGlobals.Logger.Debug("IsVbr: " + CGlobals.IsVbr.ToString());
            CGlobals.Logger.Debug("REMOTEEXEC: " + CGlobals.REMOTEEXEC.ToString());


            if (CGlobals.IsVbr || CGlobals.REMOTEEXEC)
            {
                CGlobals.Logger.Info("Entering vbr ps invoker", false);
                SCRIPTSUCCESS = p.Invoke();
            }
        }
        private void ExecVbrConfigOnly(PSInvoker p)
        {
            CGlobals.Logger.Info("Entering vbr config collection");
            SCRIPTSUCCESS = p.RunVbrConfigCollect();
        }
        private void ExecVb365Scripts(PSInvoker p)
        {
            if (CGlobals.IsVb365)
            {
                CGlobals.Logger.Info("Entering vb365 ps invoker", false);
                //p.InvokeVb365CollectEmbedded();
                p.InvokeVb365Collect();
            }
        }
        private void PopulateWaits()
        {
            try
            {
                CLogParser lp = new();
                lp.GetWaitsFromFiles();
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Error checking log files:");
                CGlobals.Logger.Error(e.Message);
            }

        }
    }
}
