using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.Html.Exportables
{
    /// <summary>
    /// Which export formats a scrubbed run can produce. A run builds one report (scrubbed or
    /// original), and PDF and PowerPoint are only generated from the original one, so
    /// requesting either with scrubbing on has no effect. The exporter, the CLI argument
    /// parser and the GUI share this so they agree on that, and say so (#261).
    /// </summary>
    internal static class ScrubbedExportPolicy
    {
        /// <summary>What the GUI's PDF checkbox should offer for the current selections.</summary>
        internal enum PdfCheckBoxState
        {
            Available,
            UnavailableBothProducts,
            UnavailableScrubbing,
        }

        /// <summary>The formats that were requested but will not be produced because of scrubbing.</summary>
        internal static IReadOnlyList<string> SkippedFormats(bool scrub, bool exportPdf, bool exportPptx)
        {
            var skipped = new List<string>();
            if (scrub && exportPdf)
            {
                skipped.Add("PDF");
            }

            if (scrub && exportPptx)
            {
                skipped.Add("PowerPoint");
            }

            return skipped;
        }

        internal static PdfCheckBoxState GetPdfCheckBoxState(bool bothProductsDetected, bool scrub)
        {
            if (bothProductsDetected)
            {
                return PdfCheckBoxState.UnavailableBothProducts;
            }

            return scrub ? PdfCheckBoxState.UnavailableScrubbing : PdfCheckBoxState.Available;
        }
    }
}
