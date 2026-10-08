using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.Exportables;
using VeeamHealthCheck.Startup;
using Xunit;
using static VeeamHealthCheck.Functions.Reporting.Html.Exportables.ScrubbedExportPolicy;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// Issue #261: PDF and PowerPoint are only generated for the unscrubbed report, so asking for
    /// either with scrubbing on has to be surfaced rather than silently dropped.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ScrubbedExportPolicyTests
    {
        [Theory]
        [InlineData(true, true, true, "PDF,PowerPoint")]
        [InlineData(true, true, false, "PDF")]
        [InlineData(true, false, true, "PowerPoint")]
        [InlineData(true, false, false, "")]
        [InlineData(false, true, true, "")]
        [InlineData(false, false, false, "")]
        public void SkippedFormats_ScrubAndRequestedFormats_ListsOnlyRequestedFormatsWhenScrubbing(
            bool scrub, bool pdf, bool pptx, string expected)
        {
            var skipped = SkippedFormats(scrub, pdf, pptx);

            Assert.Equal(expected, string.Join(",", skipped));
        }

        [Theory]
        [InlineData(false, false, nameof(PdfCheckBoxState.Available))]
        [InlineData(false, true, nameof(PdfCheckBoxState.UnavailableScrubbing))]
        [InlineData(true, false, nameof(PdfCheckBoxState.UnavailableBothProducts))]
        [InlineData(true, true, nameof(PdfCheckBoxState.UnavailableBothProducts))]
        public void GetPdfCheckBoxState_ProductsAndScrub_PicksTheReasonPdfIsUnavailable(
            bool bothProducts, bool scrub, string expected)
        {
            Assert.Equal(expected, GetPdfCheckBoxState(bothProducts, scrub).ToString());
        }

        [Fact]
        public void GetScrubExportWarnings_PdfAndPptxWithScrub_NamesEachFlagAndHowToExport()
        {
            var warnings = CArgsParser.GetScrubExportWarnings(scrub: true, exportPdf: true, exportPptx: true);

            Assert.Equal(2, warnings.Count);
            Assert.Contains("/pdf", warnings[0]);
            Assert.Contains("/scrub:false", warnings[0]);
            Assert.Contains("/pptx", warnings[1]);
        }

        [Theory]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        public void GetScrubExportWarnings_NothingSkipped_ReturnsNoWarnings(bool scrub, bool pdf, bool pptx)
        {
            Assert.Empty(CArgsParser.GetScrubExportWarnings(scrub, pdf, pptx));
        }
    }
}
