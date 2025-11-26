// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;

namespace VeeamHealthCheck.Shared
{
    internal class CMessages
    {
        private static string ProcEnd = "DONE!";

        public static string helpMenu = @"
Veeam Health Check - Command Line Help

USAGE: VeeamHealthCheck.exe [options]

BASIC COMMANDS:
  /run              Execute health check via CLI
  /gui              Launch graphical user interface
  /help             Show this help menu

REPORTING OPTIONS:
  /days:<N>         Set reporting interval (7, 30, 90, 12 days). Default: 7
  /lite             Skip individual job HTML exports (faster)
  /pdf              Export report as PDF in addition to HTML
  /scrub:<true|false> Control sensitive data removal. Default: both versions

REMOTE OPERATIONS:
  /remote           Enable remote execution mode
  /host=<hostname>  Specify remote Veeam server hostname
  /creds=<user:pass> Provide credentials (or /creds to use stored)

SPECIAL MODES:
  /security         Run security-focused assessment only
  /import           Generate report from existing data (no new collection)
  /hotfix /path:<dir> Run hotfix detection on specified path

EXAMPLES:
  VeeamHealthCheck.exe /run
  VeeamHealthCheck.exe /run /days:30 /lite
  VeeamHealthCheck.exe /security /remote /host:vbr-server /creds=admin:password
  VeeamHealthCheck.exe /import /run
  VeeamHealthCheck.exe /hotfix /path=C:\VeeamUpdates

For more information, visit: https://github.com/VeeamHub/veeam-healthcheck
";        
public static string PsVbrConfigStart = "[PS] Enter Config Collection Invoker...";
        public static string PsVbrConfigDone = PsVbrConfigStart + ProcEnd;

        public static string PsVbrFunctionStart = "[PS] Enter Function Setter...";
        public static string PsVbrFunctionDone = PsVbrConfigStart + ProcEnd;

        public static string PsVbrConfigStartProc = "[PS][VBR Config] Starting PowerShell Process...";
        public static string PsVbrConfigStartProcDone = PsVbrConfigStartProc + ProcEnd;

        public static string PsVbrConfigProcId = "[PS][VBR Config] PowerShell Process started with ID: ";
        public static string PsVbrConfigProcIdDone = PsVbrConfigProcId + ProcEnd;


        public static string FoundHotfixesMessage(List<string> fixes)
        {
            string output = String.Format("\n\nThank you for running the Veeam Hotfix Detector." +
                "\n - HFD version: {0}" +
                "\n - Detected B&R Version: {1}" +
                "\n\n" +
                "The scan has found {2} hotfixes:\n", CVersionSetter.GetFileVersion(), CGlobals.VBRFULLVERSION, fixes.Count);
            foreach (var fix in fixes)
            {
                output += "\n - " + fix;
            }
            output += "\r\n \r\nPlease delay your upgrade until verification has been completed." +
                " To verify your system, please do the following:" +
                "\n\t1. Open a support case" +
                "\n\t2. Set the subject to: " +
                "\n\ta. Hotfix Detector Results" +
                "\n\t3. Paste the following as the body and await a reply from a Support Representative:" +
                "\n\nHello," +
                "\nI have used the Veeam Hotfix Detector on my Veeam Server. " +
                "I would like to verify if the following fixes are included in the " +
                "latest release of Veeam Backup & Replication.";

            foreach (var fix in fixes)
            {
                output += "\n - " + fix;
            }
            output += String.Format("\r\n \r\nPlease check these fixes and let me know if I " +
                "may safely upgrade my system." +
                "\r\n \r\nInstalled B&R Version: {0}" +
                "\r\nHotfix Detector Version: {1}" +
                "\r\n \r\nThank you,", CGlobals.VBRFULLVERSION, CVersionSetter.GetFileVersion());
            //"\r\n4.\tAllow the support representative to return an answer.\r\n"
            return output;
        }
    }
}
