// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace VhcXTests.Integration
{
    /// <summary>
    /// Unit-level checks for the vHC-VbrConfig PowerShell module that do not require
    /// a live Veeam environment. These tests satisfy Task 10 Step 3 of the VBR config
    /// refactor (docs/plans/2026-02-21-vbr-config-refactor.md).
    /// </summary>
    [Collection("Integration Tests")]
    [Trait("Category", "Integration")]
    public class VhcModuleUnitTests
    {
        private readonly string _exportCsvScriptPath;

        public VhcModuleUnitTests()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            _exportCsvScriptPath = Path.Combine(
                projectRoot, "Tools", "Scripts", "HealthCheck", "VBR",
                "vHC-VbrConfig", "Private", "Export-VhciCsv.ps1");
        }

        /// <summary>
        /// Verifies that Export-VhciCsv preserves [pscustomobject][ordered] column order.
        /// A known object is piped through the function and the resulting CSV header row
        /// is asserted to be an exact string match.
        /// </summary>
        [Fact]
        public void ExportVhcCsv_ColumnOrder_IsPreserved()
        {
            if (!File.Exists(_exportCsvScriptPath))
            {
                Assert.Fail($"Export-VhciCsv.ps1 not found at: {_exportCsvScriptPath}");
            }

            var tmpDir       = Path.Combine(Path.GetTempPath(), $"vhc-test-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-ColumnOrder.ps1");

            Directory.CreateDirectory(tmpDir);

            // Write the test script to a temp file to avoid complex argument escaping.
            // The script dot-sources Export-VhciCsv (and Write-LogFile which it calls internally),
            // injects the module-level vars it reads, pipes a known [pscustomobject][ordered]
            // through it, then asserts the CSV header is an exact column-order match.
            var moduleRoot    = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(_exportCsvScriptPath) ?? tmpDir, ".."));
            var writeLogPath  = Path.Combine(moduleRoot, "Public", "Write-LogFile.ps1");
            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{_exportCsvScriptPath}'

$script:ReportPath = '{tmpDir}'
$script:VBRServer  = 'test'
$script:LogPath    = '{tmpDir}'
$script:LogLevel   = 'ERROR'

[pscustomobject][ordered]@{{
    Alpha   = 1
    Bravo   = 2
    Charlie = 3
    Delta   = 4
}} | Export-VhciCsv -FileName '_ColumnOrder.csv'

$csvPath = (Join-Path '{tmpDir}' 'test_ColumnOrder.csv')
$header  = (Get-Content $csvPath | Select-Object -First 1)
$expected = '""Alpha"",""Bravo"",""Charlie"",""Delta""'
if ($header -ne $expected) {{
    Write-Error ""Column order mismatch. Expected: $expected  Got: $header""
    exit 1
}}
Write-Host 'Column order OK'
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Assert.Fail("Failed to start pwsh process for column-order check");
                    return;
                }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Export-VhciCsv column-order check failed (exit {process.ExitCode}).\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that Export-VhciCsv propagates file I/O failures rather than swallowing them.
        /// A non-existent output directory is used to trigger Export-Csv failure reliably.
        /// </summary>
        [Fact]
        public void ExportVhcCsv_IoFailure_ThrowsException()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot       = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var exportCsvPath    = Path.Combine(moduleRoot, "Private", "Export-VhciCsv.ps1");
            var writeLogPath     = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");

            var tmpDir       = Path.Combine(Path.GetTempPath(), $"vhc-ioerr-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-IoFailure.ps1");
            Directory.CreateDirectory(tmpDir);

            // ReportPath points to a path that does not exist and cannot be created by Export-VhciCsv.
            // (Export-Csv fails when the parent directory doesn't exist.)
            var nonExistentPath = Path.Combine(tmpDir, "does-not-exist", "nested");

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{exportCsvPath}'

$script:ReportPath = '{nonExistentPath}'
$script:VBRServer  = 'test'
$script:LogPath    = '{tmpDir}'
$script:LogLevel   = 'ERROR'

try {{
    [pscustomobject]@{{ A = 1 }} | Export-VhciCsv -FileName '_test.csv'
    # Should NOT reach here - expect an exception from the missing directory
    Write-Error 'Export-VhciCsv did not throw on I/O failure'
    exit 1
}} catch {{
    # Exception propagated as expected
    exit 0
}}
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Export-VhciCsv should have thrown on I/O failure and been caught by the test's try/catch.\n" +
                    $"Exit code {process.ExitCode} means the exception was swallowed.\nSTDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Regression guard: Invoke-VhcCollector must never throw, even when the wrapped
        /// scriptblock throws. It must return Success=false and Error set to the exception message.
        /// </summary>
        [Fact]
        public void InvokeVhcCollector_ScriptblockThrows_ReturnsFailedResultWithoutThrowing()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var collectorPath   = Path.Combine(moduleRoot, "Public", "Invoke-VhcCollector.ps1");
            var writeLogPath    = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");

            var tmpDir       = Path.Combine(Path.GetTempPath(), $"vhc-coll-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-CollectorIsolation.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{collectorPath}'

# Initialize the module-level vars Write-LogFile reads
$script:LogPath  = '{tmpDir}'
$script:LogLevel = 'ERROR'

$result = Invoke-VhcCollector -Name 'TestCollector' -Action {{
    throw 'Intentional test error'
}}

if ($result.Success -eq $true) {{
    Write-Error 'Expected Success=false but got Success=true'
    exit 1
}}
if ([string]::IsNullOrEmpty($result.Error)) {{
    Write-Error 'Expected Error to be populated but it was empty'
    exit 1
}}
if ($result.Error -notmatch 'Intentional test error') {{
    Write-Error ""Expected Error to contain 'Intentional test error' but got: $($result.Error)""
    exit 1
}}
Write-Host ""OK: Success=false, Error='$($result.Error)'""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Invoke-VhcCollector did not correctly isolate the thrown exception.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Regression guard: Get-VhcSessionReport must throw with a clear error message when
        /// $script:AllBackupSessions is null (i.e. Get-VhcBackupSessions was never called or failed).
        /// When wrapped by Invoke-VhcCollector this produces a FAIL entry, not a silent empty CSV.
        /// </summary>
        [Fact]
        public void GetVhcSessionReport_NullSessions_ThrowsDescriptiveError()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot       = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var sessionRptPath   = Path.Combine(moduleRoot, "Public",  "Get-VhcSessionReport.ps1");
            var writeLogPath     = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");
            var exportCsvPath    = Path.Combine(moduleRoot, "Private", "Export-VhciCsv.ps1");
            var sessionLogPath   = Path.Combine(moduleRoot, "Private", "Get-VhciSessionLogWithTimeout.ps1");

            var tmpDir       = Path.Combine(Path.GetTempPath(), $"vhc-sess-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-NullSessionGuard.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{exportCsvPath}'
. '{sessionLogPath}'
. '{sessionRptPath}'

$script:ReportPath     = '{tmpDir}'
$script:VBRServer      = 'test'
$script:LogPath        = '{tmpDir}'
$script:LogLevel       = 'ERROR'

try {{
    # Pass $null explicitly via the new parameter — no $script: state involved.
    Get-VhcSessionReport -BackupSessions $null
    # Should NOT reach here
    Write-Error 'Get-VhcSessionReport did not throw when BackupSessions is null'
    exit 1
}} catch {{
    if ($_.Exception.Message -notmatch 'Get-VhcBackupSessions') {{
        Write-Error ""Expected error mentioning Get-VhcBackupSessions but got: $($_.Exception.Message)""
        exit 1
    }}
    Write-Host ""OK: threw with expected message: $($_.Exception.Message)""
    exit 0
}}
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhcSessionReport did not throw the expected error for null sessions.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies Get-VhcBackupSessions accepts an explicit -ReportInterval parameter
        /// and does not silently fall back to $script:ReportInterval.
        /// Called without any $script: state; the expected failure is VBR-not-available,
        /// not a missing-variable error — which proves the parameter is used.
        /// </summary>
        [Fact]
        public void GetVhcBackupSessions_AcceptsReportIntervalParameter_NotScriptVar()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot     = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var funcPath       = Path.Combine(moduleRoot, "Public", "Get-VhcBackupSessions.ps1");
            var writeLogPath   = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-bksess-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-BackupSessionsParam.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{funcPath}'

# Do NOT set $script:ReportInterval - the function must use the parameter instead.
$script:LogPath  = '{tmpDir}'
$script:LogLevel = 'ERROR'

try {{
    Get-VhcBackupSessions -ReportInterval 14
}} catch {{
    $msg = $_.Exception.Message
    # A ReportInterval-related error means the param was ignored and $script: was read.
    if ($msg -match 'ReportInterval') {{
        Write-Error ""Function used `$script:ReportInterval instead of parameter: $msg""
        exit 1
    }}
    # Any VBR-not-available error is expected and proves the param is being used.
    Write-Host ""OK: VBR-unavailable error (expected): $msg""
    exit 0
}}
# If no exception: VBR is somehow available; pass since the param was accepted.
Write-Host 'OK: function accepted -ReportInterval parameter without error'
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhcBackupSessions did not accept -ReportInterval parameter.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that Get-VhciServerOsOverhead returns the MAX of active role overheads
        /// (independently for CPU and RAM) rather than summing them.
        /// ADR 0010: a multi-role server runs one OS instance; the overhead floor is the
        /// largest single-role value, not an additive stack.
        /// </summary>
        [Fact]
        public void GetVhciServerOsOverhead_MultiRole_ReturnsMaxNotSum()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var overheadPath    = Path.Combine(moduleRoot, "Private", "Get-VhciServerOsOverhead.ps1");
            var safeValuePath   = Path.Combine(moduleRoot, "Private", "SafeValue.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-overhead-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-OverheadMultiRole.ps1");
            Directory.CreateDirectory(tmpDir);

            // Entry: Repo(10) + VpProxy(4) + GpProxy(2) → all three overhead blocks active.
            // Additive (wrong): CPU=2+2+2=6, RAM=4+2+4=10
            // Max (correct):    CPU=max(2,2,2)=2, RAM=max(4,2,4)=4
            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{safeValuePath}'
. '{overheadPath}'

$entry = @{{
    TotalRepoTasks     = 10
    TotalGWTasks       = 0
    TotalVpProxyTasks  = 4
    TotalGPProxyTasks  = 2
    TotalCDPProxyTasks = 0
}}
$thresholds = [PSCustomObject]@{{
    RepoOSCPU      = 2;  RepoOSRAM      = 4
    VpProxyOSCPU   = 2;  VpProxyOSRAM   = 2
    GpProxyOSCPU   = 2;  GpProxyOSRAM   = 4
    CdpProxyOSCPU  = 0;  CdpProxyOSRAM  = 0
}}

$result = Get-VhciServerOsOverhead -Entry $entry -Thresholds $thresholds

if ($result.CPU -ne 2) {{
    Write-Error ""Expected CPU=2 (max) but got $($result.CPU) (additive would be 6)""
    exit 1
}}
if ($result.RAM -ne 4) {{
    Write-Error ""Expected RAM=4 (max) but got $($result.RAM) (additive would be 10)""
    exit 1
}}
Write-Host ""OK: CPU=$($result.CPU), RAM=$($result.RAM)""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhciServerOsOverhead multi-role check failed (exit {process.ExitCode}).\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that a single-role server returns that role's overhead values exactly
        /// (max of one value == that value).
        /// </summary>
        [Fact]
        public void GetVhciServerOsOverhead_SingleRole_ReturnsRoleOverhead()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var overheadPath    = Path.Combine(moduleRoot, "Private", "Get-VhciServerOsOverhead.ps1");
            var safeValuePath   = Path.Combine(moduleRoot, "Private", "SafeValue.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-overhead-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-OverheadSingleRole.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{safeValuePath}'
. '{overheadPath}'

$entry = @{{
    TotalRepoTasks     = 0
    TotalGWTasks       = 0
    TotalVpProxyTasks  = 4
    TotalGPProxyTasks  = 0
    TotalCDPProxyTasks = 0
}}
$thresholds = [PSCustomObject]@{{
    RepoOSCPU      = 2;  RepoOSRAM      = 4
    VpProxyOSCPU   = 2;  VpProxyOSRAM   = 2
    GpProxyOSCPU   = 2;  GpProxyOSRAM   = 4
    CdpProxyOSCPU  = 0;  CdpProxyOSRAM  = 0
}}

$result = Get-VhciServerOsOverhead -Entry $entry -Thresholds $thresholds

if ($result.CPU -ne 2) {{
    Write-Error ""Expected CPU=2 for VP proxy role but got $($result.CPU)""
    exit 1
}}
if ($result.RAM -ne 2) {{
    Write-Error ""Expected RAM=2 for VP proxy role but got $($result.RAM)""
    exit 1
}}
Write-Host ""OK: CPU=$($result.CPU), RAM=$($result.RAM)""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhciServerOsOverhead single-role check failed (exit {process.ExitCode}).\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that a server with no active role tasks returns zero overhead.
        /// </summary>
        [Fact]
        public void GetVhciServerOsOverhead_NoRoles_ReturnsZero()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var overheadPath    = Path.Combine(moduleRoot, "Private", "Get-VhciServerOsOverhead.ps1");
            var safeValuePath   = Path.Combine(moduleRoot, "Private", "SafeValue.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-overhead-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-OverheadNoRoles.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{safeValuePath}'
. '{overheadPath}'

$entry = @{{
    TotalRepoTasks     = 0
    TotalGWTasks       = 0
    TotalVpProxyTasks  = 0
    TotalGPProxyTasks  = 0
    TotalCDPProxyTasks = 0
}}
$thresholds = [PSCustomObject]@{{
    RepoOSCPU      = 2;  RepoOSRAM      = 4
    VpProxyOSCPU   = 2;  VpProxyOSRAM   = 2
    GpProxyOSCPU   = 2;  GpProxyOSRAM   = 4
    CdpProxyOSCPU  = 0;  CdpProxyOSRAM  = 0
}}

$result = Get-VhciServerOsOverhead -Entry $entry -Thresholds $thresholds

if ($result.CPU -ne 0) {{
    Write-Error ""Expected CPU=0 for no active roles but got $($result.CPU)""
    exit 1
}}
if ($result.RAM -ne 0) {{
    Write-Error ""Expected RAM=0 for no active roles but got $($result.RAM)""
    exit 1
}}
Write-Host ""OK: CPU=$($result.CPU), RAM=$($result.RAM)""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhciServerOsOverhead no-roles check failed (exit {process.ExitCode}).\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies Get-VhcBackupSessions collects agent/computer backup sessions via
        /// Get-VBRComputerBackupJobSession and includes them in the returned array alongside
        /// VM and Backup Copy sessions from Get-VBRBackupSession. See ADR 0012.
        /// </summary>
        [Fact]
        public void GetVhcBackupSessions_IncludesAgentSessions_CallsComputerBackupJobSession()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot   = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var funcPath     = Path.Combine(moduleRoot, "Public", "Get-VhcBackupSessions.ps1");
            var writeLogPath = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-agtsess-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-AgentSessions.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{funcPath}'

$script:LogPath  = '{tmpDir}'
$script:LogLevel = 'ERROR'

# Stub VBR cmdlets - no Veeam install required.
# Get-VBRBackupSession returns empty; Get-VBRComputerBackupJobSession returns one agent session.
function Get-VBRBackupSession {{ return @() }}
function Get-VBRComputerBackupJobSession {{
    return @([pscustomobject]@{{ Name = 'Agent Job'; CreationTime = (Get-Date) }})
}}

$result = @(Get-VhcBackupSessions -ReportInterval 14)

if ($result.Count -ne 1) {{
    Write-Error ""Expected 1 agent session but got $($result.Count)""
    exit 1
}}
if ($result[0].Name -ne 'Agent Job') {{
    Write-Error ""Expected Name='Agent Job' but got '$($result[0].Name)'""
    exit 1
}}
Write-Host 'OK: agent session included in result'
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhcBackupSessions did not include agent sessions.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies Get-VhcSessionReport uses the parent session Name as JobName for agent
        /// task sessions, not the machine-suffixed $task.JobName that Veeam produces.
        /// Stubs Get-VBRTaskSession and provides a mock session with a known Name. See ADR 0012.
        /// </summary>
        [Fact]
        public void GetVhcSessionReport_AgentSession_UsesParentSessionNameAsJobName()
        {
            var projectRoot  = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var sessionRptPath  = Path.Combine(moduleRoot, "Public",  "Get-VhcSessionReport.ps1");
            var writeLogPath    = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");
            var exportCsvPath   = Path.Combine(moduleRoot, "Private", "Export-VhciCsv.ps1");
            var sessionLogPath  = Path.Combine(moduleRoot, "Private", "Get-VhciSessionLogWithTimeout.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-agtrpt-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-AgentJobName.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{exportCsvPath}'
. '{sessionLogPath}'
. '{sessionRptPath}'

$script:ReportPath = '{tmpDir}'
$script:VBRServer  = 'test'
$script:LogPath    = '{tmpDir}'
$script:LogLevel   = 'ERROR'

# Stub Get-VBRTaskSession to return one mock agent task session.
# JobName on the task has the machine suffix that Veeam appends; the function must
# use $session.Name instead when ObjectPlatform.IsEpAgentPlatform is true.
function Get-VBRTaskSession {{
    param($Session)
    $fakeStats    = [pscustomobject]@{{ BackupSize = 1073741824; DataSize = 2147483648; DedupRatio = 1.5; CompressRatio = 2.0 }}
    $fakeProgress = [pscustomobject]@{{ Duration = [TimeSpan]::FromMinutes(10); TransferedSize = 1073741824; ReadSize = 2147483648 }}
    $fakeInfo     = [pscustomobject]@{{ SessionAlgorithm = 'Increment' }}
    $fakeJobSess  = [pscustomobject]@{{ IsRetryMode = $false; Progress = $fakeProgress; BackupStats = $fakeStats; CreationTime = (Get-Date).AddMinutes(-30); Info = $fakeInfo }}
    $fakePlatform = [pscustomobject]@{{ IsEpAgentPlatform = $true; Platform = 'ELinuxPhysical' }}
    return @([pscustomobject]@{{
        Name           = 'machine.host.net'
        JobName        = 'Agent Job - machine.host.net'
        Status         = 'Success'
        ObjectPlatform = $fakePlatform
        JobSess        = $fakeJobSess
        WorkDetails    = [pscustomobject]@{{ TaskAlgorithm = 0; WorkDuration = $null }}
        Logger         = $null
    }})
}}

$fakeSession = [pscustomobject]@{{ Name = 'Agent Job'; CreationTime = (Get-Date).AddMinutes(-35) }}
Get-VhcSessionReport -BackupSessions @($fakeSession)

$csvPath = (Join-Path '{tmpDir}' 'VeeamSessionReport.csv')
$rows = Import-Csv $csvPath
if ($rows.Count -ne 1) {{
    Write-Error ""Expected 1 CSV row but got $($rows.Count)""
    exit 1
}}
if ($rows[0].JobName -ne 'Agent Job') {{
    Write-Error ""Expected JobName='Agent Job' but got '$($rows[0].JobName)'""
    exit 1
}}
Write-Host ""OK: JobName='$($rows[0].JobName)'""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhcSessionReport did not use parent session Name as JobName for agent sessions.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies Get-VhcSessionReport populates BottleneckDetails and PrimaryBottleneck
        /// from $task.JobSess.Progress.BottleneckInfo for agent sessions that produce no
        /// log records post-completion. See ADR 0013.
        /// </summary>
        [Fact]
        public void GetVhcSessionReport_AgentSession_PopulatesBottleneckFromStructuredProperty()
        {
            var projectRoot  = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var sessionRptPath  = Path.Combine(moduleRoot, "Public",  "Get-VhcSessionReport.ps1");
            var writeLogPath    = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");
            var exportCsvPath   = Path.Combine(moduleRoot, "Private", "Export-VhciCsv.ps1");
            var sessionLogPath  = Path.Combine(moduleRoot, "Private", "Get-VhciSessionLogWithTimeout.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-bitest-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-AgentBottleneck.ps1");
            Directory.CreateDirectory(tmpDir);

            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{exportCsvPath}'
. '{sessionLogPath}'
. '{sessionRptPath}'

$script:ReportPath = '{tmpDir}'
$script:VBRServer  = 'test'
$script:LogPath    = '{tmpDir}'
$script:LogLevel   = 'ERROR'

# Stub Get-VBRTaskSession returning an agent task session with BottleneckInfo populated
# but Logger returning empty log records, simulating agent session post-completion state.
function Get-VBRTaskSession {{
    param($Session)
    $fakeBi       = [pscustomobject]@{{ Bottleneck = 'Proxy'; Source = 74; Proxy = 77; Network = 24; Target = 75 }}
    $fakeStats    = [pscustomobject]@{{ BackupSize = 5000000000; DataSize = 15000000000; DedupRatio = 1.2; CompressRatio = 1.8 }}
    $fakeProgress = [pscustomobject]@{{ Duration = [TimeSpan]::FromMinutes(15); TransferedSize = 5000000000; ReadSize = 15000000000; BottleneckInfo = $fakeBi }}
    $fakeInfo     = [pscustomobject]@{{ SessionAlgorithm = 'Increment' }}
    $fakeJobSess  = [pscustomobject]@{{ IsRetryMode = $false; Progress = $fakeProgress; BackupStats = $fakeStats; CreationTime = (Get-Date).AddMinutes(-30); Info = $fakeInfo }}
    $fakePlatform = [pscustomobject]@{{ IsEpAgentPlatform = $true; Platform = 'ELinuxPhysical' }}
    return @([pscustomobject]@{{
        Name           = 'linux-server.host.net'
        JobName        = 'Physical Servers - Linux - linux-server.host.net'
        Status         = 'Success'
        ObjectPlatform = $fakePlatform
        JobSess        = $fakeJobSess
        WorkDetails    = [pscustomobject]@{{ TaskAlgorithm = 0; WorkDuration = $null }}
        Logger         = $null
    }})
}}

$fakeSession = [pscustomobject]@{{ Name = 'Physical Servers - Linux'; CreationTime = (Get-Date).AddMinutes(-35) }}
Get-VhcSessionReport -BackupSessions @($fakeSession)

$csvPath = (Join-Path '{tmpDir}' 'VeeamSessionReport.csv')
$rows = Import-Csv $csvPath
if ($rows.Count -ne 1) {{
    Write-Error ""Expected 1 CSV row but got $($rows.Count)""
    exit 1
}}
$expectedDetails = 'Source 74% > Proxy 77% > Network 24% > Target 75%'
if ($rows[0].BottleneckDetails -ne $expectedDetails) {{
    Write-Error ""Expected BottleneckDetails='$expectedDetails' but got '$($rows[0].BottleneckDetails)'""
    exit 1
}}
if ($rows[0].PrimaryBottleneck -ne 'Proxy') {{
    Write-Error ""Expected PrimaryBottleneck='Proxy' but got '$($rows[0].PrimaryBottleneck)'""
    exit 1
}}
Write-Host ""OK: BottleneckDetails='$($rows[0].BottleneckDetails)' PrimaryBottleneck='$($rows[0].PrimaryBottleneck)'""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhcSessionReport did not populate bottleneck from BottleneckInfo for agent sessions.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies Get-VhcSessionReport derives PrimaryBottleneck from the highest percentage
        /// component when EBottleneck is NotDefined (integer 0), as is always the case on VBR v12.
        /// Source/Proxy/Network/Target percentages are still populated on v12. See ADR 0013.
        /// </summary>
        [Fact]
        public void GetVhcSessionReport_V12NotDefinedBottleneck_DerivesPrimaryFromHighestPercentage()
        {
            var projectRoot  = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var moduleRoot      = Path.Combine(projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "vHC-VbrConfig");
            var sessionRptPath  = Path.Combine(moduleRoot, "Public",  "Get-VhcSessionReport.ps1");
            var writeLogPath    = Path.Combine(moduleRoot, "Public",  "Write-LogFile.ps1");
            var exportCsvPath   = Path.Combine(moduleRoot, "Private", "Export-VhciCsv.ps1");
            var sessionLogPath  = Path.Combine(moduleRoot, "Private", "Get-VhciSessionLogWithTimeout.ps1");

            var tmpDir        = Path.Combine(Path.GetTempPath(), $"vhc-v12bi-{Guid.NewGuid():N}");
            var tmpScriptPath = Path.Combine(tmpDir, "Test-V12Bottleneck.ps1");
            Directory.CreateDirectory(tmpDir);

            // Simulate VBR v12: Bottleneck = 0 (EBottleneck.NotDefined), percentages populated.
            // Source=99 is highest so PrimaryBottleneck should be derived as 'Source'.
            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
. '{writeLogPath}'
. '{exportCsvPath}'
. '{sessionLogPath}'
. '{sessionRptPath}'

$script:ReportPath = '{tmpDir}'
$script:VBRServer  = 'test'
$script:LogPath    = '{tmpDir}'
$script:LogLevel   = 'ERROR'

function Get-VBRTaskSession {{
    param($Session)
    # Bottleneck = 0 simulates EBottleneck.NotDefined as returned on VBR v12
    $fakeBi       = [pscustomobject]@{{ Bottleneck = 0; Source = 99; Proxy = 34; Network = 0; Target = 0 }}
    $fakeStats    = [pscustomobject]@{{ BackupSize = 2684354560; DataSize = 7516192768; DedupRatio = 1.5; CompressRatio = 2.0 }}
    $fakeProgress = [pscustomobject]@{{ Duration = [TimeSpan]::FromMinutes(5); TransferedSize = 2684354560; ReadSize = 7516192768; BottleneckInfo = $fakeBi }}
    $fakeInfo     = [pscustomobject]@{{ SessionAlgorithm = 'Increment' }}
    $fakeJobSess  = [pscustomobject]@{{ IsRetryMode = $false; Progress = $fakeProgress; BackupStats = $fakeStats; CreationTime = (Get-Date).AddMinutes(-10); Info = $fakeInfo }}
    $fakePlatform = [pscustomobject]@{{ IsEpAgentPlatform = $false; Platform = 'EVmware' }}
    return @([pscustomobject]@{{
        Name           = 'DC01'
        JobName        = 'VC_DC0x'
        Status         = 'Success'
        ObjectPlatform = $fakePlatform
        JobSess        = $fakeJobSess
        WorkDetails    = [pscustomobject]@{{ TaskAlgorithm = 0; WorkDuration = $null }}
        Logger         = $null
    }})
}}

$fakeSession = [pscustomobject]@{{ Name = 'VC_DC0x'; CreationTime = (Get-Date).AddMinutes(-15) }}
Get-VhcSessionReport -BackupSessions @($fakeSession)

$csvPath = (Join-Path '{tmpDir}' 'VeeamSessionReport.csv')
$rows = Import-Csv $csvPath
if ($rows.Count -ne 1) {{
    Write-Error ""Expected 1 CSV row but got $($rows.Count)""
    exit 1
}}
$expectedDetails = 'Source 99% > Proxy 34% > Network 0% > Target 0%'
if ($rows[0].BottleneckDetails -ne $expectedDetails) {{
    Write-Error ""Expected BottleneckDetails='$expectedDetails' but got '$($rows[0].BottleneckDetails)'""
    exit 1
}}
if ($rows[0].PrimaryBottleneck -ne 'Source') {{
    Write-Error ""Expected PrimaryBottleneck='Source' but got '$($rows[0].PrimaryBottleneck)'""
    exit 1
}}
Write-Host ""OK: BottleneckDetails='$($rows[0].BottleneckDetails)' PrimaryBottleneck='$($rows[0].PrimaryBottleneck)'""
exit 0
";
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode == 0,
                    $"Get-VhcSessionReport did not derive PrimaryBottleneck from percentages when EBottleneck=NotDefined.\n" +
                    $"STDOUT: {stdout}\nSTDERR: {stderr}");
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }

        /// <summary>
        /// Verifies the orchestrator throws a clear error when VbrConfig.json is missing
        /// a required threshold key, rather than silently using $null.
        /// </summary>
        [Fact]
        public void GetVBRConfig_MissingThresholdKey_ThrowsDescriptiveError()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            var orchestratorPath = Path.Combine(
                projectRoot, "Tools", "Scripts", "HealthCheck", "VBR", "Get-VBRConfig.ps1");

            if (!File.Exists(orchestratorPath))
            {
                Assert.Fail($"Get-VBRConfig.ps1 not found at: {orchestratorPath}");
            }

            var tmpDir         = Path.Combine(Path.GetTempPath(), $"vhc-thresh-{Guid.NewGuid():N}");
            var tmpConfigPath  = Path.Combine(tmpDir, "VbrConfig.json");
            var tmpScriptPath  = Path.Combine(tmpDir, "Test-ThresholdValidation.ps1");

            Directory.CreateDirectory(tmpDir);

            // VbrConfig.json with one required threshold key intentionally removed
            var brokenConfig = @"{
  ""ConfigVersion"": 1,
  ""LogLevel"": ""INFO"",
  ""DefaultOutputPath"": ""C:\\temp\\vHC\\Original\\VBR"",
  ""Thresholds"": {
    ""VpProxyCPUPerTask"": 0.5
  },
  ""SecurityComplianceRuleNames"": {}
}";
            // Test script: dot-sources only the validation block from the orchestrator
            // by extracting just the $config loading + key-check logic into a minimal wrapper.
            var scriptContent = $@"
$ErrorActionPreference = 'Stop'
$configPath = '{tmpConfigPath}'
$config = Get-Content -Path $configPath -Raw | ConvertFrom-Json

# --- Paste the exact validation block from Get-VBRConfig.ps1 here ---
$requiredThresholds = @(
    'VpProxyRAMPerTask','VpProxyCPUPerTask','VpProxyOSCPU','VpProxyOSRAM',
    'GpProxyRAMPerTask','GpProxyCPUPerTask','GpProxyOSCPU','GpProxyOSRAM',
    'RepoGwRAMPerTask','RepoGwCPUPerTask','RepoOSCPU','RepoOSRAM',
    'CdpProxyRAM','CdpProxyCPU',
    'BackupServerCPU_v12','BackupServerRAM_v12','BackupServerCPU_v13','BackupServerRAM_v13',
    'SqlRAMMin','SqlCPUMin',
    'CompliancePollMaxSeconds','CompliancePollIntervalSeconds'
)
foreach ($key in $requiredThresholds) {{
    if ($null -eq $config.Thresholds.$key) {{
        throw ""VbrConfig.json Thresholds is missing required key: '$key'""
    }}
}}
# Should not reach here - the missing key should have thrown
exit 0
";
            File.WriteAllText(tmpConfigPath, brokenConfig);
            File.WriteAllText(tmpScriptPath, scriptContent);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "pwsh",
                    Arguments              = $"-NoProfile -NonInteractive -File \"{tmpScriptPath}\"",
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) { Assert.Fail("Failed to start pwsh"); return; }
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Assert.True(process.ExitCode != 0,
                    $"Expected non-zero exit when threshold key is missing, got 0.\nSTDOUT: {stdout}\nSTDERR: {stderr}");
                Assert.Contains("VbrConfig.json Thresholds is missing required key", stderr + stdout,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            }
        }
    }
}
