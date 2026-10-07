using System.Text.RegularExpressions;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.Shared
{
    /// <summary>
    /// Regression tests for issue #257: printing the HTML report from a browser clipped wide
    /// tables and printed scrollbars, because the print overrides only existed in the PDF exporter.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ReportPrintCssTests
    {
        private static string PrintBlock()
        {
            var css = CHtmlFormatting.GetEmbeddedCssContent("css.css");
            var start = css.IndexOf("@media print", System.StringComparison.Ordinal);
            Assert.True(start >= 0, "css.css has no @media print block");

            // The print block is a top-level rule: walk braces to find where it closes.
            var depth = 0;
            for (var i = css.IndexOf('{', start); i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) return css.Substring(start, i - start + 1);
            }

            throw new Xunit.Sdk.XunitException("Unterminated @media print block in css.css");
        }

        [Theory]
        [InlineData(@"\.section-body")]
        [InlineData(@"\.content")]
        public void PrintCss_ScrollContainersHoldingTables_AreUnclipped(string selector)
        {
            Assert.Matches(selector + @"\b[^{}]*\{[^}]*overflow:\s*visible\s*!important", PrintBlock());
        }

        [Fact]
        public void PrintCss_SectionCard_DoesNotClipOverflow()
        {
            Assert.Matches(@"\.section-card\s*\{[^}]*overflow:\s*visible", PrintBlock());
        }

        [Fact]
        public void PrintCss_TableCells_WrapInsteadOfStayingOnOneLine()
        {
            Assert.Matches(@"th,\s*td\s*\{[^}]*white-space:\s*normal", PrintBlock());
            Assert.Matches(@"th,\s*td\s*\{[^}]*overflow-wrap:\s*anywhere", PrintBlock());
        }

        [Fact]
        public void PrintCss_Tables_ShrinkAndRepeatHeaderAcrossPages()
        {
            var block = PrintBlock();
            Assert.Matches(@"\btable\s*\{[^}]*font-size:\s*\d+px", block);
            Assert.Matches(@"thead\s*\{[^}]*display:\s*table-header-group", block);
        }

        [Fact]
        public void PrintCss_Page_DefaultsToLandscape()
        {
            var css = CHtmlFormatting.GetEmbeddedCssContent("css.css");
            Assert.Matches(@"@page\s*\{[^}]*size:\s*landscape", css);
        }
    }
}
