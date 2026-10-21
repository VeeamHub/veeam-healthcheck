// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// A credentialed vHC run must never put the password in a child-process command line (visible
    /// to other local processes, and recorded by process-creation auditing: event 4688 / Sysmon 1).
    /// Every credentialed launch path is built here and checked two ways: the argument string holds
    /// neither the password nor its Base64 form, and the password arrives in the child's own
    /// environment (PsCredentialChannel). The Unix-only tests then launch a real child process via a
    /// shell-script stand-in for PowerShell and read back what that process actually received.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    [Collection("GlobalState")]
    [Trait("Category", "Security")]
    public class PasswordNotOnCommandLineTests : IDisposable
    {
        // Contains characters that would need escaping on a command line, so a leak cannot hide
        // behind quoting: single and double quotes, backslash, dollar, backtick.
        private const string Password = "Zq!7'p\"w\\$`Xk9-secret";
        private const string User = "DOMAIN\\svc-vhc";
        private const string Host = "vbr-pw-test.lab";

        private static readonly string PasswordBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Password));

        private readonly IsolatedCredentialStore _store;
        private readonly string _origRemoteHost;
        private readonly bool _origRemoteExec;
        private readonly int _origVbrVersion;
        private readonly string _origPath;
        private readonly string _origStubOut;
        private readonly string _stubDir;
        private readonly string _stubOut;

        public PasswordNotOnCommandLineTests()
        {
            _origRemoteHost = CGlobals.REMOTEHOST;
            _origRemoteExec = CGlobals.REMOTEEXEC;
            _origVbrVersion = CGlobals.VBRMAJORVERSION;
            _origPath = Environment.GetEnvironmentVariable("PATH");
            _origStubOut = Environment.GetEnvironmentVariable("VHC_STUB_OUT");

            _store = new IsolatedCredentialStore();
            CredentialStore.SetTransient(Host, User, Password);
            CGlobals.REMOTEHOST = Host;
            CGlobals.REMOTEEXEC = true;
            CGlobals.VBRMAJORVERSION = 13;

            // Directory on PATH holding stand-ins for pwsh.exe / powershell.exe, so PSInvoker finds
            // "PowerShell" on any OS. On Unix they are real executable scripts that record what
            // they were started with; on Windows they only need to exist (builders do not run them).
            _stubDir = Path.Combine(Path.GetTempPath(), $"vhc-pw-stub-{Guid.NewGuid()}");
            Directory.CreateDirectory(_stubDir);
            _stubOut = Path.Combine(_stubDir, "received.txt");
            foreach (string exe in new[] { "pwsh.exe", "powershell.exe" })
            {
                string path = Path.Combine(_stubDir, exe);
                File.WriteAllText(
                    path,
                    "#!/bin/sh\n" +
                    "{ printf 'EXE=%s\\n' \"$0\"; printf 'ARGS=%s\\n' \"$*\"; printf 'PW=%s\\n' \"$VHC_CRED_PASSWORD\"; } >> \"$VHC_STUB_OUT\"\n" +
                    "exit 0\n");
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            Environment.SetEnvironmentVariable("PATH", _stubDir + Path.PathSeparator + _origPath);
            Environment.SetEnvironmentVariable("VHC_STUB_OUT", _stubOut);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PATH", _origPath);
            Environment.SetEnvironmentVariable("VHC_STUB_OUT", _origStubOut);
            CGlobals.REMOTEHOST = _origRemoteHost;
            CGlobals.REMOTEEXEC = _origRemoteExec;
            CGlobals.VBRMAJORVERSION = _origVbrVersion;
            _store.Dispose();
            try { Directory.Delete(_stubDir, recursive: true); } catch { /* best effort */ }
        }

        private static void AssertSecretOnlyInEnvironment(ProcessStartInfo startInfo)
        {
            string args = startInfo.Arguments ?? string.Empty;
            Assert.DoesNotContain(Password, args);
            Assert.DoesNotContain(PasswordBase64, args);
            Assert.DoesNotContain("-PasswordBase64", args);
            Assert.DoesNotContain("-Password ", args.Replace("-Password $vhcPw", string.Empty));
            Assert.False(startInfo.UseShellExecute);
            Assert.Equal(Password, startInfo.Environment[PsCredentialChannel.PasswordVariable]);
        }

        // ----------------------------------------------------------------
        // Builder level: runs on every OS
        // ----------------------------------------------------------------

        [Fact]
        public void VbrConfigStartInfo_RemoteRun_PasswordInEnvironmentNotArguments()
        {
            var startInfo = new PSInvoker().VbrConfigStartInfo();

            Assert.Contains("-User", startInfo.Arguments);
            AssertSecretOnlyInEnvironment(startInfo);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ConfigStartInfo_RemoteRun_PasswordInEnvironmentNotArguments(int days)
        {
            var startInfo = new PSInvoker().ConfigStartInfo("nas.ps1", days, Path.GetTempPath());

            Assert.Contains("-User", startInfo.Arguments);
            AssertSecretOnlyInEnvironment(startInfo);
        }

        [Fact]
        public void BuildVb365StartInfo_RemoteRun_PasswordInEnvironmentNotArguments()
        {
            var startInfo = new PSInvoker().BuildVb365StartInfo();

            Assert.Contains("-Username", startInfo.Arguments);
            AssertSecretOnlyInEnvironment(startInfo);
        }

        [Fact]
        public void BuildRemoteMfaStartInfo_PasswordInEnvironmentNotArguments()
        {
            var startInfo = PSInvoker.BuildRemoteMfaStartInfo(Host, User, Password, 13);

            Assert.Contains("-Password $vhcPw", startInfo.Arguments);
            AssertSecretOnlyInEnvironment(startInfo);
        }

        [Fact]
        public void BuildRemoteMfaScriptStartInfo_PasswordInEnvironmentNotArguments()
        {
            var startInfo = CCollections.BuildRemoteMfaScriptStartInfo("pwsh.exe", "TestMfa.ps1", Host, User, Password, 13);

            Assert.Contains("-Username", startInfo.Arguments);
            AssertSecretOnlyInEnvironment(startInfo);
        }

        [Fact]
        public void Vb365RemoteMfa_ScriptTextAndStartInfo_PasswordInEnvironmentNotArguments()
        {
            string args = PSInvoker.BuildVb365RemoteMfaArgs(User, Host);
            var startInfo = new PSInvoker().CreatePsStartInfo(
                args, useShellExecute: false, createNoWindow: true, redirectStdErr: true,
                version: PowerShellVersion.PowerShell7, password: Password);

            Assert.Contains("Connect-VBOServer", startInfo.Arguments);
            AssertSecretOnlyInEnvironment(startInfo);
        }

        [Fact]
        public void InlineMfaScripts_ReadThenClearPasswordVariable()
        {
            // The inline -Command scripts must remove the variable from the child environment
            // straight after reading it, so nothing the child starts later inherits it.
            string vbr = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", 13);
            string vb365 = PSInvoker.BuildVb365RemoteMfaArgs("user", "srv");
            foreach (string script in new[] { vbr, vb365 })
            {
                int read = script.IndexOf("GetEnvironmentVariable('VHC_CRED_PASSWORD'", StringComparison.Ordinal);
                int clear = script.IndexOf("SetEnvironmentVariable('VHC_CRED_PASSWORD', $null", StringComparison.Ordinal);
                Assert.True(read >= 0, "script does not read VHC_CRED_PASSWORD");
                Assert.True(clear > read, "script does not clear VHC_CRED_PASSWORD after reading it");
            }
        }

        [Fact]
        public void SetPassword_ShellExecute_Throws()
        {
            var startInfo = new ProcessStartInfo("x") { UseShellExecute = true };
            Assert.Throws<InvalidOperationException>(() => PsCredentialChannel.SetPassword(startInfo, Password));
        }

        [Fact]
        public void SetPassword_DoesNotTouchOwnProcessEnvironment()
        {
            Environment.SetEnvironmentVariable(PsCredentialChannel.PasswordVariable, null);
            PsCredentialChannel.SetPassword(new ProcessStartInfo("x"), Password);
            Assert.Null(Environment.GetEnvironmentVariable(PsCredentialChannel.PasswordVariable));
        }

        // ----------------------------------------------------------------
        // Process level: a real child is started and we read what it received
        // ----------------------------------------------------------------

        private Dictionary<string, string> ReadChildReport()
        {
            Assert.True(File.Exists(_stubOut), "the stand-in PowerShell was never started");
            var report = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(_stubOut))
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    report[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }

            return report;
        }

        private void AssertChildGotSecretOnlyByEnvironment()
        {
            string raw = File.ReadAllText(_stubOut);
            var report = ReadChildReport();

            Assert.Equal(Password, report["PW"]);
            Assert.DoesNotContain(Password, report["ARGS"]);
            Assert.DoesNotContain(PasswordBase64, report["ARGS"]);
            Assert.DoesNotContain("-PasswordBase64", report["ARGS"]);

            // ARGS is the only line that mirrors the command line: nothing else may leak it there.
            Assert.DoesNotContain(PasswordBase64, raw);
        }

        [UnixOnlyFact]
        public void RunVbrConfigCollect_RealChild_ReceivesPasswordOnlyByEnvironment()
        {
            Assert.True(new PSInvoker().RunVbrConfigCollect());
            AssertChildGotSecretOnlyByEnvironment();
        }

        [UnixOnlyFact]
        public void InvokeVb365Collect_RealChild_ReceivesPasswordOnlyByEnvironment()
        {
            Assert.True(new PSInvoker().InvokeVb365Collect());
            AssertChildGotSecretOnlyByEnvironment();
        }

        [UnixOnlyFact]
        public void TestMfaVB365_RealChild_ReceivesPasswordOnlyByEnvironment()
        {
            Assert.True(new PSInvoker().TestMfaVB365());
            AssertChildGotSecretOnlyByEnvironment();
        }

        [UnixOnlyFact]
        public void TestMfa_RealChild_ReceivesPasswordOnlyByEnvironment()
        {
            new PSInvoker().TestMfa();
            AssertChildGotSecretOnlyByEnvironment();
        }
    }
}
