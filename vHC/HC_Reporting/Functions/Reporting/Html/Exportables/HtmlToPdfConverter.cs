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
        private readonly CLogger log = CGlobals.Logger;

        public HtmlToPdfConverter()
        {
            this.converter = new SynchronizedConverter(new PdfTools());
        }

        // wkhtmltopdf renders with screen CSS unless PrintMediaType is set, so the report's own
        // @media print rules (hide the fixed sidebar, drop the .main margin) never applied (#123).
        // The injected block below also un-clips the scrollable section containers: the report
        // has no .table-responsive wrapper, tables sit in .section-body / .content, which are
        // overflow: auto in the screen CSS and would otherwise render as a clipped box.
        private const string PrintCss = @"<style>
@media print {
    table { width: 100%; table-layout: fixed; page-break-inside: auto; }
    tr { page-break-inside: avoid; page-break-after: auto; }
    td, th { word-wrap: break-word; overflow-wrap: break-word; }
    .section-body, .content { overflow: visible !important; }
}
</style>";

        internal static HtmlToPdfDocument BuildDocument(string htmlContent)
        {
            var html = htmlContent.Replace("</head>", PrintCss + "</head>");

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

            // Run conversion on a dedicated STA thread to avoid deadlocking the WPF UI thread.
            // DinkToPdf's SynchronizedConverter uses COM interop which requires an STA thread.
            byte[] pdf = null;
            Exception conversionError = null;

            var thread = new Thread(() =>
            {
                try
                {
                    pdf = this.converter.Convert(doc);
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
            this.converter = null;
        }
    }
}


