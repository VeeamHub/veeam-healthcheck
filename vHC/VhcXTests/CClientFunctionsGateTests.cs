// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Reflection;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for the PS 7.6+ module preflight gate's call-site contract: GetVbrVersion
    /// (gated: detect + hard-exit-if-too-old) must only ever be called from StartCollections, the
    /// single choke point immediately before real PowerShell-module-based collection. Every other
    /// caller (ModeCheck, RunHotfixDetector) must use the ungated DetectVbrVersion so a
    /// too-old-PowerShell machine doesn't hard-exit a feature that never touches
    /// Veeam.Backup.PowerShell.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    [Collection("GlobalState")]
    public class CClientFunctionsGateTests : IDisposable
    {
        private readonly bool _origImport;
        private readonly int _origMajorVersion;
        private readonly string _origConsoleInstallDir;
        private readonly int _origPowerShellVersion;
        private readonly bool _origIsVbr;
        private readonly bool _origIsVb365;

        public CClientFunctionsGateTests()
        {
            _origImport = CGlobals.IMPORT;
            _origMajorVersion = CGlobals.VBRMAJORVERSION;
            _origConsoleInstallDir = CGlobals.VbrConsoleInstallDir;
            _origPowerShellVersion = CGlobals.PowerShellVersion;
            _origIsVbr = CGlobals.IsVbr;
            _origIsVb365 = CGlobals.IsVb365;
        }

        public void Dispose()
        {
            CGlobals.IMPORT = _origImport;
            CGlobals.VBRMAJORVERSION = _origMajorVersion;
            CGlobals.VbrConsoleInstallDir = _origConsoleInstallDir;
            CGlobals.PowerShellVersion = _origPowerShellVersion;
            CGlobals.IsVbr = _origIsVbr;
            CGlobals.IsVb365 = _origIsVb365;
        }

        [Fact]
        public void GetVbrVersion_MethodVisibility_IsPrivate()
        {
            // Regression guard for the root cause of the ModeCheck() hard-exit-on-GUI-startup
            // bug: GetVbrVersion must stay private so StartCollections() remains its only
            // possible caller.
            var method = typeof(CClientFunctions).GetMethod(
                "GetVbrVersion", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);
            Assert.True(method.IsPrivate);
        }

        [Fact]
        public void StartCollections_ImportModeEnabled_DoesNotThrow()
        {
            // Smoke test only: on a CI runner without VBR installed, DetectVbrVersion() would
            // leave VBRMAJORVERSION at 0 whether or not the !IMPORT guard actually skipped it, so
            // this can't distinguish "gate correctly skipped" from "gate ran and failed anyway".
            // The real regression guard for the gate's call-site contract is
            // GetVbrVersion_MethodVisibility_IsPrivate below, which makes StartCollections() the
            // only possible caller at compile time.
            CGlobals.IMPORT = true;

            using var functions = new CClientFunctions();
            var method = typeof(CClientFunctions).GetMethod(
                "StartCollections", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            Exception? ex = Record.Exception(() => method.Invoke(functions, Array.Empty<object>()));

            Assert.Null(ex);
        }

        [Fact]
        public void RunHotfixDetector_DetectVbrVersionThrows_DoesNotPropagate()
        {
            CGlobals.VBRMAJORVERSION = 0;
            CGlobals.VbrConsoleInstallDir = null;

            using var functions = new CClientFunctions();

            // A UNC path is rejected by VerifyPath before CHotfixDetector ever runs, keeping this
            // test focused on the DetectVbrVersion try/catch (added by this fix) rather than
            // exercising the real hotfix-detection flow.
            Exception? ex = Record.Exception(() => functions.RunHotfixDetector(@"\\invalid\nonexistent\path", string.Empty));

            Assert.Null(ex);
        }

        [Fact]
        public void ModeCheck_NoVeeamProcessRunning_ReturnsFailWithoutThrowing()
        {
            CGlobals.IsVbr = false;
            CGlobals.IsVb365 = false;

            using var functions = new CClientFunctions();

            string? result = null;
            Exception? ex = Record.Exception(() => { result = functions.ModeCheck(); });

            Assert.Null(ex);

            // Only meaningful when no Veeam.Backup.Service/Veeam.Archiver.Service process is
            // actually running on the test machine (true for standard CI runners) - otherwise
            // ModeCheck() legitimately won't return "fail".
            if (!CGlobals.IsVbr && !CGlobals.IsVb365)
            {
                Assert.Equal("fail", result);
            }
        }
    }
}
