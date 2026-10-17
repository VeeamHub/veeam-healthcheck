// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Shared
{
    /// <summary>
    /// Centralizes silent / unattended mode exit codes, the user-facing
    /// "[silent]" prefix, and the canonical stderr+log+return / stderr+exit
    /// patterns used at silent-mode failure call sites.
    ///
    /// Adding a new silent exit code: add the constant here, then update
    /// the help-menu copy in <see cref="CMessages.helpMenu"/> by hand.
    /// The help-menu copy is intentionally not generated from these constants
    /// because it is documentation that may need to expand on each code's
    /// meaning beyond the constant name.
    /// </summary>
    internal static class SilentExit
    {
        public const string Prefix = "[silent]";
        public const int Success = 0;
        public const int GenericFailure = 1;
        public const int CredsMissing = 2;
        public const int AuthFailed = 3;
        public const int MfaUnsupported = 4;
        public const int HostUnreachable = 5;
        public const int BadCredFile = 6;
        public const int NoProductDetected = 7;
        public const int PowerShellVersionUnsupported = 8;

        /// <summary>
        /// Used at silent-mode failure call sites that return an exit code
        /// up the stack (e.g. <c>CArgsParser.LoadCredFile</c>,
        /// <c>CArgsParser.ValidateSilentArgs</c>). Writes the prefixed message
        /// to stderr, logs the bare message, and returns the supplied code so
        /// the caller can <c>Environment.Exit</c> at a single chokepoint.
        /// </summary>
        internal static int FailSilent(int code, string msg)
        {
            string prefixed = $"{Prefix} {msg}";
            Console.Error.WriteLine(prefixed);
            CGlobals.Logger.Error(msg, false);
            return code;
        }

        /// <summary>
        /// Used at silent-mode failure call sites that exit the process
        /// directly (e.g. branches inside <c>CCollections.MfaTestPassed</c>
        /// where we cannot bubble an exit code back up through the call
        /// graph). Writes the prefixed message to stderr and exits with the
        /// supplied code.
        /// </summary>
        internal static void ExitSilent(int code, string msg)
        {
            Console.Error.WriteLine($"{Prefix} {msg}");
            Environment.Exit(code);
        }
    }
}
