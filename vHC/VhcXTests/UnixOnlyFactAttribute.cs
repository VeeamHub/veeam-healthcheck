// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Marks a test that launches a real child process through a shell-script stand-in for
    /// powershell.exe/pwsh.exe, which only works where the OS can execute a script by file mode
    /// (macOS/Linux). It reports as Skipped on Windows.
    /// </summary>
    public sealed class UnixOnlyFactAttribute : FactAttribute
    {
        public UnixOnlyFactAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                this.Skip = "Needs a shell-script stand-in for the PowerShell executable (macOS/Linux only)";
            }
        }
    }
}
