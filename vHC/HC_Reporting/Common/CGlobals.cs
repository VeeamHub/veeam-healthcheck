// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Shared
{
    public class CGlobals
    {
        // static globals:
        public static CLogger mainlog = new("HealthCheck");

        /// <summary>
        /// UI-framework-specific dialog seam. Null until GUI mode wires it up
        /// (CArgsParser.LaunchUi). Most call sites (PreRunCheck's admin-check box,
        /// ResolveImportPath, ValidatePowerShellVersionMeetsVbrRequirement,
        /// WeighSuccessContinuation) guard on CGlobals.GUIEXEC first, which is false
        /// for every CLI code path. AcceptTerms is the one exception - it has no
        /// such guard, but its only caller (VhcGui's AcceptButton_click) is GUI-only,
        /// so this is still never dereferenced while null in practice.
        /// </summary>
        public static IUiNotifier Notifier { get; set; }

        /// <summary>
        /// UI-framework-specific credential prompt seam. Null until GUI mode wires it up.
        /// Used by CredsHandler.PromptForCredentials when GUIEXEC is true.
        /// </summary>
        public static ICredentialPrompter CredentialPrompter { get; set; }

        /// <summary>
        /// Stores validation results for CSV files collected during the data gathering phase.
        /// Used to track which files are present/missing and generate data collection summaries.
        /// </summary>
        public static List<CsvValidationResult> CsvValidationResults { get; set; } = new();

        /// <summary>
        /// Stores per-collector success/failure entries loaded from _CollectionManifest.csv.
        /// Populated by CCsvValidator.LoadManifest() after PowerShell collection completes.
        /// </summary>
        public static List<CCollectionManifestEntry> CollectionManifest { get; set; } = new();
        private static bool scrub;
        private static readonly CScrubHandler scrubberMain = new();
        public static readonly string backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8";
        public static bool IMPORT = false;

        /// <summary>
        /// When IMPORT is true, this specifies the path to import CSV files from.
        /// If null or empty, the default path (C:\temp\vHC) is used.
        /// Supports both flat structure and nested servername/timestamp structure.
        /// </summary>
        public static string IMPORT_PATH = null;
        public static int VBRMAJORVERSION;
        public static string VBRFULLVERSION;

        /// <summary>
        /// Directory containing the VBR Console installation (e.g. ".../Backup and Replication/Console"),
        /// resolved by <see cref="Functions.Collection.DB.CRegReader.GetVbrVersionFilePath"/>. Used to locate
        /// the Veeam.Backup.PowerShell module manifest for the PowerShell version preflight check.
        /// </summary>
        public static string VbrConsoleInstallDir;
        public static int PowerShellVersion;
        public static DateTime TOOLSTART;

        /// <summary>
        /// True when targeting a VBR/VB365 server other than the local machine (set via /remote
        /// /host=&lt;name&gt;). Despite the name, this does NOT mean PowerShell scripts execute on
        /// that remote machine - vHC always launches pwsh.exe and the Veeam.Backup.PowerShell
        /// module locally. REMOTEEXEC only changes which server the module connects to (via
        /// -Server / -VBOServerFqdnOrIp) and whether explicit credentials are required instead
        /// of Windows auth. See <see cref="REMOTEHOST"/>.
        /// </summary>
        public static bool REMOTEEXEC = false;

        /// <summary>The server to connect to when <see cref="REMOTEEXEC"/> is true.</summary>
        public static string REMOTEHOST = string.Empty;
        public static bool GUIEXEC = false;
        private static string _runTimestamp = null;
        public static string VHCVERSION = string.Empty;
        public static bool DEBUG = false;

        /// <summary>
        /// Delimiter multi-value producers (e.g. CDataFormer.RegOptions, SetGateHosts,
        /// SummarizeRoleTypes) join their values with. HTML rendering converts this to a
        /// line break at the call site; JSON export keeps it as-is (see ADR 0029).
        /// </summary>
        public const string MultiValueDelimiter = "|";

        // Remote Exec variables
        public static string VBRServerName = "localhost";

        // vhc-monitor integration
        public static string VhcMonitorInstallDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VeeamHealthCheck", "monitor");
        public static string VhcMonitorConfigPath =>
            Path.Combine(VhcMonitorInstallDir, "vhc-monitor.yaml");
        public static string VhcMonitorExeBundlePath =>
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty, "vhc-monitor.exe");
        public static readonly string VhcMonitorTaskName = "VHC Monitor";
        
        // Multi-server execution support
        public static List<string> SelectedServers = new List<string> { "localhost" };
        public static int MaxParallelServers = 3;
        public static bool IsVbrInstalled = false;

        public static string RawReport = string.Empty;
        public static string ScrubbedReport = string.Empty;

        // GUI & CLI Options:
        private static int reportDays = 7;
        private static string _desiredPath = CVariables.unsafeDir;
        private static bool openHtml;
        private static bool openExplorer;
        private static bool isVbr;
        private static bool isVb365;
        private static bool runFullReport;
        private static bool runSecReport;
        public static bool EXPORTINDIVIDUALJOBHTMLS = true;
        public static bool CHECKFIXES = false;
        public static bool EXPORTPDF = false;
        public static bool EXPORTPPTX = false;
        public static bool ClearStoredCreds = false;
        public static bool RunningWithoutAdmin = false;
        public static bool RescanHosts = false;

        // Unattended / silent execution flags. See plan: VHC Silent / Unattended Execution.
        // Silent = true means "never prompt, fail fast"; suppresses GUI dialogs, console
        // password prompts, and any PowerShell Get-Credential. Required for unattended runs.
        public static bool Silent = false;
        // Path to a JSON credfile loaded by /credfile=<path>. When set, the file's
        // contents are loaded into CredentialStore via SetTransient (in-memory only).
        public static string CredFilePath = null;
        // True when /savecreds was supplied; the parser runs the seed flow and exits.
        public static bool SaveCredsOnly = false;

        // Security Values
        public static bool IsMfaEnabled = false;

        // Last user-visible error collected during execution (shown in GUI if set)
        public static string UserFacingError = null;

        // B&R Server global values
        // public static string isConsoleLocal = "Undetermined";
        // public static string _isRdpEnabled = "Undetermined";
        // public static string _isDomainJoined = "";

        // config db values
        public static BackupServer BACKUPSERVER;
        public static string ISDBLOCAL;
        public static string DBTYPE;
        public static readonly string SqlTypeName = "MS SQL";
        public static readonly string PgTypeName = "PG SQL";
        public static string DBNAME;
        public static string DBVERSION;
        public static string DBHOSTNAME;
        public static string DBEdition;
        public static string DBINSTANCE;
        public static int DBCORES;
        public static int DBRAM;

        // collections
        public static Dictionary<string, Object> DEFAULTREGISTRYKEYS = new Dictionary<string, Object>();
        public static List<CServerTypeInfos> ServerInfo = new();
        public static CDataTypesParser DtParser;

        public static bool IsReconDetected = false;
        public static DateTime LastReconRun = DateTime.MinValue;

        // JSON aggregation object for full report output
        internal static CFullReportJson FullReportJson = new();

        public CGlobals()
        {
        }

        public static int ReportDays
        {
            get { return reportDays; }
            set { reportDays = value; }
        }

        public static bool Scrub
        {
            get { return scrub; }
            set { scrub = value; }
        }

        public static DateTime GetToolStart
        {
            get { if(TOOLSTART == DateTime.MinValue)
                {
                    TOOLSTART = DateTime.Now;
                }


                return TOOLSTART;
            }

            set { TOOLSTART = value; }
        }

        public static CLogger Logger { get { return mainlog; } }

        public static CScrubHandler Scrubber { get { return scrubberMain; } }

        public static bool OpenHtml { get { return openHtml; } set { openHtml = value; } }

        public static bool OpenExplorer { get { return openExplorer; } set { openExplorer = value; } }

        // public bool Import { get { return _import; } set { _import = value; } }
        public static bool IsVbr { get { return isVbr; } set { isVbr = value; } }

        public static bool IsVb365 { get { return isVb365; } set { isVb365 = value; } }

        public static TargetProduct TargetProductType { get; set; } = TargetProduct.Auto;

        public static bool EffectiveIsVbr
        {
            get
            {
                if (TargetProductType == TargetProduct.Vbr || TargetProductType == TargetProduct.Both)
                    return true;
                if (TargetProductType == TargetProduct.Auto)
                    return IsVbr;
                return false;
            }
        }

        public static bool EffectiveIsVb365
        {
            get
            {
                if (TargetProductType == TargetProduct.Vb365 || TargetProductType == TargetProduct.Both)
                    return true;
                if (TargetProductType == TargetProduct.Auto)
                    return IsVb365;
                return false;
            }
        }

        public static bool RunFullReport { get { return runFullReport; } set { runFullReport = value; } }

        public static bool RunSecReport { get { return runSecReport; } set { runSecReport = value; } }

        // Add a public property to match callers using 'desiredPath'
        public static string desiredPath
        {
            get => _desiredPath;
            set => _desiredPath = value;
        }

        /// <summary>
        /// Gets the timestamp for the current tool run in yyyyMMdd_HHmmss format.
        /// Timestamp is set once per run and reused throughout.
        /// </summary>
        public static string GetRunTimestamp()
        {
            if (_runTimestamp == null)
            {
                _runTimestamp = GetToolStart.ToString("yyyyMMdd_HHmmss");
            }
            return _runTimestamp;
        }
    }
}
