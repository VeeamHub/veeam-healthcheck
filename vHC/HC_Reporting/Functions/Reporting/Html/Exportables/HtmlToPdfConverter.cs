using DinkToPdf;
using DinkToPdf.Contracts;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.Exportables
{
    public class HtmlToPdfConverter
    {
        private IConverter converter;
        private PdfTools tools;
        private readonly CLogger log = CGlobals.Logger;

        // The native wkhtmltopdf wrapper is created on first use. An unused PdfTools has no loaded
        // module, and its finalizer throws NullReferenceException on the finalizer thread, which
        // terminates the process (exit 134 off Windows with /pdf + /import).
        private IConverter GetConverter()
        {
            if (this.converter == null)
            {
                this.tools = new PdfTools();
                this.converter = new SynchronizedConverter(this.tools);
            }

            return this.converter;
        }

        // Marker class added to <html> so css.css can skip rules meant only for browser printing.
        internal const string PdfExportClass = "pdf-export";

        // wkhtmltopdf renders with screen CSS unless PrintMediaType is set, so the report's own
        // @media print rules (hide the fixed sidebar, drop the .main margin) never applied (#123).
        // The injected block below also un-clips the scrollable section containers: the report
        // has no .table-responsive wrapper, tables sit in .section-body / .content, which are
        // overflow: auto in the screen CSS and would otherwise render as a clipped box.
        private const string PrintCss = @"<style>
@media print {
    table { width: 100%; page-break-inside: auto; }
    tr { page-break-inside: avoid; page-break-after: auto; }
    td, th { word-wrap: break-word; overflow-wrap: break-word; }
    .section-body, .content { overflow: visible !important; }
}
</style>";

        internal static HtmlToPdfDocument BuildDocument(string htmlContent)
        {
            var html = htmlContent.Replace("</head>", PrintCss + "</head>");

            // Lets css.css skip its browser-print table shrink: the PDF is A3, so it needs no shrinking.
            var htmlTag = html.IndexOf("<html>", StringComparison.Ordinal);
            if (htmlTag >= 0)
            {
                html = html.Remove(htmlTag, "<html>".Length).Insert(htmlTag, "<html class=\"" + PdfExportClass + "\">");
            }

            return new HtmlToPdfDocument()
            {
                GlobalSettings = {
                    ColorMode = DinkToPdf.ColorMode.Color,
                    Orientation = DinkToPdf.Orientation.Landscape,
                    PaperSize = DinkToPdf.PaperKind.A3,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 15, Right = 15 },
                },
                Objects = {
                    new ObjectSettings()
                    {
                        HtmlContent = html,
                        WebSettings = { DefaultEncoding = "utf-8", PrintMediaType = true },
                    }
                }
            };
        }

        public void ConvertHtmlToPdf(string htmlContent, string outputPath)
        {
            var doc = BuildDocument(htmlContent);
            var engine = this.GetConverter();

            // Run conversion on a dedicated STA thread to avoid deadlocking the WPF UI thread.
            // DinkToPdf's SynchronizedConverter uses COM interop which requires an STA thread.
            byte[] pdf = null;
            Exception conversionError = null;

            var thread = new Thread(() =>
            {
                try
                {
                    pdf = engine.Convert(doc);
                }
                catch (Exception ex)
                {
                    conversionError = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!thread.Join(TimeSpan.FromMinutes(5)))
            {
                this.log.Error("[PdfConverter] PDF conversion timed out after 5 minutes.", false);
                throw new TimeoutException("PDF conversion timed out after 5 minutes.");
            }

            if (conversionError != null)
            {
                this.log.Error($"[PdfConverter] PDF conversion failed: {conversionError.Message}", false);
                throw conversionError;
            }

            File.WriteAllBytes(outputPath, pdf);
        }

        public void Dispose()
        {
            // If conversion was attempted but the native library never loaded (wkhtmltopdf missing),
            // PdfTools.Dispose would throw from its finalizer too, so skip it.
            if (this.tools != null && !this.tools.IsLoaded)
            {
                GC.SuppressFinalize(this.tools);
            }

            this.tools = null;
            this.converter = null;
        }
    }
}


