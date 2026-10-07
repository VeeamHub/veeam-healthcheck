using VeeamHealthCheck.Functions.Reporting.Html.Exportables;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
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
        public void BuildDocument_HtmlWithOpenTag_MarksHtmlElementAsPdfExport()
        {
            var doc = HtmlToPdfConverter.BuildDocument(Html);

            var html = Assert.Single(doc.Objects).HtmlContent;
            Assert.Contains("<html class=\"" + HtmlToPdfConverter.PdfExportClass + "\">", html);
            Assert.DoesNotContain("<html>", html);
        }

        [Fact]
        public void BuildDocument_RealReportHeader_MarksHtmlElementAsPdfExport()
        {
            // Guards against CHtmlFormatting.Header() changing its <html> tag (for example adding an
            // attribute) and silently losing the marker that keeps the A3 PDF at its normal size.
            var reportHtml = new CHtmlFormatting().Header() + "<body></body></html>";

            var doc = HtmlToPdfConverter.BuildDocument(reportHtml);

            var html = Assert.Single(doc.Objects).HtmlContent;
            Assert.Contains("<html class=\"" + HtmlToPdfConverter.PdfExportClass + "\">", html);
        }

        [Fact]
        public void BuildDocument_HtmlWithoutOpenTag_AddsNoPdfExportClass()
        {
            var doc = HtmlToPdfConverter.BuildDocument("<p>x</p></head>");

            var html = Assert.Single(doc.Objects).HtmlContent;
            Assert.DoesNotContain(HtmlToPdfConverter.PdfExportClass, html);
        }

        [Fact]
        public void BuildDocument_AnyHtml_UnclipsSectionContainersThatHoldTables()
        {
            var doc = HtmlToPdfConverter.BuildDocument(Html);

            var html = Assert.Single(doc.Objects).HtmlContent;
            foreach (var selector in new[] { @"\.section-body", @"\.content" })
            {
                Assert.Matches(selector + @"\b[^{}]*\{[^}]*overflow:\s*visible\s*!important", html);
            }
        }
    }
}
