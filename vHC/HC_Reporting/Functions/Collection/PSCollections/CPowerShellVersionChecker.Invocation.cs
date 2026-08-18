// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Diagnostics;
using VeeamHealthCheck.Functions.Collection;

namespace VeeamHealthCheck.Functions.Collection.PSCollections
{
    /// <summary>
    /// Process-invocation half of CPowerShellVersionChecker. Kept in a separate file (not linked
    /// into the cross-platform test project) because it depends on CCollections, which pulls in a
    /// WPF reference chain.
    /// </summary>
    internal static partial class CPowerShellVersionChecker
    {
        private const int VersionCheckTimeoutSeconds = 30;

        /// <summary>
        /// Invokes pwsh.exe to read $PSVersionTable.PSVersion. Returns false if pwsh cannot be
        /// located, fails to exit within the timeout, or its output cannot be parsed.
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

            var psi = new ProcessStartInfo
            {
                FileName = pwshPath,
                Arguments = "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                // Async reads of both streams under a bounded timeout, same as the fix applied to
                // PSInvoker for issue #149 - a synchronous ReadToEnd()/WaitForExit() here can
                // deadlock if pwsh writes enough to stderr to fill the OS pipe buffer before exiting.
                bool exited = CCollections.RunBoundedPowerShell(
                    psi, VersionCheckTimeoutSeconds, "[PwshVersionCheck]",
                    out string stdOut, out _, out _, out bool timedOut);

                if (!exited || timedOut)
                {
                    return false;
                }

                return TryParsePwshVersionOutput(stdOut, out installedVersion, out rawVersion);
            }
            catch
            {
                return false;
            }
        }
    }
}
