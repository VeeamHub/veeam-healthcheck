//using DinkToPdf;
//using DinkToPdf.Contracts;
//using System.IO;

//public class HtmlToPdfConverter
//{
//    private IConverter _converter;

//    public HtmlToPdfConverter()
//    {
//        _converter = new SynchronizedConverter(new PdfTools());
//    }

//    public byte[] ConvertHtmlToPdf(string htmlContent, string outputPath)
//    {
//        var globalSettings = new GlobalSettings
//        {
//            ColorMode = ColorMode.Color,
//            Orientation = Orientation.Portrait,
//            PaperSize = PaperKind.A4,
//            Margins = new MarginSettings { Top = 10 },
//            DocumentTitle = "PDF Report",
//            Out = outputPath
//        };

//        var objectSettings = new ObjectSettings
//        {
//            PagesCount = true,
//            HtmlContent = htmlContent,
//            WebSettings = { DefaultEncoding = "utf-8" },
//            HeaderSettings = { FontName = "Arial", FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
//            FooterSettings = { FontName = "Arial", FontSize = 9, Line = true, Center = "Report Footer" }
//        };

//        var pdfDoc = new HtmlToPdfDocument()
//        {
//            GlobalSettings = globalSettings,
//            Objects = { objectSettings }
//        };

//        return _converter.Convert(pdfDoc);
//    }
//}
