// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    internal static class CVmcLogFileSelector
    {
        internal static string? SelectVmcLogFile(IEnumerable<string>? filePaths)
        {
            if (filePaths == null)
            {
                return null;
            }

            List<string> matches = filePaths.Where(f => f.Contains("VMC.log")).ToList();

            // Directory.GetFiles() enumeration order is filesystem-defined, not chronological, so
            // when a rotated backup (VMC.log.1, VMC.log.2, ...) sits alongside the live file, prefer
            // the exact "VMC.log" name deterministically instead of whichever the OS happens to list first.
            // These are always Windows-style paths (vb365Logs is a hardcoded "C:\..." constant), so
            // check the suffix directly rather than via Path.GetFileName, which splits on the host
            // OS's separator and would treat the whole backslash-delimited path as one filename here
            // when running on non-Windows (e.g. this cross-platform test project on macOS/Linux CI).
            return matches.FirstOrDefault(f => f.EndsWith(@"\VMC.log", StringComparison.Ordinal) || f == "VMC.log")
                ?? matches.FirstOrDefault();
        }
    }
}
