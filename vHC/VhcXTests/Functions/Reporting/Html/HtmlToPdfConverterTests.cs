using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DinkToPdf;
using DinkToPdf.Contracts;
using Moq;
using VeeamHealthCheck.Functions.Reporting.Html.Exportables;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// Regression tests for issue #123 (unreadable PDF output). BuildDocument only
    /// assembles the DinkToPdf request, so these run without the native wkhtmltopdf library.
    /// </summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
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

        [Fact]
        public void BuildDocument_AnyHtml_SetsLandscapeA3ColorMarginsAndEncoding()
        {
            HtmlToPdfDocument document = HtmlToPdfConverter.BuildDocument(Html);

            Assert.Equal(ColorMode.Color, document.GlobalSettings.ColorMode);
            Assert.Equal(Orientation.Landscape, document.GlobalSettings.Orientation);
            Assert.Equal(PaperKind.A3, document.GlobalSettings.PaperSize);
            Assert.Equal(10, document.GlobalSettings.Margins.Top);
            Assert.Equal(10, document.GlobalSettings.Margins.Bottom);
            Assert.Equal(15, document.GlobalSettings.Margins.Left);
            Assert.Equal(15, document.GlobalSettings.Margins.Right);
            Assert.Equal("utf-8", Assert.Single(document.Objects).WebSettings.DefaultEncoding);
        }

        [Theory]
        [InlineData("")]
        [InlineData("<p>fragment &amp; text</p>")]
        public void BuildDocument_NoHeadOrHtmlTag_PreservesInput(string html)
        {
            Assert.Equal(html, Assert.Single(HtmlToPdfConverter.BuildDocument(html).Objects).HtmlContent);
        }

        [Fact]
        public void BuildDocument_NullHtml_ThrowsBeforeRendering()
        {
            Assert.Throws<NullReferenceException>(() => HtmlToPdfConverter.BuildDocument(null!));
        }

        [Fact]
        public void ConvertHtmlToPdf_NullHtml_ThrowsBeforeWritingOutput()
        {
            using var report = new GoldenVbrReport();
            string path = Path.Combine(report.Root, "null.pdf");
            var converter = CreateConverterForFlowTest();
            try
            {
                Assert.Throws<NullReferenceException>(() => converter.ConvertHtmlToPdf(null!, path));
                Assert.False(File.Exists(path));
            }
            finally
            {
                converter.Dispose();
            }
        }

        [Fact]
        public void ConvertHtmlToPdf_PlatformThreadSupport_WritesBytesOrReportsUnsupportedSta()
        {
            using var report = new GoldenVbrReport();
            string path = Path.Combine(report.Root, "unsupported.pdf");
            var converter = CreateConverterForFlowTest();
            var engine = new Mock<IConverter>(MockBehavior.Strict);
            byte[] bytes = Encoding.ASCII.GetBytes("%PDF-flow-fixture");
            engine.Setup(value => value.Convert(It.IsAny<IDocument>())).Returns(bytes);
            SetConverter(converter, engine.Object);
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    converter.ConvertHtmlToPdf(Html, path);
                    Assert.Equal(bytes, File.ReadAllBytes(path));
                    engine.VerifyAll();
                }
                else
                {
                    Assert.Throws<PlatformNotSupportedException>(() => converter.ConvertHtmlToPdf(Html, path));
                    Assert.False(File.Exists(path));
                    engine.Verify(value => value.Convert(It.IsAny<IDocument>()), Times.Never);
                }
            }
            finally
            {
                converter.Dispose();
            }
        }

        [WindowsOnlyFact]
        public void ConvertHtmlToPdf_RealRenderer_WritesPdfWithAtLeastOnePage()
        {
            using var report = new GoldenVbrReport();
            string path = Path.Combine(report.Root, "rendered.pdf");
            var converter = new HtmlToPdfConverter();
            try
            {
                converter.ConvertHtmlToPdf("<html><head><meta charset=\"utf-8\"></head><body><h1>Local PDF fixture</h1><table><tr><td>42</td></tr></table></body></html>", path);
                byte[] bytes = File.ReadAllBytes(path);
                Assert.True(bytes.Length > 4);
                Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.True(Regex.Matches(Encoding.Latin1.GetString(bytes), @"/Type\s*/Page\b").Count > 0);
            }
            finally
            {
                converter.Dispose();
            }
        }

        [WindowsOnlyFact]
        public void ConvertHtmlToPdf_ConverterThrows_PropagatesErrorWithoutWritingOutput()
        {
            using var report = new GoldenVbrReport();
            string path = Path.Combine(report.Root, "error.pdf");
            var failure = new InvalidOperationException("fixture conversion failed");
            var engine = new Mock<IConverter>(MockBehavior.Strict);
            engine.Setup(value => value.Convert(It.IsAny<IDocument>())).Throws(failure);
            var converter = CreateConverterForFlowTest();
            SetConverter(converter, engine.Object);
            try
            {
                Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => converter.ConvertHtmlToPdf(Html, path)));
                Assert.False(File.Exists(path));
                Assert.Contains(failure.Message, File.ReadAllText(Path.Combine(report.Root, "report.log")));
                engine.VerifyAll();
            }
            finally
            {
                converter.Dispose();
            }
        }

        [WindowsOnlyFact]
        public void ConvertHtmlToPdf_MissingOutputDirectory_PropagatesFileErrorAfterConversion()
        {
            using var report = new GoldenVbrReport();
            string path = Path.Combine(report.Root, "missing", "error.pdf");
            var engine = new Mock<IConverter>(MockBehavior.Strict);
            engine.Setup(value => value.Convert(It.IsAny<IDocument>())).Returns(Encoding.ASCII.GetBytes("%PDF-fixture"));
            var converter = CreateConverterForFlowTest();
            SetConverter(converter, engine.Object);
            try
            {
                Assert.Throws<DirectoryNotFoundException>(() => converter.ConvertHtmlToPdf(Html, path));
                Assert.False(File.Exists(path));
                engine.VerifyAll();
            }
            finally
            {
                converter.Dispose();
            }
        }

        private static void SetConverter(HtmlToPdfConverter converter, IConverter engine) =>
            typeof(HtmlToPdfConverter).GetField("converter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(converter, engine);

        [Fact(Skip = "bug: disposing an unused PDF converter leaves PdfTools.Finalize throwing NullReferenceException")]
        public void Dispose_UnusedConverter_NativeToolsFinalizerDoesNotThrow()
        {
            using var report = new GoldenVbrReport();
            var converter = new HtmlToPdfConverter();
            object tools = NativeTools(converter);
            try
            {
                converter.Dispose();
                MethodInfo finalizer = tools.GetType().GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Exception? error = Record.Exception(() => finalizer.Invoke(tools, null));
                Assert.Null(error);
            }
            finally
            {
                // Invoke deterministically above instead of letting a finalizer exception
                // terminate the whole test host during an unrelated test.
                GC.SuppressFinalize(tools);
            }
        }

        private static HtmlToPdfConverter CreateConverterForFlowTest()
        {
            var converter = new HtmlToPdfConverter();
            // These tests never initialize wkhtmltopdf. Its unused PdfTools finalizer
            // crashes the host; the separate Dispose regression records that defect.
            GC.SuppressFinalize(NativeTools(converter));
            return converter;
        }

        private static object NativeTools(HtmlToPdfConverter converter)
        {
            object engine = typeof(HtmlToPdfConverter).GetField("converter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(converter)!;
            return typeof(BasicConverter).GetField("Tools", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(engine)!;
        }
    }
}
