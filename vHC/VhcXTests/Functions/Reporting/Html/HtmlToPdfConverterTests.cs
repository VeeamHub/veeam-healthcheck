using VeeamHealthCheck.Functions.Reporting.Html.Exportables;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// Regression tests for issue #123 (unreadable PDF output). BuildDocument only
    /// assembles the DinkToPdf request, so these run without the native wkhtmltopdf library.
    /// </summary>
    [Trait("Category", "Unit")]
    public class HtmlToPdfConverterTests
    {
        private const string Html = "<html><head><title>t</title></head><body></body></html>";

        [Fact]
        public void BuildDocument_AnyHtml_EnablesPrintMediaType()
        {
            var doc = HtmlToPdfConverter.BuildDocument(Html);

            var obj = Assert.Single(doc.Objects);
            Assert.True(obj.WebSettings.PrintMediaType);
        }

        [Fact]
        public void BuildDocument_AnyHtml_InjectsPrintCssBeforeHeadClose()
        {
            var doc = HtmlToPdfConverter.BuildDocument(Html);

            var html = Assert.Single(doc.Objects).HtmlContent;
            var styleIndex = html.IndexOf("@media print", System.StringComparison.Ordinal);
            Assert.True(styleIndex >= 0);
            Assert.True(styleIndex < html.IndexOf("</head>", System.StringComparison.Ordinal));
        }

        [Fact]
        public void BuildDocument_AnyHtml_UnclipsSectionContainersThatHoldTables()
        {
            var doc = HtmlToPdfConverter.BuildDocument(Html);

            var html = Assert.Single(doc.Objects).HtmlContent;
            Assert.Contains(".section-body, .content", html);
            Assert.Contains("overflow: visible !important", html);
        }
    }
}
