// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Functions.Monitor;
using VeeamHealthCheck.Functions.UserInteraction;

// using VeeamHealthCheck.Reporting.vsac;
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

        private readonly string[] args;
        private readonly CClientFunctions functions = new();

        public CArgsParser(string[] args)
        {
            this.args = args;
            CGlobals.TOOLSTART = DateTime.Now;
        }

        public int InitializeProgram()
        {
            // CGlobals.RunFullReport = true;
            this.LogInitialInfo();

            PSInvoker p = new();
            p.TryUnblockFiles();

            if (this.args.Length == 0){
                CGlobals.Logger.Debug("No arguments provided. Launching GUI", false);
                return this.LaunchUi(this.Handle(), true);
            }
            else if (this.args != null && this.args.Length > 0)
                return this.ParseAllArgs(this.args);
            else
                return this.LaunchUi(this.Handle(), false);
        }

        private void LogInitialInfo()
        {
            CClientFunctions f = new CClientFunctions();
            f.LogVersionAndArgs(this.args);
            // Note: VBR version detection is deferred until after argument parsing
            // so that REMOTEEXEC flag can be properly set first
            f.Dispose();
        }

        private int LaunchUi(IntPtr handle, bool hide)
        {
            CGlobals.Logger.Info("Executing GUI", false);
            CGlobals.RunFullReport = true;
            CGlobals.GUIEXEC = true;
            CGlobals.Notifier = new AvaloniaUiNotifier();
            CGlobals.CredentialPrompter = new AvaloniaCredentialPrompter();

            // if (hide)
            //     ShowWindow(handle, SW_HIDE);
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(Array.Empty<string>());
        }

        private IntPtr Handle()
        {
            // GetConsoleWindow is a kernel32.dll P/Invoke — no console-window
            // concept exists off Windows, so calling it there would throw
            // DllNotFoundException.
            return OperatingSystem.IsWindows() ? GetConsoleWindow() : IntPtr.Zero;
        }

        // private int ParseZeroArgs()
        // {
        //    var pos = Console.GetCursorPosition();
        //    CGlobals.Logger.Debug("pos = " + pos.ToString(), false);
        //    if (pos == (0, 1) || pos == (0, 2))
        //    {
        //        CGlobals.Logger.Info("0s");
        //         return LaunchUi(Handle(), true);
        //    }
        //    else
        //    {
        //        CGlobals.Logger.Info("not 0");
        //        Console.WriteLine(CMessages.helpMenu);
        //        return 0;
        //    }
        // }
        private int ParseAllArgs(string[] args)
        {
            bool run = false;
            bool ui = false;
            bool runHfd = false;
            string _hfdPath = string.Empty;

            // Same intentional Windows-only default as CVariables.outDir - see that
            // comment for why this isn't cross-platform-guarded.
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
                    case var importMatch when new Regex("^/import[:=](.+)$", RegexOptions.IgnoreCase).IsMatch(a):
                        run = true;
                        CGlobals.IMPORT = true;
                        CGlobals.RunFullReport = true;
                        CGlobals.IMPORT_PATH = this.ParseImportPath(a);
                        if (!string.IsNullOrEmpty(CGlobals.IMPORT_PATH))
                        {
                            CGlobals.Logger.Info("Import path set to: " + CGlobals.IMPORT_PATH);
                        }
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
                        // functions.RunHotfixDetector();
                        runHfd = true;

                        // Environment.Exit(0);
                        break;
                    case "/clearcreds":
                        CGlobals.ClearStoredCreds = true;
                        CGlobals.Logger.Info("Clear stored credentials flag set", false);
                        break;
                    case "/silent":
                        CGlobals.Silent = true;
                        CGlobals.Logger.Info("Silent (unattended) mode enabled", false);
                        break;
                    case "/savecreds":
                        CGlobals.SaveCredsOnly = true;
                        CGlobals.Logger.Info("Save-credentials flag set (one-shot interactive seed)", false);
                        break;
                    case var _ when a.StartsWith("/credfile=", StringComparison.OrdinalIgnoreCase) && a.Length > "/credfile=".Length:
                        CGlobals.CredFilePath = this.ParsePath(a);
                        CGlobals.Logger.Info("Credfile path set: " + CGlobals.CredFilePath, false);
                        break;
                    case "/pdf":
                        CGlobals.EXPORTPDF = true;
                        break;
                    case "/pptx":
                        CGlobals.EXPORTPPTX = true;
                        break;
                    case "/debug":
                        CGlobals.DEBUG = true;
                        break;
                    case "/vbr":
                        if (CGlobals.TargetProductType == TargetProduct.Vb365)
                            CGlobals.TargetProductType = TargetProduct.Both;
                        else
                            CGlobals.TargetProductType = TargetProduct.Vbr;
                        CGlobals.Logger.Info("Target product: VBR", false);
                        break;
                    case "/vb365":
                        if (CGlobals.TargetProductType == TargetProduct.Vbr)
                            CGlobals.TargetProductType = TargetProduct.Both;
                        else
                            CGlobals.TargetProductType = TargetProduct.Vb365;
                        CGlobals.Logger.Info("Target product: VB365", false);
                        break;
                    case var match when new Regex("/path=.*").IsMatch(a):
                        _hfdPath = this.ParsePath(a);
                        CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/PATH=.*").IsMatch(a):
                        _hfdPath = this.ParsePath(a);
                        CGlobals.Logger.Info("HFD path: " + targetDir);
                        break;
                    case var match when new Regex("/outdir=.*", RegexOptions.IgnoreCase).IsMatch(a):
                        string parsedOutDir = this.ParsePath(a);
                        if (!string.IsNullOrEmpty(parsedOutDir))
                        {
                            targetDir = parsedOutDir;
                            this.ApplyOutDir(parsedOutDir); // apply immediately so CVariables.unsafeDir resolves correctly
                            CGlobals.Logger.Info("Output directory overridden: " + targetDir, false);
                        }
                        break;
                    case var match when new Regex("/host=.*", RegexOptions.IgnoreCase).IsMatch(a):
                        string providedHost = this.ParsePath(a);

                        // Check if the provided host is actually the local machine (Issue #82)
                        if (CHostNameHelper.IsLocalHost(providedHost))
                        {
                            CGlobals.Logger.Info($"Detected /host={providedHost} is local machine - using local execution mode", false);
                            CGlobals.REMOTEEXEC = false;
                            CGlobals.REMOTEHOST = "localhost";
                        }
                        else
                        {
                            CGlobals.REMOTEEXEC = true;
                            CGlobals.REMOTEHOST = providedHost;
                        }
                        break;
                    case "/monitor:setup":
                        this.RunMonitorSetup();
                        return 0;
                    case "/monitor:run":
                        return this.RunMonitorNow();
                    case "/monitor:status":
                        this.PrintMonitorStatus();
                        return 0;
                    case "/monitor:disable":
                        CVhcMonitorIntegration.Uninstall();
                        CGlobals.Logger.Info("VHC Monitor scheduled task removed.", false);
                        return 0;
                }
            }

            // If a product flag and host are provided, imply remote execution
            if (CGlobals.TargetProductType != TargetProduct.Auto
                && !string.IsNullOrEmpty(CGlobals.REMOTEHOST)
                && !CGlobals.REMOTEHOST.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                CGlobals.REMOTEEXEC = true;
            }

            // ----------------------------------------------------------------
            // Silent / unattended mode validation and dispatch.
            //
            //   /silent + /savecreds -> mutually exclusive, exit 2.
            //   /savecreds (without /silent) -> run the seed flow and exit 0.
            //   /credfile=<path> -> load into the in-memory transient cache;
            //                       composes with /silent. Exit 6 on invalid file.
            //
            // The "/silent + no creds source" check is intentionally NOT done
            // here because at this point the parser does not yet know whether
            // a stored DPAPI credential exists for the target host. That
            // exit-2 path is enforced later by CredsHandler / CCollections
            // when a null credential is returned in silent mode.
            // ----------------------------------------------------------------
            int silentValidation = this.ValidateSilentArgs();
            if (silentValidation != 0)
            {
                Environment.Exit(silentValidation);
            }

            if (CGlobals.SaveCredsOnly)
            {
                int saveExit = this.RunSaveCredsFlow();
                Environment.Exit(saveExit);
            }

            if (!string.IsNullOrEmpty(CGlobals.CredFilePath))
            {
                int credfileExit = this.LoadCredFile(CGlobals.CredFilePath);
                if (credfileExit != 0)
                {
                    Environment.Exit(credfileExit);
                }
            }

            // Now that arguments are parsed, detect VBR version - see
            // DetectVbrVersionIfTargeted()'s doc comment for the full rationale.
            // This must happen after parsing so REMOTEEXEC flag is properly set.
            this.DetectVbrVersionIfTargeted();

            int result = 0;
            if (string.IsNullOrEmpty(CGlobals.REMOTEHOST))
            {
                //CGlobals.REMOTEHOST = "localhost";
            }

            if (runHfd)
            {
                if(CGlobals.REMOTEEXEC)
                {
                    this.functions.RunHotfixDetector(_hfdPath, CGlobals.REMOTEHOST);
                }

                this.functions.RunHotfixDetector(_hfdPath, string.Empty);
            }
            else if (ui)
                this.LaunchUi(this.Handle(), false);
            else if (run)
            {
                // The PS 7.6+ module gate is no longer called here. It's private on
                // CClientFunctions and enforced exactly once, from StartCollections(), the single
                // choke point every path below (import, remote, local) eventually reaches via
                // FullRun -> CliRun -> StartPrimaryFunctions. Calling it here too used to run it
                // unconditionally even for /import (which never reaches real collection) and
                // spawn pwsh.exe a second time on the plain local /run path.
                if (CGlobals.IMPORT)
                     result = this.FullRun(targetDir);
                else if (CGlobals.REMOTEEXEC && CGlobals.REMOTEHOST == string.Empty)
                {
                    CGlobals.Logger.Warning("Remote execution selected but no host defined. Please define host: " +
                        "/host=HOSTNAME", false);
                    Environment.Exit(0);
                }

                // else if(CGlobals.REMOTEEXEC && !CGlobals.RunSecReport)
                // {
                //    CGlobals.Logger.Warning("Remote execution not available for general Health Check. Please run the tool from a server hosting Veeam Backup & Replication", false);
                //    Environment.Exit(0);
                // }
                else if (CGlobals.REMOTEHOST != string.Empty && CGlobals.RunSecReport)
                {
                    CGlobals.Logger.Debug("Remote execution selected with host: " + CGlobals.REMOTEHOST, false);
                    result = this.FullRun(targetDir);
                }
                else if(CGlobals.REMOTEHOST != string.Empty)
                {
                    CGlobals.Logger.Debug("Remote execution selected with host: " + CGlobals.REMOTEHOST, false);
                    result = this.FullRun(targetDir);
                }
                else
                {
                    if (this.functions.ModeCheck() == "fail")
                    {
                        CGlobals.Logger.Error("No compatible software detected or remote host specified. Exiting.", false);
                        Environment.Exit(0);
                    }
                    else
                        result = this.FullRun(targetDir);
                }
            }

            return result;
        }

        private string ParsePath(string input)
        {
            try
            {
                string[] outputDir = input.Split('=', 2);
                return outputDir[1];
            }
            catch (Exception)
            {
                CGlobals.Logger.Error("Input path is invalide. Try again.");
                return null;
            }
        }

        /// <summary>
        /// Parse import path from /import:path or /import=path format.
        /// </summary>
        private string ParseImportPath(string input)
        {
            try
            {
                // Handle both /import:path and /import=path formats
                char separator = input.Contains('=') ? '=' : ':';
                int separatorIndex = input.IndexOf(separator);

                if (separatorIndex < 0 || separatorIndex >= input.Length - 1)
                {
                    CGlobals.Logger.Error("Import path parameter is empty. Usage: /import:C:\\path\\to\\csvs");
                    return null;
                }

                string path = input.Substring(separatorIndex + 1).Trim();

                // Validate path exists
                if (!Directory.Exists(path))
                {
                    CGlobals.Logger.Error($"Import path does not exist: {path}");
                    CGlobals.Logger.Info("Please verify the path and try again.");
                    return null;
                }

                return path;
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Error($"Error parsing import path: {ex.Message}");
                return null;
            }
        }

        // ----------------------------------------------------------------
        // Silent / unattended mode helpers.
        // ----------------------------------------------------------------

        /// <summary>
        /// Validates the silent-mode flag combinations that the parser can
        /// determine without further runtime context.
        ///
        /// Returns 0 when the combination is valid, or a non-zero exit code
        /// matching the table in the plan (§Exit Codes). Currently the only
        /// case enforced here is the mutually-exclusive /silent + /savecreds.
        /// </summary>
        internal int ValidateSilentArgs()
        {
            if (CGlobals.Silent && CGlobals.SaveCredsOnly)
            {
                return SilentExit.FailSilent(
                    SilentExit.CredsMissing,
                    "/silent and /savecreds are mutually exclusive.");
            }
            return SilentExit.Success;
        }

        /// <summary>
        /// Loads a credfile of the form
        /// <code>{ "host": { "username": "...", "passwordBase64": "..." } }</code>
        /// into the in-memory transient cache via
        /// <see cref="CredentialStore.SetTransient"/>. Does not write to disk.
        ///
        /// Returns 0 on success, 6 on any malformed/invalid input.
        /// </summary>
        internal int LoadCredFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return SilentExit.FailSilent(
                    SilentExit.BadCredFile,
                    "/credfile=<path> invalid: empty path.");
            }

            // Note: deliberately no separate File.Exists() check here. The
            // ReadAllText try/catch below covers missing files (and is
            // race-free vs. a TOCTOU-style check), with a tailored message
            // when the exception is FileNotFound / DirectoryNotFound.

            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (FileNotFoundException ex)
            {
                return SilentExit.FailSilent(
                    SilentExit.BadCredFile,
                    $"/credfile={path} invalid: file not found ({ex.Message}).");
            }
            catch (DirectoryNotFoundException ex)
            {
                return SilentExit.FailSilent(
                    SilentExit.BadCredFile,
                    $"/credfile={path} invalid: directory not found ({ex.Message}).");
            }
            catch (Exception ex)
            {
                return SilentExit.FailSilent(
                    SilentExit.BadCredFile,
                    $"/credfile={path} invalid: read failure ({ex.Message}).");
            }

            Dictionary<string, CredFileEntry> parsed;
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                parsed = JsonSerializer.Deserialize<Dictionary<string, CredFileEntry>>(content, opts);
            }
            catch (JsonException ex)
            {
                return SilentExit.FailSilent(
                    SilentExit.BadCredFile,
                    $"/credfile={path} invalid: JSON parse error ({ex.Message}).");
            }

            if (parsed == null || parsed.Count == 0)
            {
                return SilentExit.FailSilent(
                    SilentExit.BadCredFile,
                    $"/credfile={path} invalid: no host entries.");
            }

            // Username injection prevention: reject characters that could be
            // weaponized when the username is splatted into a PowerShell
            // command line later (PSInvoker.BuildVb365Arguments,
            // CCollections.MfaTestPassed). Quotes/backticks/$/;/newlines
            // would let a malicious credfile break out of the -Username
            // argument.
            char[] forbiddenUsernameChars = new[] { '"', '\'', '`', '$', ';', '\n', '\r' };

            int loaded = 0;
            foreach (var kvp in parsed)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null
                    || string.IsNullOrWhiteSpace(kvp.Value.Username)
                    || string.IsNullOrWhiteSpace(kvp.Value.PasswordBase64))
                {
                    return SilentExit.FailSilent(
                        SilentExit.BadCredFile,
                        $"/credfile={path} invalid: entry '{kvp.Key}' missing username or passwordBase64.");
                }

                if (kvp.Value.Username.IndexOfAny(forbiddenUsernameChars) >= 0)
                {
                    return SilentExit.FailSilent(
                        SilentExit.BadCredFile,
                        $"/credfile={path}: entry '{kvp.Key}' has invalid characters in username.");
                }

                string plain;
                try
                {
                    plain = Encoding.UTF8.GetString(Convert.FromBase64String(kvp.Value.PasswordBase64));
                }
                catch (FormatException)
                {
                    return SilentExit.FailSilent(
                        SilentExit.BadCredFile,
                        $"/credfile={path} invalid: entry '{kvp.Key}' has non-Base64 passwordBase64.");
                }

                CredentialStore.SetTransient(kvp.Key, kvp.Value.Username, plain);
                loaded++;
            }

            CGlobals.Logger.Info($"[silent] Loaded {loaded} credfile entries into transient cache.", false);
            return SilentExit.Success;
        }

        /// <summary>
        /// One-shot interactive seed flow for /savecreds. Delegates to
        /// <see cref="CredsHandler.PromptForCredentialsCli"/> (which itself
        /// calls <see cref="CredentialStore.Set"/> on success), so the seed
        /// path uses the exact same prompt + persist code path as the
        /// interactive collector flow. Returns 0 on success, 1 on
        /// cancel/empty-input/exception.
        /// </summary>
        internal int RunSaveCredsFlow()
        {
            string host = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;
            try
            {
                var result = new CredsHandler().PromptForCredentialsCli(host, headerPrefix: "Seed Credentials");
                if (result == null)
                {
                    return SilentExit.GenericFailure;
                }

                CGlobals.Logger.Info($"[savecreds] Stored credentials for host: {host}", false);
                return SilentExit.Success;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[savecreds] Error: {ex.Message}");
                CGlobals.Logger.Error($"savecreds failure: {ex.Message}");
                return SilentExit.GenericFailure;
            }
        }

        /// <summary>
        /// Test seam: applies the flag-side-effects of the foreach switch
        /// without invoking any of the run-dispatch behavior (no
        /// Environment.Exit, no PSInvoker, no GetVbrVersion). The body is a
        /// minimal mirror of the case branches in <c>ParseAllArgs</c> for
        /// flags that have unit tests. New flag-only cases that need direct
        /// unit testing should be added here.
        /// </summary>
        internal void ApplyFlagsForTest(string[] testArgs)
        {
            const string credfilePrefix = "/credfile=";
            foreach (var a in testArgs)
            {
                switch (a)
                {
                    case "/silent":
                        CGlobals.Silent = true;
                        break;
                    case "/savecreds":
                        CGlobals.SaveCredsOnly = true;
                        break;
                    default:
                        if (a.StartsWith(credfilePrefix, StringComparison.OrdinalIgnoreCase)
                            && a.Length > credfilePrefix.Length)
                        {
                            CGlobals.CredFilePath = this.ParsePath(a);
                        }
                        else if (a.StartsWith("/outdir=", StringComparison.OrdinalIgnoreCase)
                            && a.Length > "/outdir=".Length)
                        {
                            this.ApplyOutDir(this.ParsePath(a));
                        }
                        break;
                }
            }
        }

        private void ApplyOutDir(string parsedOutDir)
        {
            if (string.IsNullOrEmpty(parsedOutDir)) return;
            CGlobals.desiredPath = parsedOutDir;
            CGlobals.mainlog = new CLogger("HealthCheck");
        }

        /// <summary>
        /// Detects the VBR version right after CLI argument parsing, for any run that might
        /// touch VBR. Skipped only for an explicit /vb365-only target: DetectVbrVersion()
        /// reads the LOCAL machine's VBR registry keys, which are legitimately absent on a
        /// VB365-only server, and its failure branch logs 5 ERROR-level lines for a totally
        /// expected condition. Checked against the explicit TargetProductType flag only (not
        /// EffectiveIsVbr/IsVbr): ModeCheck(), which populates the auto-detected
        /// IsVbr/IsVb365 flags, hasn't run yet at this point in the CLI flow (it runs later,
        /// and only for a subset of dispatch branches), so an Auto-mode run's real product mix
        /// is still unknown here and detection must still be attempted.
        /// </summary>
        internal void DetectVbrVersionIfTargeted()
        {
            if (CGlobals.TargetProductType == TargetProduct.Vb365)
            {
                return;
            }

            try
            {
                this.functions.DetectVbrVersion();
            }
            catch (Exception ex)
            {
                CGlobals.Logger.Debug($"VBR version detection skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// Shape of one host entry inside the JSON credfile loaded by
        /// /credfile=<path>.
        /// </summary>
        private class CredFileEntry
        {
            public string Username { get; set; }
            public string PasswordBase64 { get; set; }
        }

        private int Run(string targetDir)
        {
            CClientFunctions functions = new();
            return functions.CliRun(targetDir);
        }

        private int FullRun(string targetDir)
        {
            CGlobals.Logger.Info("Starting RUN...", false);
            var res = this.Run(targetDir);

            CGlobals.Logger.Info("Starting RUN...complete!", false);
            CGlobals.Logger.Info("Output is stored in " + targetDir, false);

            return res;
        }

        private void RunMonitorSetup()
        {
            if (!CVhcMonitorIntegration.IsExePresentInBundle())
            {
                CGlobals.Logger.Error("vhc-monitor.exe not found alongside VeeamHealthCheck.exe. Cannot set up.", false);
                return;
            }
            var creds = CredentialStore.Get(CGlobals.VBRServerName);
            if (creds == null)
            {
                CGlobals.Logger.Warning("No stored credentials for " + CGlobals.VBRServerName + ". Run interactively to store credentials first.", false);
                return;
            }
            CVhcMonitorIntegration.Install(CGlobals.VBRServerName, creds.Value.Username, creds.Value.Password);
            CGlobals.Logger.Info("VHC Monitor installed and scheduled task registered.", false);
        }

        private int RunMonitorNow()
        {
            if (!CVhcMonitorIntegration.IsInstalled())
            {
                CGlobals.Logger.Error("VHC Monitor not installed. Run /monitor:setup first.", false);
                return 1;
            }
            var (exitCode, output) = CVhcMonitorIntegration.RunNow();
            Console.WriteLine(output);
            return exitCode;
        }

        private void PrintMonitorStatus()
        {
            bool bundled = CVhcMonitorIntegration.IsExePresentInBundle();
            bool installed = CVhcMonitorIntegration.IsInstalled();
            bool taskActive = CVhcMonitorIntegration.IsTaskRegistered();
            string version = installed ? CVhcMonitorIntegration.GetInstalledVersion() : "n/a";
            var lastRun = CVhcMonitorIntegration.GetLastRunStatus();

            Console.WriteLine("=== VHC Monitor Status ===");
            Console.WriteLine($"  Bundled:        {(bundled ? "Yes" : "No")}");
            Console.WriteLine($"  Installed:      {(installed ? "Yes (" + version + ")" : "No")}");
            Console.WriteLine($"  Scheduled Task: {(taskActive ? "Registered" : "Not registered")}");
            Console.WriteLine($"  Config:         {CGlobals.VhcMonitorConfigPath}");
            if (lastRun != null)
                Console.WriteLine($"  Last Run:       {lastRun.Timestamp:g} — {lastRun.Summary}");
            Console.WriteLine("==========================");
        }
    }
}
