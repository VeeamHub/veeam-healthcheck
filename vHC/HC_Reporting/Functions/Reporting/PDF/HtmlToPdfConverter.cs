using DinkToPdf;
using DinkToPdf.Contracts;
using System;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Controls;

public class HtmlToPdfConverter
{
    private IConverter _converter;

    public HtmlToPdfConverter()
    {
        _converter = new SynchronizedConverter(new PdfTools());
    }

    [STAThread]
    public void ConvertHtmlToPdf(string htmlContent, string outputPath)
    {
        var converter = new SynchronizedConverter(new PdfTools());
        var html = htmlContent; //"<h1>Hello, World!</h1>"; // replace with your HTML string
        var doc = new HtmlToPdfDocument()
        {
            GlobalSettings = {
            ColorMode = DinkToPdf.ColorMode.Color,
            Orientation = DinkToPdf.Orientation.Portrait,
            PaperSize = DinkToPdf.PaperKind.A4Plus,
    },
            Objects = {
        new ObjectSettings()
        {
            HtmlContent = html,
            WebSettings = { DefaultEncoding = "utf-8" },
        }
    }
        };

        byte[] pdf = converter.Convert(doc);
        File.WriteAllBytes(outputPath, pdf);
        
    }
    // dispose method
    public void Dispose()
    {
        _converter = null;
    }
}

