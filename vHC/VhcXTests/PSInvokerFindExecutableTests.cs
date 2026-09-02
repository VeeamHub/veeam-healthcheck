// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Reflection;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for FindExecutableInPath's hardcoded pwsh.exe fallback, which previously
    /// returned the default install path unconditionally (no File.Exists check) - unlike
    /// CPowerShellVersionChecker.FindPwshExecutable - causing ExecutePsScriptWithFailover's
    /// "try PS7, then PS5" loop to always attempt a nonexistent path instead of falling back to
    /// PS5 when pwsh isn't installed at all.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    [Collection("GlobalState")]
    public class PSInvokerFindExecutableTests : IDisposable
    {
        private const string DefaultPwshPath = @"C:\Program Files\PowerShell\7\pwsh.exe";
        private readonly string? _origPath;

        public PSInvokerFindExecutableTests()
        {
            _origPath = Environment.GetEnvironmentVariable("PATH");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PATH", _origPath);
        }

        private static string? InvokeFindExecutableInPath(string exeName)
        {
            var method = typeof(PSInvoker).GetMethod("FindExecutableInPath", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (string?)method!.Invoke(new PSInvoker(), new object[] { exeName });
        }

        [Fact]
        public void FindExecutableInPath_PwshInPathDirectory_ReturnsPath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"vhc-pwsh-path-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            string stubPwsh = Path.Combine(tempDir, "pwsh.exe");
            File.WriteAllText(stubPwsh, string.Empty);

            try
            {
                Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + _origPath);

                string? result = InvokeFindExecutableInPath("pwsh.exe");

                Assert.Equal(stubPwsh, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindExecutableInPath_DefaultPathMissing_ReturnsNull()
        {
            // This regression guard only applies when the CI image doesn't happen to have
            // PowerShell 7 installed at the literal hardcoded default path - if it does, the
            // "missing" branch can't be exercised on that machine, so skip rather than assert
            // falsely.
            if (File.Exists(DefaultPwshPath))
            {
                return;
            }

            // Point PATH at a directory guaranteed not to contain pwsh.exe, removing the
            // PATH-hit branch so only the hardcoded-default fallback is exercised.
            string emptyDir = Path.Combine(Path.GetTempPath(), $"vhc-pwsh-empty-{Guid.NewGuid()}");
            Directory.CreateDirectory(emptyDir);

            try
            {
                Environment.SetEnvironmentVariable("PATH", emptyDir);

                string? result = InvokeFindExecutableInPath("pwsh.exe");

                Assert.Null(result);
            }
            finally
            {
                Directory.Delete(emptyDir, true);
            }
        }
    }
}
