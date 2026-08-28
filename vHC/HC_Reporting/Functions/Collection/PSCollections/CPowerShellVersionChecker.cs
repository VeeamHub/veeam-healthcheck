// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
#nullable enable
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace VeeamHealthCheck.Functions.Collection.PSCollections
{
    /// <summary>
    /// Outcome of comparing an installed PowerShell 7 against the VBR PowerShell module's minimum
    /// required version. Kept separate from message text (see BuildPwshVersionFailureMessage) so
    /// both the decision and the wording are independently unit-testable.
    /// </summary>
    internal enum PwshVersionStatus
    {
        MeetsRequirement,
        NotInstalled,
        VersionInconclusive,
        BelowRequirement
    }

    /// <summary>
    /// Determines whether the PowerShell 7 install on this machine meets the minimum version
    /// required by the installed Veeam.Backup.PowerShell module, so callers can fail fast with an
    /// actionable message instead of letting Import-Module abort deep inside a collection script.
    /// Split into a partial class: this file holds the pure parsing/derivation logic (no WPF or
    /// process-invocation dependency, so it can be linked into the cross-platform test project);
    /// CPowerShellVersionChecker.Invocation.cs holds the process-invocation half.
    /// </summary>
    internal static partial class CPowerShellVersionChecker
    {
        private static readonly Regex ManifestVersionRegex =
            new(@"PowerShellVersion\s*=\s*['""]([\d.]+)['""]", RegexOptions.IgnoreCase);

        private static readonly Regex AnsiEscapeRegex =
            new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

        /// <summary>
        /// Reads the required "PowerShellVersion" entry from a PowerShell module manifest (.psd1).
        /// Returns false (with requiredVersion null) if the file is missing or unparsable so callers
        /// can skip the preflight check rather than block a run over a manifest format they don't recognize.
        /// </summary>
        public static bool TryGetManifestRequiredVersion(string manifestPath, out Version? requiredVersion)
        {
            requiredVersion = null;

            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                return false;
            }

            try
            {
                string content = File.ReadAllText(manifestPath);
                return TryParseManifestContent(content, out requiredVersion);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryParseManifestContent(string manifestContent, out Version? requiredVersion)
        {
            requiredVersion = null;

            if (string.IsNullOrEmpty(manifestContent))
            {
                return false;
            }

            // Skip whole-line comments so a stale commented-out entry (e.g. left behind after a
            // manual edit) can never win over the live one below it.
            foreach (string line in manifestContent.Split('\n'))
            {
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                Match match = ManifestVersionRegex.Match(line);
                if (match.Success && Version.TryParse(match.Groups[1].Value, out requiredVersion))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryParsePwshVersionOutput(string rawOutput, out Version? installedVersion, out string? rawVersion)
        {
            installedVersion = null;
            rawVersion = null;

            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                return false;
            }

            // Strip ANSI/SGR codes pwsh may emit on stdout (RunBoundedPowerShell only strips
            // stderr) and take the last non-blank line, since $PSVersionTable.PSVersion.ToString()'s
            // result is always printed last - any warning/notice text goes on earlier lines.
            string cleaned = AnsiEscapeRegex.Replace(rawOutput, string.Empty);

            string? lastNonEmptyLine = null;
            foreach (string line in cleaned.Split('\n'))
            {
                string trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    lastNonEmptyLine = trimmedLine;
                }
            }

            if (string.IsNullOrEmpty(lastNonEmptyLine))
            {
                return false;
            }

            rawVersion = lastNonEmptyLine;

            // Strip prerelease/build metadata, e.g. "7.6.0-preview.3" -> "7.6.0"
            string numericPart = lastNonEmptyLine.Split('-')[0].Trim();

            return Version.TryParse(numericPart, out installedVersion);
        }

        private static string? FindPwshExecutable()
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (string dir in pathEnv.Split(Path.PathSeparator))
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim(), "pwsh.exe");
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch
                    {
                        // Ignore malformed PATH segments
                    }
                }
            }

            const string defaultPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
            return File.Exists(defaultPath) ? defaultPath : null;
        }

        /// <summary>
        /// Decides what, if anything, is wrong with the installed PowerShell 7 relative to what the
        /// VBR PowerShell module requires. NotInstalled is checked first and unconditionally so a
        /// simultaneously-unreadable module manifest (requiredVersion null) can never mask a
        /// completely missing PowerShell 7 install - the scenario issue #135 was filed about.
        /// </summary>
        internal static PwshVersionStatus EvaluatePwshVersionStatus(string? pwshPath, Version? installedVersion, Version? requiredVersion)
        {
            if (string.IsNullOrEmpty(pwshPath))
            {
                return PwshVersionStatus.NotInstalled;
            }

            if (requiredVersion == null || installedVersion == null)
            {
                return PwshVersionStatus.VersionInconclusive;
            }

            return installedVersion < requiredVersion ? PwshVersionStatus.BelowRequirement : PwshVersionStatus.MeetsRequirement;
        }

        /// <summary>
        /// Builds the actionable failure message for the NotInstalled and BelowRequirement statuses.
        /// Never called for MeetsRequirement/VersionInconclusive - those aren't failures.
        /// </summary>
        internal static string BuildPwshVersionFailureMessage(PwshVersionStatus status, string vbrFullVersion, Version? requiredVersion, string? rawInstalledVersion)
        {
            string requirementClause = requiredVersion != null
                ? $"requires PowerShell {requiredVersion} or higher"
                : "requires PowerShell 7";

            return status switch
            {
                PwshVersionStatus.NotInstalled =>
                    $"The Veeam Backup & Replication PowerShell module (VBR {vbrFullVersion}) {requirementClause}, " +
                    "but no PowerShell 7 installation was found on this computer. Install PowerShell 7 " +
                    "(https://aka.ms/powershell-release?tag=stable) and re-run Veeam Health Check.",

                PwshVersionStatus.BelowRequirement =>
                    $"The Veeam Backup & Replication PowerShell module (VBR {vbrFullVersion}) {requirementClause}, " +
                    $"but this computer has PowerShell {rawInstalledVersion} installed. Install a newer PowerShell 7 " +
                    "release (https://aka.ms/powershell-release?tag=stable) and re-run Veeam Health Check.",

                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "BuildPwshVersionFailureMessage only supports NotInstalled and BelowRequirement.")
            };
        }
    }
}
