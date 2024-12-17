using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System;
using System.IO;
using Xceed.Words.NET;
using HtmlToOpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;

namespace VeeamHealthCheck.Functions.Reporting.Html.Exportables
{
    internal class CHtmlToDocx
    {
        public void ExportHtmlToDocx(string htmlContent, string outputPath)
        {
            // Create a new document
            using (var wordDocument = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
            {
                // Add a main document part
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();
                mainPart.Document.Append(body);

                // Set the document to landscape mode
                var sectionProperties = new SectionProperties();
                var pageSize = new PageSize
                {
                    Width = 16838, // 11.69 inches in twentieths of a point (A4 landscape width)
                    Height = 11906, // 8.27 inches in twentieths of a point (A4 landscape height)
                    Orient = PageOrientationValues.Landscape
                };
                var pageMargin = new PageMargin
                {
                    Top = 720, // 1 inch in twentieths of a point
                    Right = 720,
                    Bottom = 720,
                    Left = 720
                };
                sectionProperties.Append(pageSize);
                sectionProperties.Append(pageMargin);
                body.Append(sectionProperties);

                // Create a new HtmlConverter
                var converter = new HtmlConverter(mainPart);

                // Convert the HTML content to DocX format
                converter.ParseBody(htmlContent);

                // Save the document
                mainPart.Document.Save();
            }
        }
    }
}
