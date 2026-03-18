// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;

namespace VeeamHealthCheck.Shared
{
    internal class CMessages
    {
        private static readonly string ProcEnd = "DONE!";

        public static string helpMenu = @"
Veeam Health Check - Command Line Help

USAGE: VeeamHealthCheck.exe [options]

BASIC COMMANDS:
  /run              Execute health check via CLI
  /gui              Launch graphical user interface
  /help             Show this help menu

REPORTING OPTIONS:
  /days:<N>         Set reporting interval (7, 12, 30, or 90 days). Default: 7
  /lite             Skip individual job HTML exports (faster execution)
  /outdir=<path>    Write all output to <path> instead of C:\temp\vHC
  /pdf              Export report as PDF in addition to HTML
  /pptx             Export report as PowerPoint presentation
  /scrub:true       Enable sensitive data removal (anonymize output)
  /scrub:false      Disable sensitive data removal (full detail)

DISPLAY OPTIONS:
  /show:files       Open file explorer after report generation
  /show:report      Open HTML report in browser after generation

REMOTE OPERATIONS:
  /remote           Enable remote execution mode
  /host=<hostname>  Specify remote Veeam server hostname (required with /remote)
                    Note: Credentials will be prompted if needed

SPECIAL MODES:
  /security         Run security-focused assessment only
  /import           Generate report from existing data (no new collection)
                     Uses default path: C:\temp\vHC
  /import:<path>    Generate report from CSV files at specified path
                    Supports both flat and nested directory structures:
                    - Direct: /import:C:\path\to\csvs
                    - Nested: /import:C:\path\Original\VBR\server\timestamp
  /hotfix           Run hotfix detection
  /path=<dir>       Specify path for hotfix detection (used with /hotfix)

UTILITY OPTIONS:
  /clearcreds       Clear stored credentials from Windows Credential Manager
  /debug            Enable debug logging for troubleshooting

EXAMPLES:
  VeeamHealthCheck.exe /run
  VeeamHealthCheck.exe /run /days:30 /lite /pdf
  VeeamHealthCheck.exe /run /outdir=D:\Reports
  VeeamHealthCheck.exe /security /remote /host=vbr-server.domain.local
  VeeamHealthCheck.exe /import /run
  VeeamHealthCheck.exe /import:D:\Exports\VBR-data
  VeeamHealthCheck.exe /import:""C:\My Path\CSV Files""
  VeeamHealthCheck.exe /hotfix /path=C:\VeeamUpdates
  VeeamHealthCheck.exe /run /show:report /scrub:true

NOTES:
  - Run with no arguments to launch the GUI
  - Credentials are managed via Windows Credential Manager
  - Remote execution requires appropriate permissions on target server
  - Default output directory: C:\temp\vHC (override with /outdir=)

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

            // "\r\n4.\tAllow the support representative to return an answer.\r\n"
            return output;
        }
    }
}
