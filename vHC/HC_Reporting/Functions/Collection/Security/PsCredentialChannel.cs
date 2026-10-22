using System;
using System.Diagnostics;

namespace VeeamHealthCheck.Functions.Collection.Security
{
    /// <summary>
    /// The one place that decides how a password reaches a PowerShell child process.
    /// <para>
    /// A process command line is readable by other local processes and is recorded by Windows
    /// process-creation auditing (event 4688) and Sysmon event 1, so a password must never be part
    /// of <see cref="ProcessStartInfo.Arguments"/> — not in plain text and not Base64-encoded
    /// (Base64 is encoding, not protection). Instead the password is placed in the child's own
    /// environment block through <see cref="ProcessStartInfo.Environment"/>: that value is handed to
    /// that one child at creation, is never part of vHC's own environment, and is not written to any
    /// audit log of the command line. The receiving script reads it once and removes it from its
    /// environment immediately, so nothing the script launches later inherits it.
    /// </para>
    /// </summary>
    internal static class PsCredentialChannel
    {
        /// <summary>Name of the per-process environment variable that carries the password.</summary>
        internal const string PasswordVariable = "VHC_CRED_PASSWORD";

        /// <summary>
        /// Puts <paramref name="password"/> in the environment of the process that
        /// <paramref name="startInfo"/> will create. Nothing is added to the argument string.
        /// </summary>
        internal static void SetPassword(ProcessStartInfo startInfo, string password)
        {
            ArgumentNullException.ThrowIfNull(startInfo);

            if (startInfo.UseShellExecute)
            {
                // With ShellExecute the OS launches the process and ignores Environment; failing
                // here is better than silently starting a child that never receives its password.
                throw new InvalidOperationException(
                    "A credentialed PowerShell child must be started with UseShellExecute = false.");
            }

            startInfo.Environment[PasswordVariable] = password ?? string.Empty;
        }

        /// <summary>
        /// PowerShell statements for inline <c>-Command</c> scripts: read the password from the
        /// environment into <paramref name="variableName"/> (for example <c>$vhcPw</c>) and remove it
        /// from the environment straight away. Ends with <c>"; "</c> so it can be concatenated.
        /// </summary>
        internal static string ReadAndClearPasswordStatement(string variableName)
        {
            return $"{variableName} = [Environment]::GetEnvironmentVariable('{PasswordVariable}', 'Process'); " +
                   $"[Environment]::SetEnvironmentVariable('{PasswordVariable}', $null, 'Process'); ";
        }
    }
}
