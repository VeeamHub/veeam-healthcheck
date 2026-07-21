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
    /// never be answered and the whole collection hangs forever — exactly the symptom in
    /// the reported screenshot (frozen at "Local VBR detected, using Windows authentication").
    ///
    /// The remote path (TestMfa.ps1) already passed -ForceAcceptTlsCertificate; the local
    /// path (CCollections.RunLocalMfaCheckNoCredentials) was missing it. These tests trap
    /// that class of bug at the script-construction seam so it cannot silently regress.
    /// </summary>
    [Trait("Category", "Regression")]
    public class LocalMfaConnectScriptTests
    {
        [Fact]
        public void BuildLocalMfaConnectScript_ForcesTlsCertificateAcceptance_Issue149()
        {
            string script = CCollections.BuildLocalMfaConnectScript();

            // Without this flag VBR v13 prompts to accept the server certificate and the
            // headless collection hangs forever (issue #149). This is the core regression guard.
            Assert.Contains("-ForceAcceptTlsCertificate", script);
        }

        [Fact]
        public void BuildLocalMfaConnectScript_StopsOnError()
        {
            string script = CCollections.BuildLocalMfaConnectScript();

            // -ErrorAction Stop makes a failed connect surface as a non-zero exit code
            // rather than a silent "success" the caller would misread.
            Assert.Contains("-ErrorAction Stop", script);
        }

        [Fact]
        public void BuildLocalMfaConnectScript_ConnectsToLocalhostViaVeeamModule()
        {
            string script = CCollections.BuildLocalMfaConnectScript();

            Assert.Contains("Import-Module Veeam.Backup.PowerShell", script);
            Assert.Contains("Connect-VBRServer", script);
            Assert.Contains("-Server localhost", script);
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
