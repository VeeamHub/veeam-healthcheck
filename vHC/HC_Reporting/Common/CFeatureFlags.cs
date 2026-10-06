// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Shared
{
    /// <summary>
    /// Gate for work that is not ready for general use.
    /// </summary>
    /// <remarks>
    /// <see cref="ExperimentsEnabled"/> is true only when the <c>VHC_EXPERIMENTS</c>
    /// environment variable is exactly <c>1</c> or <c>true</c> (case-insensitive, surrounding
    /// whitespace ignored). Everything else - unset, empty, <c>0</c>, <c>false</c>,
    /// <c>yes</c>, <c>on</c>, typos - is OFF. This is deliberately an allowlist: the gate
    /// exists to keep unfinished work away from users, so an unrecognised value must fail
    /// closed rather than open. The variable is re-read on every call (it is cheap, and it
    /// lets tests flip it without reflection).
    /// </remarks>
    internal static class CFeatureFlags
    {
        internal const string ExperimentsEnvVar = "VHC_EXPERIMENTS";

        internal static bool ExperimentsEnabled
        {
            get
            {
                string value = (Environment.GetEnvironmentVariable(ExperimentsEnvVar) ?? string.Empty).Trim();
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
