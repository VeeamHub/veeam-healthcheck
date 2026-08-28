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

PRODUCT TYPE FLAGS:
  /vbr              Target a VBR (Backup & Replication) server
  /vb365            Target a VB365 (Backup for Microsoft 365) server
  /vbr /vb365       Target a server running both products

UTILITY OPTIONS:
  /clearcreds       Clear stored credentials from Windows Credential Manager
  /debug            Enable debug logging for troubleshooting

CONTINUOUS MONITORING:
  /monitor:setup    Install vhc-monitor and register a 5-minute scheduled task
  /monitor:run      Trigger an immediate monitor check
  /monitor:status   Show current monitor installation and last-run status
  /monitor:disable  Remove the scheduled task (keeps config and files)

UNATTENDED / SILENT MODE:
  /silent           Master ""never prompt, fail fast"" flag. Suppresses GUI
                    dialogs, console password prompts, and PowerShell
                    Get-Credential calls. Required for unattended (Task
                    Scheduler / fleet) execution. Pairs with /savecreds-seeded
                    DPAPI credentials or a /credfile= entry for the host.
  /savecreds        One-shot interactive seed. Prompts for user/password and
                    stores them via DPAPI (CurrentUser scope) for the host
                    given by /host= (defaults to localhost). Exits 0 when
                    done. Mutually exclusive with /silent.
  /credfile=<path>  Load a JSON credfile of the form
                    {""<host>"": {""username"": ""..."", ""passwordBase64"": ""...""}}
                    into the in-memory transient cache. Composes with /silent.
                    Does NOT persist to DPAPI.

  Conflict rules:
    /silent + /savecreds            -> exit 2 (mutually exclusive)
    /silent alone, no creds source  -> exit 2 (creds missing)
    /credfile= without /silent      -> valid; takes precedence over DPAPI store

  Exit codes (silent mode):
    0  Success
    1  Generic failure (existing behavior)
    2  Creds missing or flag conflict
    3  Authentication failed
    4  Account is MFA-enabled (unsupported for unattended VBR)
    5  Host unreachable
    6  /credfile= invalid (malformed JSON, missing fields, bad Base64)
    7  No Veeam product detected and no /host= provided
    8  PowerShell 7 missing, or its installed version is below what the VBR PowerShell module requires

  Worked example (Task Scheduler / 20-server fleet):
    Seed once on each server as the service account:
      VeeamHealthCheck.exe /savecreds /host=localhost
    Then schedule:
      VeeamHealthCheck.exe /run /vbr /silent /scrub:true /outdir=D:\vHC-Reports

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
