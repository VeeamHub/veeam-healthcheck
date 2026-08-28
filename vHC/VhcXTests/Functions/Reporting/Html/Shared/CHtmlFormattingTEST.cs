using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.Shared
{
    /// <summary>
    /// Tests for CHtmlFormatting.RenderMultiValueHtml, the shared HTML call-site
    /// helper introduced to fix the issue #171 HTML-leak bug class (ADR 0029).
    /// </summary>
    [Trait("Category", "Unit")]
    public class CHtmlFormattingTEST
    {
        [Fact]
        public void RenderMultiValueHtml_PipeDelimitedValue_ConvertsToLineBreaks()
        {
            var form = new CHtmlFormatting();

            string result = form.RenderMultiValueHtml("gateway1|gateway2");

            Assert.Equal("gateway1<br>gateway2", result);
        }

        [Fact]
        public void RenderMultiValueHtml_NoDelimiter_ReturnsValueUnchanged()
        {
            var form = new CHtmlFormatting();

            string result = form.RenderMultiValueHtml("singlehost");

            Assert.Equal("singlehost", result);
        }

        [Fact]
        public void RenderMultiValueHtml_Null_ReturnsNull()
        {
            var form = new CHtmlFormatting();

            string result = form.RenderMultiValueHtml(null);

            Assert.Null(result);
        }

        [Fact]
        public void RenderMultiValueHtml_Empty_ReturnsEmpty()
        {
            var form = new CHtmlFormatting();

            string result = form.RenderMultiValueHtml(string.Empty);

            Assert.Equal(string.Empty, result);
        }
    }
}
