// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
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

        // ---- ReplaceRegisteredValuesInHtml (#263) ------------------------

        [Fact]
        public void ReplaceRegisteredValuesInHtml_RootRegistered_LeavesCssRootSelectorIntact()
        {
            var map = new Dictionary<string, string> { ["root"] = "Item_8" };
            string html = "<html><head><style>:root { --green: #00d15f; }</style></head><body><td>root</td></body></html>";

            string result = CScrubHandler.ReplaceRegisteredValuesInHtml(html, map);

            Assert.Contains(":root { --green: #00d15f; }", result);
            Assert.Contains("<td>Item_8</td>", result);
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_ReportCssAndScript_AreNotChangedByAnyShortWordValue()
        {
            // The real embedded stylesheet and script, with values a user could plausibly register
            // that are also CSS/JS words. None of them may alter either block.
            string css = CHtmlCompiler.GetEmbeddedCssContent("css.css");
            string js = CHtmlCompiler.GetEmbeddedCssContent("ReportScript.js");
            var map = new Dictionary<string, string>
            {
                ["root"] = "Item_0", ["none"] = "Item_1", ["auto"] = "Item_2", ["flex"] = "Item_3",
                ["grid"] = "Item_4", ["bold"] = "Item_5", ["block"] = "Item_6", ["table"] = "Item_7",
                ["function"] = "Item_8", ["document"] = "Item_9",
            };
            string html = "<html><head><style type=\"text/css\">" + css + "</style></head><body>x"
                + "<script type=\"text/javascript\">" + js + "</script></body></html>";

            Assert.Equal(html, CScrubHandler.ReplaceRegisteredValuesInHtml(html, map));
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_MarkupWordRegistered_LeavesTagsAndAttributeNamesIntact()
        {
            var map = new Dictionary<string, string> { ["body"] = "Item_1", ["span"] = "Item_2", ["title"] = "Item_3" };
            string html = "<body><span class=\"body\" id=\"span\" title=\"x\">body span</span></body>";

            string result = CScrubHandler.ReplaceRegisteredValuesInHtml(html, map);

            Assert.Equal("<body><span class=\"body\" id=\"span\" title=\"x\">Item_1 Item_2</span></body>", result);
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_HostnameInTextAndTitleAttribute_IsReplacedInBoth()
        {
            var map = new Dictionary<string, string> { ["srv01host"] = "Server_0" };
            string html = "<td title=\"\\\\srv01host\\share\">\\\\srv01host\\share</td><td title='srv01host'>ok</td>";

            string result = CScrubHandler.ReplaceRegisteredValuesInHtml(html, map);

            Assert.Equal("<td title=\"\\\\Server_0\\share\">\\\\Server_0\\share</td><td title='Server_0'>ok</td>", result);
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_AttributesHoldingIdentifiers_AreLeftAlone()
        {
            var map = new Dictionary<string, string> { ["target"] = "Item_1" };
            string html = "<a href=\"#target\" onclick=\"go('target')\" data-x=\"target\">target</a>";

            string result = CScrubHandler.ReplaceRegisteredValuesInHtml(html, map);

            Assert.Equal("<a href=\"#target\" onclick=\"go('target')\" data-x=\"Item_1\">Item_1</a>", result);
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_GreaterThanInsideQuotedAttribute_DoesNotEndTheTagEarly()
        {
            var map = new Dictionary<string, string> { ["secret"] = "Item_1" };
            string html = "<td title=\"a > secret\">secret</td>";

            Assert.Equal("<td title=\"a > Item_1\">Item_1</td>", CScrubHandler.ReplaceRegisteredValuesInHtml(html, map));
        }

        // Cell data is written into the report unencoded, so these literal '<' characters really do
        // reach the final pass. They are text, not tags, and must still be scrubbed.
        [Theory]
        [InlineData("<td>Owner: <jsmith@corp.local></td>", "<td>Owner: <Item_1@corp.local></td>")]
        [InlineData("<td>Owner: <jsmith></td>", "<td>Owner: <Item_1></td>")]
        [InlineData("<td>if x<y then srv01host</td>", "<td>if x<y then Server_0</td>")]
        [InlineData("<td>Notes: copy <a few files from srv01host> later</td>", "<td>Notes: copy <a few files from Server_0> later</td>")]
        public void ReplaceRegisteredValuesInHtml_LiteralAngleBracketInCellData_IsTreatedAsTextAndScrubbed(string html, string expected)
        {
            var map = new Dictionary<string, string> { ["jsmith"] = "Item_1", ["srv01host"] = "Server_0" };

            Assert.Equal(expected, CScrubHandler.ReplaceRegisteredValuesInHtml(html, map));
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_GeneratedTableMarkup_IsRecognisedAsTags()
        {
            // Markup in the shape the report generators emit. With 'cell', 'danger', 'title' and 'span'
            // registered, only the cell text may change.
            var map = new Dictionary<string, string>
            {
                ["cell"] = "Item_1", ["danger"] = "Item_2", ["title"] = "Item_3", ["span"] = "Item_4", ["data"] = "Item_5",
            };
            string html = "<table><thead><tr><th>cell</th></tr></thead><tbody>"
                + "<tr><td title=\"cell\" class=\"cell-danger\">danger</td><td><span class=\"danger\">span</span><br></td></tr>"
                + "</tbody></table><img src=\"data:image/png;base64,data\" alt=\"data\" />";

            string expected = "<table><thead><tr><th>Item_1</th></tr></thead><tbody>"
                + "<tr><td title=\"Item_1\" class=\"cell-danger\">Item_2</td><td><span class=\"danger\">Item_4</span><br></td></tr>"
                + "</tbody></table><img src=\"data:image/png;base64,data\" alt=\"Item_5\" />";

            Assert.Equal(expected, CScrubHandler.ReplaceRegisteredValuesInHtml(html, map));
        }

        [Fact]
        public void ReplaceRegisteredValuesInHtml_LongestFirst_AvoidsPartialMatch()
        {
            var map = new Dictionary<string, string>
            {
                ["prodbox"] = "Server_6",
                ["prodbox-vbr-01"] = "Server_7",
            };

            Assert.Equal("<td>Server_7</td>", CScrubHandler.ReplaceRegisteredValuesInHtml("<td>prodbox-vbr-01</td>", map));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReplaceRegisteredValuesInHtml_EmptyHtml_IsPassedThrough(string? html)
        {
            var map = new Dictionary<string, string> { ["something"] = "Server_0" };
            Assert.Equal(html, CScrubHandler.ReplaceRegisteredValuesInHtml(html, map));
        }

        // ---- RestrictFileToOwner (ACL lockdown) -------------------------

        [WindowsOnlyFact]
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
