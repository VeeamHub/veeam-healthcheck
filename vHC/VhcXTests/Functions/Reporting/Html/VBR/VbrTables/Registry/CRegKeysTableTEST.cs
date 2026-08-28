// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Registry;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.Registry
{
    /// <summary>
    /// Tests that CRegKeysTable only converts the "|" multi-value delimiter to a line
    /// break for registry entries that were actually joined from a multi-value
    /// (string[]) registry key, per PR #203 review finding: a single-value entry may
    /// legitimately contain a literal "|" character that must not be corrupted.
    /// </summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class CRegKeysTableTEST
    {
        [Fact]
        public void Render_MultiValueRegistryKey_ConvertsDelimiterToLineBreak()
        {
            var originalKeys = CGlobals.DEFAULTREGISTRYKEYS;
            var originalJson = CGlobals.FullReportJson;
            CGlobals.DEFAULTREGISTRYKEYS = new Dictionary<string, object>
            {
                ["VhcTest_MultiValueKey"] = new[] { "austriaeast", "brazilsoutheast" },
            };

            try
            {
                string html = new CRegKeysTable().Render(scrub: false);

                Assert.Contains("austriaeast<br>brazilsoutheast", html);
                Assert.DoesNotContain("austriaeast|brazilsoutheast", html);
            }
            finally
            {
                CGlobals.DEFAULTREGISTRYKEYS = originalKeys;
                CGlobals.FullReportJson = originalJson;
            }
        }

        [Fact]
        public void Render_SingleValueRegistryKeyContainingLiteralPipe_LeavesPipeUnchanged()
        {
            var originalKeys = CGlobals.DEFAULTREGISTRYKEYS;
            var originalJson = CGlobals.FullReportJson;
            CGlobals.DEFAULTREGISTRYKEYS = new Dictionary<string, object>
            {
                ["VhcTest_SingleValueKeyWithPipe"] = "C:\\Path|WithLiteralPipe",
            };

            try
            {
                string html = new CRegKeysTable().Render(scrub: false);

                Assert.Contains("C:\\Path|WithLiteralPipe", html);
                Assert.DoesNotContain("C:\\Path<br>WithLiteralPipe", html);
            }
            finally
            {
                CGlobals.DEFAULTREGISTRYKEYS = originalKeys;
                CGlobals.FullReportJson = originalJson;
            }
        }
    }
}
