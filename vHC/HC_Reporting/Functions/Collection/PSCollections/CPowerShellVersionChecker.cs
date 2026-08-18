// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace VeeamHealthCheck.Functions.Collection.PSCollections
{
    /// <summary>
    /// Determines whether the PowerShell 7 install on this machine meets the minimum version
    /// required by the installed Veeam.Backup.PowerShell module, so callers can fail fast with an
    /// actionable message instead of letting Import-Module abort deep inside a collection script.
    /// </summary>
    internal static class CPowerShellVersionChecker
    {
        private static readonly Regex ManifestVersionRegex =
            new(@"PowerShellVersion\s*=\s*['""]([\d.]+)['""]", RegexOptions.IgnoreCase);

        /// <summary>
        /// Reads the required "PowerShellVersion" entry from a PowerShell module manifest (.psd1).
        /// Returns false (with requiredVersion null) if the file is missing or unparsable so callers
        /// can skip the preflight check rather than block a run over a manifest format they don't recognize.
        /// </summary>
        public static bool TryGetManifestRequiredVersion(string manifestPath, out Version requiredVersion)
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

        internal static bool TryParseManifestContent(string manifestContent, out Version requiredVersion)
        {
            requiredVersion = null;

            if (string.IsNullOrEmpty(manifestContent))
            {
                return false;
            }

            Match match = ManifestVersionRegex.Match(manifestContent);
            return match.Success && Version.TryParse(match.Groups[1].Value, out requiredVersion);
        }

        /// <summary>
        /// Invokes pwsh.exe to read $PSVersionTable.PSVersion. Returns false if pwsh cannot be
        /// located or its output cannot be parsed.
        /// </summary>
        public static bool TryGetInstalledPwshVersion(out Version installedVersion, out string rawVersion)
        {
            installedVersion = null;
            rawVersion = null;

            string pwshPath = FindPwshExecutable();
            if (string.IsNullOrEmpty(pwshPath))
            {
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pwshPath,
                    Arguments = "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return TryParsePwshVersionOutput(output, out installedVersion, out rawVersion);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryParsePwshVersionOutput(string rawOutput, out Version installedVersion, out string rawVersion)
        {
            installedVersion = null;
            rawVersion = rawOutput?.Trim();

            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return false;
            }

            // Strip prerelease/build metadata, e.g. "7.6.0-preview.3" -> "7.6.0"
            string numericPart = rawVersion.Split('-')[0].Trim();

            return Version.TryParse(numericPart, out installedVersion);
        }

        private static string FindPwshExecutable()
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
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
    }
}
