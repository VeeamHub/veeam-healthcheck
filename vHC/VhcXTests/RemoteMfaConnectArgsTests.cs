// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for the remote / PS5-failover VBR MFA connect command line
    /// (PSInvoker.BuildRemoteMfaConnectArgs). Companion to LocalMfaConnectScriptTests.
    ///
    /// Issue #149: the MFA check runs Connect-VBRServer headless; on VBR v13 an untrusted
    /// server certificate raises an interactive trust prompt that hangs forever. The flag
    /// was missing from this credentialed path before the fix, so these guards trap its
    /// removal (RED against the pre-fix inline string, GREEN after).
    /// </summary>
    [Trait("Category", "Regression")]
    public class RemoteMfaConnectArgsTests
    {
        [Fact]
        public void BuildRemoteMfaConnectArgs_ForcesTlsCertificateAcceptance_Issue149()
        {
            string args = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", "pw");
            Assert.Contains("-ForceAcceptTlsCertificate", args);
        }

        [Fact]
        public void BuildRemoteMfaConnectArgs_StopsOnError_SoFailureSurfacesAsNonZeroExit()
        {
            string args = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", "pw");
            Assert.Contains("-ErrorAction Stop", args);
        }

        [Fact]
        public void BuildRemoteMfaConnectArgs_KeepsServerUserPasswordSingleQuoted()
        {
            // The three operator-supplied fields must remain inside single quotes — escaping is
            // CredentialHelper's job upstream; this pins the quoting context so a future refactor
            // can't silently drop it and reopen an argument-injection vector.
            string args = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", "pw");
            Assert.Contains("-Server 'srv'", args);
            Assert.Contains("-User 'user'", args);
            Assert.Contains("-Password 'pw'", args);
        }
    }
}
