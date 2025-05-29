// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Shared;
using Microsoft.Management.Infrastructure;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using System.Windows;
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
            if(CGlobals.IsVbr)
                GetCsvFileSizesToLog();



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
        private void GetCsvFileSizesToLog(){
            if(CGlobals.DEBUG)
                CGlobals.Logger.Debug("Logging CSV File Sizes:");
            var files = Directory.GetFiles(CVariables.vbrDir, "*.csv", SearchOption.AllDirectories);
            foreach (var file in files){
                var fileInfo = new FileInfo(file);
                var fileSize = fileInfo.Length;
                if(fileSize > 0){
                CGlobals.Logger.Info($"\tFile: {fileInfo.Name} Size: {fileSize}");

                }
                else{
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

                    if (CGlobals.IsVbr)
                    {
                        CGlobals.Logger.Info("Checking VBR MFA Access...", false);
                        if (!TestPsMFA(p))
                        {
                            if (CGlobals.IsVbr)
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
                        if (!TestPsMFAVb365(p))
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
                
                MessageBox.Show(error,"Error", button:MessageBoxButton.OK, icon:MessageBoxImage.Error, MessageBoxResult.Yes);
                
                Environment.Exit(1);
            }
            else if (!SCRIPTSUCCESS)
            {
                CGlobals.Logger.Error(error, false);
                Environment.Exit(1);
            }
        }
        private bool TestPsMFA(PSInvoker p)
        {
            CScripts scripts = new();

            return p.TestMfa();
        }
        private bool TestPsMFAVb365(PSInvoker p)
        {
            CScripts scripts = new();

            return p.TestMfaVB365();
        }
        private void ExecVbrScripts(PSInvoker p)
        {
            if (CGlobals.IsVbr)
            {
                CGlobals.Logger.Info("Entering vbr ps invoker", false);
                SCRIPTSUCCESS =  p.Invoke();
            }
        }
        private void ExecVbrConfigOnly(PSInvoker p)
        {
            CGlobals.Logger.Info("Entering vbr config collection");
            SCRIPTSUCCESS =  p.RunVbrConfigCollect();
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
