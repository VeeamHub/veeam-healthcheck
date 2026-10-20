using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html;

/// <summary>
/// End-to-end: /pdf and /pptx are independent optional exports. Off Windows the real PDF engine
/// fails ("COM Interop is not supported"), which is what makes this a natural RED for the old
/// single-try ordering. On Windows the PDF may succeed, and the assertions still hold.
/// </summary>
[Collection("GlobalState")]
public sealed class ExportIndependenceTests
{
    [Fact]
    public void RunFullVbrReport_PdfAndPptxRequested_StillWritesPptxWhateverThePdfEngineDoes()
    {
        using var report = new GoldenVbrReport();
        string html = report.Generate(scrub: false, requestPptx: true, requestPdf: true);

        Assert.Contains("<table", html);
        string pptx = Assert.Single(Directory.GetFiles(report.Root, "*.pptx", SearchOption.AllDirectories));
        Assert.True(new FileInfo(pptx).Length > 0);
        Assert.True(File.Exists(report.JsonPath));
    }
}
