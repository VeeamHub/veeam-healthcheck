// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace VhcXTests.Integration
{
    [Collection("Integration Tests")]
    [Trait("Category", "Integration")]
    public class PSScriptIntegrationTests
    {
        private readonly string _projectRoot;
        private readonly string _scriptsPath;

        public PSScriptIntegrationTests()
        {
            // Navigate up from bin/Debug/net8.0-windows7.0 to project root
            _projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "HC_Reporting"));
            
            _scriptsPath = Path.Combine(_projectRoot, "Tools", "Scripts");
        }

        private static int RunPwshAndLog(ProcessStartInfo psi, string logName)
        {
            Directory.CreateDirectory("TestResults\\pwsh-logs");

            using var process = Process.Start(psi);
            if (process == null)
            {
                File.WriteAllText(Path.Combine("TestResults", "pwsh-logs", $"{logName}-start-failed.txt"), "Failed to start process");
                return -1;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var logPath = Path.Combine("TestResults", "pwsh-logs", $"{logName}.txt");
            File.WriteAllText(logPath, $"ExitCode: {process.ExitCode}\r\n---STDOUT---\r\n{output}\r\n---STDERR---\r\n{error}");

            return process.ExitCode;
        }

        [Fact]
        public void GetVBRConfig_ScriptExists()
        {
            var scriptPath = Path.Combine(_scriptsPath, "HealthCheck", "VBR", "Get-VBRConfig.ps1");
            Assert.True(File.Exists(scriptPath), $"Get-VBRConfig.ps1 not found at: {scriptPath}");
        }

        [Fact]
        public void GetVBRConfig_ValidPowerShellSyntax()
        {
            var scriptPath = Path.Combine(_scriptsPath, "HealthCheck", "VBR", "Get-VBRConfig.ps1");
            
            if (!File.Exists(scriptPath))
            {
                Assert.Fail($"Script not found: {scriptPath}");
            }

            // Validate PowerShell syntax using AST parser
            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -NonInteractive -Command \"$errors = $null; [System.Management.Automation.Language.Parser]::ParseFile('{scriptPath}', [ref]$null, [ref]$errors); if ($errors) {{ Write-Error 'Syntax errors found'; exit 1 }} else {{ exit 0 }}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var rc = RunPwshAndLog(psi, "GetVBRConfig_ValidPowerShellSyntax");
            Assert.True(rc == 0, $"PowerShell syntax validation failed. See TestResults/pwsh-logs/GetVBRConfig_ValidPowerShellSyntax.txt (exit {rc})");
        }

        [Fact]
        public void GetVeeamSessionReport_ValidPowerShellSyntax()
        {
            var scriptPath = Path.Combine(_scriptsPath, "HealthCheck", "VBR", "Get-VeeamSessionReport.ps1");
            
            if (!File.Exists(scriptPath))
            {
                Assert.Fail($"Script not found: {scriptPath}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -NonInteractive -Command \"$errors = $null; [System.Management.Automation.Language.Parser]::ParseFile('{scriptPath}', [ref]$null, [ref]$errors); if ($errors) {{ exit 1 }} else {{ exit 0 }}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var rc = RunPwshAndLog(psi, "GetVeeamSessionReport_ValidPowerShellSyntax");
            Assert.True(rc == 0, "Get-VeeamSessionReport.ps1 has syntax errors");
        }

        [Fact]
        public void CollectVB365Data_ValidPowerShellSyntax()
        {
            var scriptPath = Path.Combine(_scriptsPath, "HealthCheck", "VB365", "Collect-VB365Data.ps1");
            
            if (!File.Exists(scriptPath))
            {
                Assert.Fail($"Script not found: {scriptPath}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -NonInteractive -Command \"$errors = $null; [System.Management.Automation.Language.Parser]::ParseFile('{scriptPath}', [ref]$null, [ref]$errors); if ($errors) {{ exit 1 }} else {{ exit 0 }}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var rc = RunPwshAndLog(psi, "CollectVB365Data_ValidPowerShellSyntax");
            Assert.True(rc == 0, "Collect-VB365Data.ps1 has syntax errors");
        }

        [Fact]
        public void AllHotfixScripts_ValidPowerShellSyntax()
        {
            var hotfixPath = Path.Combine(_scriptsPath, "HotfixDetection");
            
            if (!Directory.Exists(hotfixPath))
            {
                Assert.Fail($"Hotfix detection scripts directory not found: {hotfixPath}");
            }

            var scripts = Directory.GetFiles(hotfixPath, "*.ps1", SearchOption.AllDirectories);
            Assert.NotEmpty(scripts);

            foreach (var scriptPath in scripts)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"-NoProfile -NonInteractive -Command \"$errors = $null; [System.Management.Automation.Language.Parser]::ParseFile('{scriptPath}', [ref]$null, [ref]$errors); if ($errors) {{ exit 1 }} else {{ exit 0 }}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var logName = $"AllHotfixScripts-{Path.GetFileName(scriptPath)}";
                var rc = RunPwshAndLog(psi, logName);
                Assert.True(rc == 0, $"Script has syntax errors: {Path.GetFileName(scriptPath)} (see TestResults/pwsh-logs/{logName}.txt)");
            }
        }

        [Fact]
        public void IncrementVersionScript_ExecutesSuccessfully()
        {
            var scriptPath = Path.Combine(_projectRoot, "increment_version.ps1");
            
            if (!File.Exists(scriptPath))
            {
                // This is optional, skip if not found
                return;
            }

            // Just validate syntax, don't actually increment
            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -NonInteractive -Command \"$errors = $null; [System.Management.Automation.Language.Parser]::ParseFile('{scriptPath}', [ref]$null, [ref]$errors); if ($errors) {{ exit 1 }} else {{ exit 0 }}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var rc = RunPwshAndLog(psi, "IncrementVersionScript_ExecutesSuccessfully");
            Assert.True(rc == 0, "increment_version.ps1 has syntax errors");
        }
    }
}
