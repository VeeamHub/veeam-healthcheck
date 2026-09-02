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
    /// server certificate raises an interactive trust prompt that hangs forever. The fix adds
    /// -ForceAcceptTlsCertificate for v13+ only — the parameter does not exist on v12, where
    /// passing it throws NamedParameterNotFound and breaks the connect.
    /// </summary>
    [Trait("Category", "Regression")]
    public class RemoteMfaConnectArgsTests
    {
        [Theory]
        [InlineData(13)]
        [InlineData(14)]
        public void BuildRemoteMfaConnectArgs_V13Plus_ForcesTlsCertificateAcceptance_Issue149(int major)
        {
            string args = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", "pw", major);
            Assert.Contains("-ForceAcceptTlsCertificate", args);
        }

        [Theory]
        [InlineData(12)]
        [InlineData(0)]
        public void BuildRemoteMfaConnectArgs_V12OrUnknown_OmitsTlsFlag_NoParamNotFound(int major)
        {
            string args = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", "pw", major);
            Assert.DoesNotContain("-ForceAcceptTlsCertificate", args);
        }

        [Theory]
        [InlineData(12)]
        [InlineData(13)]
        public void BuildRemoteMfaConnectArgs_AlwaysStopsOnErrorAndSingleQuotesFields(int major)
        {
            // -ErrorAction Stop surfaces failures as a non-zero exit; the three operator-supplied
            // fields stay inside single quotes (escaping is CredentialHelper's job upstream) so a
            // refactor cannot silently drop the quoting and reopen an injection vector.
            string args = PSInvoker.BuildRemoteMfaConnectArgs("srv", "user", "pw", major);
            Assert.Contains("-ErrorAction Stop", args);
            Assert.Contains("-Server 'srv'", args);
            Assert.Contains("-User 'user'", args);
            Assert.Contains("-Password 'pw'", args);
        }
    }
}
