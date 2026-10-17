// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Collection;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for the local (Windows-auth) VBR MFA pre-check connect script.
    ///
    /// Issue #149: on VBR v13, an untrusted / self-signed server certificate makes
    /// Connect-VBRServer raise an interactive "accept this certificate?" prompt. The MFA
    /// pre-check runs PowerShell headless (CreateNoWindow, no stdin), so the prompt can
    /// never be answered and the whole collection hangs forever.
    ///
    /// The fix adds -ForceAcceptTlsCertificate — but ONLY for v13+, because that parameter
    /// does not exist on VBR v12's Connect-VBRServer (passing it there throws
    /// "A parameter cannot be found..." and breaks collection). These tests pin both halves
    /// of that contract: present on v13+, absent on v12.
    /// </summary>
    [Trait("Category", "Regression")]
    public class LocalMfaConnectScriptTests
    {
        [Theory]
        [InlineData(13)]
        [InlineData(14)]
        public void BuildLocalMfaConnectScript_V13Plus_ForcesTlsCertificateAcceptance_Issue149(int major)
        {
            string script = CCollections.BuildLocalMfaConnectScript(major);
            // Without this flag VBR v13 prompts to accept the server certificate and the
            // headless collection hangs forever (issue #149).
            Assert.Contains("-ForceAcceptTlsCertificate", script);
        }

        [Theory]
        [InlineData(12)]
        [InlineData(0)]
        public void BuildLocalMfaConnectScript_V12OrUnknown_OmitsTlsFlag_NoParamNotFound(int major)
        {
            string script = CCollections.BuildLocalMfaConnectScript(major);
            // -ForceAcceptTlsCertificate does not exist on v12's Connect-VBRServer; including it
            // throws NamedParameterNotFound and breaks collection. Must be absent below v13.
            Assert.DoesNotContain("-ForceAcceptTlsCertificate", script);
        }

        [Theory]
        [InlineData(12)]
        [InlineData(13)]
        public void BuildLocalMfaConnectScript_AlwaysConnectsLocalhostAndStopsOnError(int major)
        {
            string script = CCollections.BuildLocalMfaConnectScript(major);
            Assert.Contains("Import-Module Veeam.Backup.PowerShell", script);
            Assert.Contains("Connect-VBRServer", script);
            Assert.Contains("-Server localhost", script);
            Assert.Contains("-ErrorAction Stop", script);
        }

        [Fact]
        public void MfaCheckTimeoutSeconds_IsPositiveAndBounded()
        {
            // A positive, bounded ceiling is the defense-in-depth guarantee that the MFA
            // pre-check can never block collection indefinitely on ANY interactive prompt
            // (issue #149), not just the certificate case the flag above covers.
            Assert.InRange(CCollections.MfaCheckTimeoutSeconds, 1, 120);
        }
    }
}
