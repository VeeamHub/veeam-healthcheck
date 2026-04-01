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

        public void ConvertHtmlToPdf(string htmlContent, string outputPath)
        {
            var printCss = @"<style>
@media print {
    table { width: 100%; table-layout: fixed; page-break-inside: auto; }
    tr { page-break-inside: avoid; page-break-after: auto; }
    td, th { word-wrap: break-word; overflow-wrap: break-word; }
    .table-responsive { overflow: visible !important; }
}
</style>";

            var html = htmlContent.Replace("</head>", printCss + "</head>");

            var doc = new HtmlToPdfDocument()
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
                        WebSettings = { DefaultEncoding = "utf-8" },
                    }
                }
            };

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


