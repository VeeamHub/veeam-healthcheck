// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using VeeamHealthCheck.Scrubber;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Security tests for the scrub-mode hardening (A6 Phase 2.4):
    /// private-IPv4 final pass, registered-value final pass, and the
    /// key-file ACL lockdown. These exercise the internal static seams
    /// directly so no live report or global state is required.
    /// </summary>
    [Trait("Category", "Scrubbing")]
    public class CScrubHandlerSecurityTests
    {
        // ---- ScrubRawPrivateIPv4 ----------------------------------------

        [Theory]
        [InlineData("10.1.2.3")]
        [InlineData("10.255.255.255")]
        [InlineData("192.168.0.1")]
        [InlineData("172.16.5.5")]
        [InlineData("172.31.255.254")]
        [InlineData("127.0.0.1")]
        [InlineData("169.254.10.10")]
        public void ScrubRawPrivateIPv4_PrivateAddress_IsReplaced(string ip)
        {
            Assert.Equal("PRIVATE_IP", CScrubHandler.ScrubRawPrivateIPv4(ip));
        }

        [Theory]
        [InlineData("8.8.8.8")]            // public DNS
        [InlineData("172.15.0.1")]         // just below the 172.16/12 block
        [InlineData("172.32.0.1")]         // just above the 172.16/12 block
        [InlineData("13.0.2.29")]          // product build version — must NOT be scrubbed
        [InlineData("10.999.0.1")]         // invalid octet, not a real IP
        public void ScrubRawPrivateIPv4_NonPrivateOrInvalid_IsUnchanged(string value)
        {
            Assert.Equal(value, CScrubHandler.ScrubRawPrivateIPv4(value));
        }

        [Fact]
        public void ScrubRawPrivateIPv4_EmbeddedInText_ReplacesOnlyTheAddress()
        {
            Assert.Equal(
                "job failed on PRIVATE_IP at 02:00",
                CScrubHandler.ScrubRawPrivateIPv4("job failed on 10.0.0.5 at 02:00"));
        }

        // ---- ReplaceRegisteredValues ------------------------------------

        [Fact]
        public void ReplaceRegisteredValues_EmbeddedHostname_IsReplaced()
        {
            var map = new Dictionary<string, string> { ["srv01host"] = "Server_0" };
            Assert.Equal(
                @"\\Server_0\share",
                CScrubHandler.ReplaceRegisteredValues(@"\\srv01host\share", map));
        }

        [Fact]
        public void ReplaceRegisteredValues_LongestFirst_AvoidsPartialMatch()
        {
            // Both keys present; the longer must win so we don't get "X-vbr-01".
            var map = new Dictionary<string, string>
            {
                ["prodbox"]         = "X",
                ["prodbox-vbr-01"]  = "Server_7",
            };
            Assert.Equal("Server_7", CScrubHandler.ReplaceRegisteredValues("prodbox-vbr-01", map));
        }

        [Fact]
        public void ReplaceRegisteredValues_ShortValue_IsSkippedToAvoidChromeCollision()
        {
            // 'ab' is < 4 chars; must not be replaced (would shred report HTML/CSS).
            var map = new Dictionary<string, string> { ["ab"] = "Z" };
            Assert.Equal("ab cd ab", CScrubHandler.ReplaceRegisteredValues("ab cd ab", map));
        }

        [Fact]
        public void ReplaceRegisteredValues_WholeWordOnly_DoesNotMatchSubstring()
        {
            var map = new Dictionary<string, string> { ["host1"] = "Server_0" };
            // "host10" should not be touched (not a whole-word match).
            Assert.Equal("host10", CScrubHandler.ReplaceRegisteredValues("host10", map));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReplaceRegisteredValues_EmptyText_IsPassedThrough(string? text)
        {
            var map = new Dictionary<string, string> { ["something"] = "Server_0" };
            Assert.Equal(text, CScrubHandler.ReplaceRegisteredValues(text, map));
        }

        [Fact]
        public void ReplaceRegisteredValues_EmptyMap_IsPassedThrough()
        {
            var map = new Dictionary<string, string>();
            Assert.Equal("nothing to do here", CScrubHandler.ReplaceRegisteredValues("nothing to do here", map));
        }

        // ---- RestrictFileToOwner (ACL lockdown) -------------------------

        [Fact]
        public void RestrictFileToOwner_RemovesBroadAccessAndBreaksInheritance()
        {
            string path = Path.Combine(Path.GetTempPath(), $"vhc-keyfile-acl-{System.Guid.NewGuid():N}.json");
            File.WriteAllText(path, "{\"original\":\"obfuscated\"}");

            try
            {
                CScrubHandler.RestrictFileToOwner(path);

                FileSecurity security = new FileInfo(path).GetAccessControl();
                Assert.True(security.AreAccessRulesProtected,
                    "Inheritance should be disabled so the world-readable temp ACLs do not apply.");

                foreach (FileSystemAccessRule rule in
                         security.GetAccessRules(true, true, typeof(NTAccount)))
                {
                    string id = rule.IdentityReference.Value;
                    Assert.DoesNotContain("Everyone", id, System.StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Authenticated Users", id, System.StringComparison.OrdinalIgnoreCase);
                    Assert.False(id.EndsWith(@"\Users", System.StringComparison.OrdinalIgnoreCase),
                        $"Built-in Users group must not retain access: {id}");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
