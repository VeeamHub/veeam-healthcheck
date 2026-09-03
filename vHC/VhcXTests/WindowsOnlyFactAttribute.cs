// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Marks a test that exercises behavior with no cross-platform equivalent (DPAPI,
    /// Windows ACLs, the registry, ...) so it reports as Skipped rather than Failed when
    /// the suite runs on macOS/Linux. xUnit reads Skip off the attribute instance after
    /// construction, so it can be set conditionally here rather than needing to be a
    /// compile-time constant.
    /// </summary>
    public sealed class WindowsOnlyFactAttribute : FactAttribute
    {
        public WindowsOnlyFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                this.Skip = "Windows-only behavior (no cross-platform equivalent)";
            }
        }
    }
}
