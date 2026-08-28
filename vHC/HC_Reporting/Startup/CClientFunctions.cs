// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
{
    internal class CClientFunctions : IDisposable
    {
        private readonly CLogger LOG = CGlobals.Logger;
        private readonly string logStart = "[Functions]\t";

        public CClientFunctions()
        {
        }

        public void Dispose() { }

        public void KbLinkAction(System.Windows.Navigation.RequestNavigateEventArgs args)
        {
            CGlobals.Logger.Info("[GUI]\tOpening KB Link");
            Application.Current.Dispatcher.Invoke(delegate
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

        public void PreRunCheck()
        {
            CGlobals.Logger.Info("Starting Admin Check", false);
            CAdminCheck priv = new();
            if (!priv.IsAdmin())
            {
                // Admin check is only required for local VBR execution (not remote)
                // Remote execution does not require admin privileges
                if (CGlobals.IsVbr && !CGlobals.REMOTEEXEC)
                {
                    // Local VBR execution without admin - offer to continue with limitations
                    if (CGlobals.GUIEXEC)
                    {
                        // GUI execution - show dialog
                        string message = "Administrator privileges are recommended when running locally against Veeam Backup & Replication.\n\n" +
                                       "Running without administrator privileges will:\n" +
                                       "• Skip some registry checks\n" +
                                       "• Skip some security assessments\n" +
                                       "• May result in incomplete data collection\n\n" +
                                       "For best results, please:\n" +
                                       "1. Close this window\n" +
                                       "2. Right-click VeeamHealthCheck.exe\n" +
                                       "3. Select 'Run as Administrator'\n\n" +
                                       "Do you want to continue without administrator privileges?";
                        
                        var result = MessageBox.Show(message, "Administrator Privileges Recommended", 
                                                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        
                        if (result == MessageBoxResult.No)
                        {
                            CGlobals.Logger.Info("User declined to run without admin privileges", false);
                            Environment.Exit(0);
                        }
                        
                        // User chose to continue without admin
                        CGlobals.RunningWithoutAdmin = true;
                        CGlobals.Logger.Warning("Running without administrator privileges - some features will be limited", false);
                    }
                    else
                    {
                        // CLI execution - just warn and continue
                        CGlobals.RunningWithoutAdmin = true;
                        CGlobals.Logger.Warning("Running without administrator privileges - some registry checks and security assessments will be skipped", false);
                    }
                }
                else if (CGlobals.IsVb365 && !CGlobals.REMOTEEXEC)
                {
                    // Local VB365 requires admin
                    string message = "Please run program as Administrator";
                    if (CGlobals.GUIEXEC)
                    {
                        MessageBox.Show(message);
                    }
                    CGlobals.Logger.Error(message, false);
                    Environment.Exit(0);
                }
                // else: Remote execution - no admin required, continue normally
            }

            CGlobals.Logger.Info("Starting Admin Check...done!");
        }

        private void VbrVersionSupportCheck()
        {
            // GetVbrVersion();

            // get the version of the current vhc software:
            if(CGlobals.VBRMAJORVERSION < 12)
            {
                string[] vhcVersionSections = CGlobals.VHCVERSION.Split('.'); 
                int.TryParse(vhcVersionSections[0], out int vhcMajorVersion);
                int.TryParse(vhcVersionSections[3], out int vhcBuildVersion);

                if(vhcMajorVersion >= 2 && vhcBuildVersion > 546)
                {
                    string msg = String.Format("Veeam Health Check version {0} does not support Veeam Backup & Replication Versions prior to v12. To check systems prior to v12, Please download 2.0.0.546: https://github.com/VeeamHub/veeam-healthcheck/releases/tag/2.0.0.546", CGlobals.VHCVERSION);

                    this.LOG.Error(msg, false);

                    if (CGlobals.GUIEXEC)
                    {
                        MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    Environment.Exit(0);
                }
            }
        }

        public string ModeCheck()
        {
            CGlobals.Logger.Info("Checking processes to determine execution mode..", false);
            string title = VbrLocalizationHelper.GuiTitle;
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                // LOG.Warning(logStart + "process name: " + process.ProcessName);
                if (process.ProcessName == "Veeam.Archiver.Service")
                {
                    CGlobals.IsVb365 = true;
                    this.LOG.Info("VB365 software detected", false);
                }

                if (process.ProcessName == "Veeam.Backup.Service")
                {
                    CGlobals.IsVbr = true;
                    CGlobals.IsVbrInstalled = true;
                    this.LOG.Info("VBR software detected", false);
                }
            }

            if (CGlobals.IsVbr)
            {
                // Detect VBR version to determine correct PowerShell version (PS7 for VBR 13+,
                // PS5 for VBR 12-). ModeCheck() runs from the GUI constructor before the window
                // is shown and doesn't itself lead into PowerShell-module-based collection, so it
                // must use the ungated DetectVbrVersion, not GetVbrVersion - the PS 7.6+ module
                // gate is enforced once, immediately before real collection, in StartCollections()
                // via RunVbrPreflightGateIfTargeted().
                try { this.DetectVbrVersion(); }
                catch (Exception ex)
                {
                    this.LOG.Debug(this.logStart + $"VBR version detection skipped: {ex.Message}");
                }
            }

            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
            {
                CGlobals.Logger.Error("No Veeam Software detected. Is this server the VBR or VB365 management server?", false);
                CGlobals.Logger.Warning("\tTry connecting to a remote server with /remote /host=hostname");
                return "fail";
            }

            if (CGlobals.IsVbr && CGlobals.IsVb365)
            {

                return title + " - " + VbrLocalizationHelper.GuiTitleBnR + " & " + VbrLocalizationHelper.GuiTitleVB365;
            }


            if (!CGlobals.IsVb365 && !CGlobals.IsVbr)
            {

                return title + " - " + VbrLocalizationHelper.GuiImportModeOnly;
            }

            if (CGlobals.IsVbr)
            {
                return title + " - " + VbrLocalizationHelper.GuiTitleBnR;
            }


            if (CGlobals.IsVb365)
            {

                return title + " - " + VbrLocalizationHelper.GuiTitleVB365;
            }
            else
            {

                return title;
            }
        }

        public bool AcceptTerms()
        {
            string message = VbrLocalizationHelper.GuiAcceptText;

            var res = MessageBox.Show(message, "Terms", MessageBoxButton.YesNo,MessageBoxImage.Question);
            if (res.ToString() == "Yes")
            {

                return true;
            }
            else
            {
                return false;
            }
        }

        public int StartPrimaryFunctions()
        {
            // Single server execution - CGlobals.VBRServerName should already be set from the selected server in the UI
            this.LogUserSettings();
            this.StartCollections();
            return this.StartAnalysis();
        }

        public void RunHotfixDetector(string path, string remoteServer)
        {
            this.LOG.Info(this.logStart + "Starting Hotfix Detector", false);
            // Hotfix detection only needs the version number, not the PS 7.6+ module gate - it
            // never touches Veeam.Backup.PowerShell. DetectVbrVersion throws by design on
            // failure (e.g. no local VBR console) - don't let that crash /hotfix uncaught.
            // Skipped only for an explicit /vb365-only target - EffectiveIsVbr/IsVbr can't be
            // used here either: ModeCheck() never runs at all on the /hotfix dispatch branch,
            // so the auto-detected IsVbr/IsVb365 flags are never populated for this call path.
            if (CGlobals.TargetProductType != TargetProduct.Vb365)
            {
                try
                {
                    this.DetectVbrVersion();
                }
                catch (Exception ex)
                {
                    this.LOG.Debug(this.logStart + $"VBR version detection skipped: {ex.Message}");
                }
            }
            if (!String.IsNullOrEmpty(path))
            {
                if (!this.VerifyPath(path))
                {
                    string error = String.Format("Entered path \"{0}\" is invalid or doesn't exist. Try a different path", path);
                    this.LOG.Error(this.logStart + error, false);
                    return;

                    // LOG.Warning(logStart + "This option will collect support logs to some local directory and then check for hotfixes", false);
                    // LOG.Warning(logStart + "Please enter local path with adequate space for log files:", false);
                    // path = Console.ReadLine();
                }
            }
            else
            {
                this.LOG.Warning(this.logStart + "/path= variable is empty or missing.");

                // "\nPlease retry with syntax:" +
                //    "\nVeeamHealthCheck.exe /hotfix /path:C:\\examplepath", false);
                this.LOG.Warning(this.logStart + "This option will collect support logs to some local directory and then check for hotfixes", false);
                this.LOG.Warning(this.logStart + "Please enter local path with adequate space for log files:", false);
                path = Console.ReadLine();
            }

            CHotfixDetector hfd = new(path);
            hfd.Run();
        }

        public bool VerifyPath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }


            if (path.StartsWith("\\\\"))
            {
                return false;
            }


            if (Directory.Exists(path))
            {
                return true;
            }

            if (this.TryCreateDir(path))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool TryCreateDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch {
                this.LOG.Error("Failed to create directory.", false);
                return false; }
        }

        private void LogUserSettings()
        {
            this.LOG.Info(this.ClientSettingsString(), false);
        }

        private void StartCollections()
        {
            if (!CGlobals.IMPORT)
            {
                // Single authoritative PS 7.6+ module preflight gate - see
                // RunVbrPreflightGateIfTargeted()'s doc comment for the full rationale.
                this.RunVbrPreflightGateIfTargeted();

                this.LOG.Info(this.logStart + "Init Collections", false);

                if (CGlobals.REMOTEEXEC && CGlobals.RunSecReport)
                {
                    CImpersonation cImpersonation = new CImpersonation();
                    cImpersonation.RunCollection();
                }

                else
                {
                    CCollections collect = new();
                    collect.Run();
                }

                this.LOG.Info(this.logStart + "Init Collections...done!", false);
            }
        }

        private int StartAnalysis()
        {
            this.LOG.Info(this.logStart + "Init Data analysis & report creations", false);

            // Resolve import path if IMPORT mode is enabled
            if (CGlobals.IMPORT)
            {
                if (!this.ResolveImportPath())
                {
                    this.LOG.Error(this.logStart + "Failed to resolve import path. Exiting.", false);
                    return 1;
                }
            }

            // Validate CSV files before report generation (VBR only - VB365-only machines have no VBR CSVs)
            if (CGlobals.IsVbr)
            {
                try
                {
                    this.LOG.Info(this.logStart + "Validating collected CSV files...", false);
                    var validator = new CCsvValidator(CVariables.vbrDir);
                    CGlobals.CsvValidationResults = validator.ValidateVbrCsvFiles();
                    this.LOG.Info(this.logStart + "CSV validation complete.", false);
                }
                catch (Exception ex)
                {
                    this.LOG.Warning(this.logStart + $"CSV validation encountered an error: {ex.Message}. Continuing with report generation.", false);
                }
            }

            int res = this.Import();

            this.LOG.Info(this.logStart + "Init Data analysis & report creations...done!", false);
            return res;
        }

        /// <summary>
        /// Resolves and validates the import path when IMPORT mode is enabled.
        /// </summary>
        /// <returns>True if import path was successfully resolved, false otherwise.</returns>
        private bool ResolveImportPath()
        {
            this.LOG.Info(this.logStart + "Resolving import path...", false);

            // Determine base path: use IMPORT_PATH if set, otherwise use default
            string basePath = !string.IsNullOrEmpty(CGlobals.IMPORT_PATH)
                ? CGlobals.IMPORT_PATH
                : CGlobals.desiredPath ?? CVariables.outDir;

            this.LOG.Info(this.logStart + $"Import base path: {basePath}", false);

            // Find the actual CSV directory within the base path
            string csvDirectory = CImportPathResolver.FindCsvDirectory(basePath);

            if (string.IsNullOrEmpty(csvDirectory))
            {
                this.LOG.Error(this.logStart + $"No valid CSV directory found in: {basePath}", false);
                this.LOG.Info(this.logStart + "Expected structure: path/VBR/servername/timestamp/ or path containing CSV files directly", false);

                if (CGlobals.GUIEXEC)
                {
                    System.Windows.MessageBox.Show(
                        $"No valid CSV files found in:\n{basePath}\n\nPlease verify the import path contains VBR or VB365 CSV export files.",
                        "Import Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }

                return false;
            }

            // Validate the CSV files in the discovered directory
            var validationResult = CImportPathResolver.ValidateCsvFiles(csvDirectory);

            if (!validationResult.IsValid && validationResult.MissingCriticalFiles.Count > 3)
            {
                this.LOG.Error(this.logStart + $"Import validation failed: {validationResult.ErrorMessage}", false);

                if (CGlobals.GUIEXEC)
                {
                    System.Windows.MessageBox.Show(
                        $"Import validation failed:\n{validationResult.ErrorMessage}\n\nMissing files: {string.Join(", ", validationResult.MissingCriticalFiles)}",
                        "Import Validation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }

                // Allow import to continue with partial data
                this.LOG.Warning(this.logStart + "Continuing with partial data - some report sections may be incomplete.", false);
            }

            // Store the resolved path for use by CVariables
            CVariables.ResolvedImportPath = csvDirectory;
            this.LOG.Info(this.logStart + $"Resolved import CSV directory: {csvDirectory}", false);

            // Set product type flags based on discovered files
            if (validationResult.ProductType == "VBR")
            {
                CGlobals.IsVbr = true;
            }
            else if (validationResult.ProductType == "VB365")
            {
                CGlobals.IsVb365 = true;
            }

            // Extract timestamps for report interval
            var (earliest, latest) = CImportPathResolver.ExtractTimestamps(csvDirectory);
            this.LOG.Info(this.logStart + $"Data collection period: {earliest:yyyy-MM-dd} to {latest:yyyy-MM-dd}", false);

            return true;
        }

        public int CliRun(string targetForOutput)
        {
            CGlobals.Logger.Info("Setting openexplorer & openhtml to false for CLI execution", false);
            CGlobals.OpenExplorer = false;

            // CGlobals.OpenHtml = false;
            CGlobals.desiredPath = targetForOutput;
            if(!CGlobals.IMPORT)
            {
                this.PreRunCheck();
            }

            try // REST TEST AREA
            {
                // RestInvoker restInvoker = new RestInvoker();
                // restInvoker.Run();
            }
            catch (Exception)
            {
            }
            
            return this.StartPrimaryFunctions();
        }

        /// <summary>
        /// Single authoritative PS 7.6+ module preflight gate, run once from StartCollections()
        /// right before the two branches that both lead into real PowerShell-module-based
        /// collection. Gated on EffectiveIsVbr - not IMPORT, not REMOTEEXEC - because this
        /// preflight (and the PowerShell-version check it wraps) is meaningless for a run that
        /// doesn't target VBR: DetectVbrVersion() reads the LOCAL machine's VBR registry keys,
        /// which are legitimately absent on a VB365-only server, and previously spammed 5
        /// ERROR-level log lines for a totally expected condition on every single such run.
        /// Still runs whenever the target includes VBR (TargetProductType Vbr/Both, or Auto with
        /// local VBR actually detected) regardless of REMOTEEXEC: per repo convention, a preflight
        /// on the local PowerShell/module install must run regardless of REMOTEEXEC, since it is
        /// never the remote machine's problem. GetVbrVersion -> DetectVbrVersion throws by design
        /// when local VBR detection fails - not fatal, the preflight just can't run; GetVbrVersion
        /// catches only that expected failure, scoped to the DetectVbrVersion call, so an exception
        /// out of the hard-fail path (ValidatePowerShellVersionMeetsVbrRequirement) is never
        /// mistaken for it and swallowed here too.
        /// </summary>
        internal void RunVbrPreflightGateIfTargeted()
        {
            if (!CGlobals.EffectiveIsVbr)
            {
                return;
            }

            this.GetVbrVersion();
        }

        /// <summary>
        /// Detects the VBR version and required PowerShell version and gates on the PS 7.6+
        /// module requirement. Private: RunVbrPreflightGateIfTargeted() is the only caller,
        /// since it's the single choke point (reached from StartCollections(), itself reached
        /// from both the GUI Run button and every CLI run path) immediately before real
        /// PowerShell-module-based collection begins, and only when EffectiveIsVbr is true.
        /// Every other caller (ModeCheck, RunHotfixDetector, early CLI arg-parsing detection)
        /// must call the ungated DetectVbrVersion instead, so a too-old-PowerShell machine
        /// doesn't hard-exit a feature that never touches the Veeam.Backup.PowerShell module.
        /// Known limitation, not fixed here: when DetectVbrVersion fails (e.g. non-admin
        /// execution, where CRegReader.GetVbrVersionFilePath() returns null), we can't know
        /// whether the local VBR is 13+ at all, so ValidatePowerShellVersionMeetsVbrRequirement
        /// is skipped rather than called - it would no-op anyway, since it gates on
        /// CGlobals.PowerShellVersion, which DetectVbrVersion only ever sets on success. Making
        /// this reachable needs VBR-version detection to work without admin rights first; that's
        /// separate, larger work in CRegReader, not a fix to this gate's catch scope.
        /// </summary>
        private void GetVbrVersion()
        {
            try
            {
                this.DetectVbrVersion();
            }
            catch (Exception ex)
            {
                this.LOG.Debug(this.logStart + $"PowerShell version gate skipped: {ex.Message}");
                return;
            }

            this.ValidatePowerShellVersionMeetsVbrRequirement();
        }

        internal void DetectVbrVersion()
        {
            try
            {
                CRegReader reg = new();
                string version = reg.GetVbrVersionFilePath();

                // Validate that version detection succeeded
                if (string.IsNullOrEmpty(version) || CGlobals.VBRMAJORVERSION == 0)
                {
                    this.LOG.Error(this.logStart + "Failed to detect VBR version. Cannot determine required PowerShell version.", false);
                    this.LOG.Error(this.logStart + "Please verify VBR is installed and accessible.", false);
                    this.LOG.Error(this.logStart + "If VBR is installed on a non-standard drive, ensure registry keys are readable.", false);
                    throw new InvalidOperationException("VBR version detection failed");
                }

                this.LOG.Info(this.logStart + "VBR Version: " + version, false);
                CGlobals.PowerShellVersion = CGlobals.VBRMAJORVERSION >= 13 ? 7 : 5;
                this.LOG.Info(this.logStart + "Using PowerShell version: " + CGlobals.PowerShellVersion.ToString(), false);

                // If PowerShell 7 is required AND we're doing remote execution, ensure we have credentials available
                // For local VBR (IsVbr=true, REMOTEEXEC=false), credentials are NOT required - Windows auth is used
                if (CGlobals.PowerShellVersion == 7 && CGlobals.REMOTEEXEC)
                {
                    this.LOG.Info(this.logStart + "PowerShell 7 with remote execution requires credentials for VBR connection", false);
                    this.EnsureCredentialsAvailable();
                }
                else if (CGlobals.PowerShellVersion == 7)
                {
                    this.LOG.Info(this.logStart + "PowerShell 7 detected, but local VBR will use Windows authentication (no credentials required)", false);
                }
            }
            catch(Exception ex)
            {
                // Log the actual error instead of swallowing it
                this.LOG.Error(this.logStart + "Exception during VBR version detection: " + ex.Message, false);
                this.LOG.Error(this.logStart + "Stack trace: " + ex.StackTrace, false);
                throw; // Re-throw to fail fast rather than continue with wrong PowerShell version
            }
        }

        /// <summary>
        /// Preflight check for VBR 13+: verifies PowerShell 7 is actually installed, and if so,
        /// compares its version against the minimum required by the local Veeam.Backup.PowerShell
        /// module manifest. Exits with an actionable message if PS7 is missing entirely, or if it's
        /// installed but too old. Without this, a missing or under-versioned PowerShell only fails
        /// later, deep inside a collection script's Import-Module call, with a confusing error trail
        /// (see issue: VBR 13.1 requires PowerShell 7.6, but a 7.4.x install - or no PS7 install at
        /// all - produces cascading Get-Package / Import-Module / Connect-VBRServer errors instead of
        /// a clear message).
        /// Reads the requirement from the manifest rather than hardcoding it, since Veeam can raise
        /// the minimum again in a future VBR release. A missing PS7 install is always a hard failure,
        /// even if the manifest itself couldn't be read (see EvaluatePwshVersionStatus - NotInstalled
        /// is checked first and unconditionally). Only skips (does not block the run) when PS7 is
        /// present but its version - or the manifest's required version - couldn't be conclusively
        /// determined; that remains a best-effort UX improvement, not a hard gate, to avoid a
        /// false-positive block on a transient detection hiccup.
        /// </summary>
        private void ValidatePowerShellVersionMeetsVbrRequirement()
        {
            if (CGlobals.PowerShellVersion != 7)
            {
                return;
            }

            string pwshPath = CPowerShellVersionChecker.FindPwshExecutable();

            Version requiredVersion = null;
            if (string.IsNullOrEmpty(CGlobals.VbrConsoleInstallDir))
            {
                this.LOG.Debug(this.logStart + "VBR console install directory unknown. Cannot read required PowerShell version from the module manifest.");
            }
            else
            {
                string manifestPath = Path.Combine(CGlobals.VbrConsoleInstallDir, "Veeam.Backup.PowerShell", "Veeam.Backup.PowerShell.psd1");
                if (!CPowerShellVersionChecker.TryGetManifestRequiredVersion(manifestPath, out requiredVersion))
                {
                    this.LOG.Debug(this.logStart + $"Could not read required PowerShell version from '{manifestPath}'.");
                }
            }

            // Only worth spawning pwsh.exe to read the installed version when a required version
            // is actually known - EvaluatePwshVersionStatus treats a null requiredVersion as
            // VersionInconclusive regardless of installedVersion, so the spawn would be wasted.
            Version installedVersion = null;
            string rawInstalledVersion = null;
            if (!string.IsNullOrEmpty(pwshPath) && requiredVersion != null &&
                !CPowerShellVersionChecker.TryGetInstalledPwshVersion(pwshPath, out installedVersion, out rawInstalledVersion))
            {
                this.LOG.Debug(this.logStart + "Could not determine installed PowerShell 7 version.");
            }

            PwshVersionStatus status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(pwshPath, installedVersion, requiredVersion);

            switch (status)
            {
                case PwshVersionStatus.MeetsRequirement:
                    return;

                case PwshVersionStatus.VersionInconclusive:
                    this.LOG.Debug(this.logStart + "Skipping PowerShell module version preflight check: could not conclusively determine the installed vs. required version.");
                    return;

                case PwshVersionStatus.NotInstalled:
                case PwshVersionStatus.BelowRequirement:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unhandled PwshVersionStatus in ValidatePowerShellVersionMeetsVbrRequirement.");
            }

            string msg = CPowerShellVersionChecker.BuildPwshVersionFailureMessage(status, CGlobals.VBRFULLVERSION, requiredVersion, rawInstalledVersion);

            this.LOG.Error(this.logStart + msg, false);

            if (CGlobals.Silent)
            {
                // Guarded like the GUIEXEC branch below: ExitSilent's Console.Error.WriteLine
                // could throw (e.g. a broken/redirected stderr pipe), and this hard-fail must
                // still reach Environment.Exit either way.
                try
                {
                    SilentExit.ExitSilent(SilentExit.PowerShellVersionUnsupported, msg);
                }
                catch (Exception ex)
                {
                    this.LOG.Debug(this.logStart + $"ExitSilent failed: {ex.Message}");
                }
            }

            if (CGlobals.GUIEXEC)
            {
                // Dispatch to the UI thread so the box is owned by the main window instead of
                // desktop-parented - this can run from a background Task (StartCollections is
                // invoked from VhcGui.Run()'s Task.Factory.StartNew), matching the existing
                // convention in VhcGui.xaml.cs's ShowCollectionWarningsIfAny. Fall back to an
                // undocked MessageBox.Show if there's no Dispatcher to marshal to - the user must
                // still see this message before the Environment.Exit below, never silently.
                // Guarded: dispatcher.Invoke/MessageBox.Show can itself throw (e.g. Dispatcher
                // shutting down), and this hard-fail must reach Environment.Exit either way.
                try
                {
                    System.Windows.Threading.Dispatcher dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher != null)
                    {
                        dispatcher.Invoke(() =>
                            MessageBox.Show(msg, "Unsupported PowerShell Version", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                    else
                    {
                        MessageBox.Show(msg, "Unsupported PowerShell Version", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    this.LOG.Debug(this.logStart + $"Failed to show PowerShell version MessageBox: {ex.Message}");
                }
            }

            Environment.Exit(SilentExit.PowerShellVersionUnsupported);
        }

        private void EnsureCredentialsAvailable()
        {
            string host = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;

            // Check if credentials are already stored for this host
            var storedCreds = CredentialStore.Get(host);
            if (storedCreds != null)
            {
                this.LOG.Info(this.logStart + $"Stored credentials found for host: {host}", false);
                return;
            }

            // No stored credentials found - log appropriate message based on mode
            // Note: Don't try to prompt here during early initialization.
            // The credential prompt will happen later when GetCreds() is called during collection,
            // at which point the GUI will be fully initialized (if in GUI mode).
            if (!CGlobals.GUIEXEC)
            {
                this.LOG.Warning(this.logStart + "No stored credentials found for PowerShell 7 connection.", false);
                this.LOG.Warning(this.logStart + "Add the /run parameter to execute and be prompted for credentials.", false);
                this.LOG.Warning(this.logStart + "Example: VeeamHealthCheck.exe /run /remote /host=" + host, false);
            }
            else
            {
                this.LOG.Info(this.logStart + "No stored credentials found. Credentials will be prompted when collection starts.", false);
            }
        }

        public bool VerifyPath()
        {
            try
            {
                if (!Directory.Exists(CGlobals.desiredPath))
                {
                    Directory.CreateDirectory(CGlobals.desiredPath);
                }


                return true;
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("[UI] Desired dir does not exist and cannot be created. Error: ");
                CGlobals.Logger.Error("\t" + e.Message);
                return false;
            }
        }

        public int Import()
        {
            CReportModeSelector cMode = new();
            int res = cMode.Run();
            cMode.Dispose();
            return res;
        }

        private string ClientSettingsString()
        {
            return string.Format(
                "User Settings:\n" +
                "\t\t\t\t\tScrub = {0}\n" +
                "\t\t\t\t\tOpen HTML = {1}\n" +
                "\t\t\t\t\tOpen Explorer = {2}\n" +
                "\t\t\t\t\tPath = {3}\n" +
                "\t\t\t\t\tInterval = {4}\n" +
                "\t\t\t\t\tTargetProductType = {5}",
                CGlobals.Scrub, CGlobals.OpenHtml, CGlobals.OpenExplorer, CGlobals.desiredPath, CGlobals.ReportDays.ToString(), CGlobals.TargetProductType
                );
        }

        public void LogUIAction(string message)
        {
            string s = string.Format("[Veeam.HC.UI]\tSelected:" + message);
            CGlobals.Logger.Info(s);
        }

        private void WriteVhcVersion()
        {
            CGlobals.Logger.Info("vHC Version: " + CVersionSetter.GetFileVersion(), false);
        }

        private void WriteCliArgs(string[] args)
        {
            CGlobals.Logger.Info("Args count = " + args.Count().ToString());
            foreach (var arg in args)
            {
                CGlobals.Logger.Info("\tInput: " + arg);
            }
        }

        public void LogVersionAndArgs(string[] args)
        {
            this.WriteVhcVersion();
            this.WriteCliArgs(args);
        }
    }
}
