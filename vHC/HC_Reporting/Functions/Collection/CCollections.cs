// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Shared;
using Microsoft.Management.Infrastructure;
using VeeamHealthCheck.Functions.Collection.PowerShell;

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
            if (!CGlobals.RunSecReport)
            {
                PopulateWaits();
            }

            ExecVmcReader();
            GetRegistryDbInfo();
            if (CGlobals.DBTYPE != CGlobals.PgTypeName)
                ExecSqlQueries();

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
                    ExecVbrScripts(p);
                    ExecVb365Scripts(p);
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

            WeighSuccessContinuation();

            CGlobals.Logger.Info("Starting PS Invoke...done!", false);
        }
        private void WeighSuccessContinuation()
        {
            if (!SCRIPTSUCCESS)
            {
                CGlobals.Logger.Error("Script execution has failed. Exiting program. See log for details.", false);
                Environment.Exit(1);
            }
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
