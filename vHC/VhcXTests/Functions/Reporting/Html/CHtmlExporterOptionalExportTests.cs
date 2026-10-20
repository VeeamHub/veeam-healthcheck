using System;
using System.Collections.Generic;
using System.IO;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// PDF and PowerPoint are optional exports of an HTML report that is already written: each one
    /// fails on its own, in both the VBR and VB365 paths. The converters are injected so these tests
    /// do not depend on the OS having a working PDF engine.
    /// </summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public sealed class CHtmlExporterOptionalExportTests : IDisposable
    {
        private const string Html = "<html><head><title>Test</title></head><body>Test content</body></html>";

        private readonly string outputDir = Path.Combine(Path.GetTempPath(), "VhcOptionalExport_" + Guid.NewGuid().ToString("N"));
        private readonly string originalDesiredPath = CGlobals.desiredPath;
        private readonly string originalLogFile = CGlobals.Logger.logFile;
        private readonly bool originalOpenHtml = CGlobals.OpenHtml;
        private readonly bool originalExportPdf = CGlobals.EXPORTPDF;
        private readonly bool originalExportPptx = CGlobals.EXPORTPPTX;
        private readonly string logPath;

        public CHtmlExporterOptionalExportTests()
        {
            Directory.CreateDirectory(this.outputDir);
            this.logPath = Path.Combine(this.outputDir, "report.log");
            CGlobals.desiredPath = this.outputDir;
            typeof(CLogger).GetProperty(nameof(CLogger.logFile))!.SetValue(CGlobals.Logger, this.logPath);
            CGlobals.OpenHtml = false;
            CGlobals.EXPORTPDF = true;
            CGlobals.EXPORTPPTX = true;
        }

        public void Dispose()
        {
            CGlobals.desiredPath = this.originalDesiredPath;
            CGlobals.OpenHtml = this.originalOpenHtml;
            CGlobals.EXPORTPDF = this.originalExportPdf;
            CGlobals.EXPORTPPTX = this.originalExportPptx;
            typeof(CLogger).GetProperty(nameof(CLogger.logFile))!.SetValue(CGlobals.Logger, this.originalLogFile);
            if (Directory.Exists(this.outputDir))
            {
                Directory.Delete(this.outputDir, true);
            }
        }

        public static IEnumerable<object[]> Products()
        {
            yield return new object[] { "VBR" };
            yield return new object[] { "VB365" };
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void Export_PdfEngineFails_StillWritesPptxReportsErrorAndReturnsZero(string product)
        {
            var pptxCalls = new List<string>();
            CHtmlExporter exporter = this.NewExporter(
                pdf: (_, _) => throw new PlatformNotSupportedException("COM Interop is not supported"),
                pptx: (_, path) => { pptxCalls.Add(path); File.WriteAllText(path, "pptx"); });

            int result = Export(exporter, product);

            Assert.Equal(0, result);
            string pptxPath = Assert.Single(pptxCalls);
            Assert.EndsWith(".pptx", pptxPath);
            Assert.True(File.Exists(pptxPath));
            Assert.Single(Directory.GetFiles(this.outputDir, "*.html", SearchOption.AllDirectories));
            string log = File.ReadAllText(this.logPath);
            Assert.Contains("Failed to export PDF: COM Interop is not supported", log);
            Assert.DoesNotContain("Failed at HTML Export", log);
            Assert.Contains("these requested exports failed: PDF.", log);
            Assert.Contains("HTML not opened. Option not enabled", log); // OpenHtmlIfEnabled was still reached
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void Export_PptxFails_PdfStillWrittenReportsErrorAndReturnsZero(string product)
        {
            var pdfCalls = new List<string>();
            CHtmlExporter exporter = this.NewExporter(
                pdf: (_, path) => { pdfCalls.Add(path); File.WriteAllText(path, "pdf"); },
                pptx: (_, _) => throw new InvalidOperationException("bad slide"));

            int result = Export(exporter, product);

            Assert.Equal(0, result);
            Assert.True(File.Exists(Assert.Single(pdfCalls)));
            string log = File.ReadAllText(this.logPath);
            Assert.Contains("Failed to export PowerPoint: bad slide", log);
            Assert.Contains("these requested exports failed: PowerPoint.", log);
            Assert.Contains("HTML not opened. Option not enabled", log);
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void Export_BothOptionalExportsFail_HtmlIsKeptAndEachFailureIsNamed(string product)
        {
            CHtmlExporter exporter = this.NewExporter(
                pdf: (_, _) => throw new InvalidOperationException("pdf down"),
                pptx: (_, _) => throw new InvalidOperationException("pptx down"));

            int result = Export(exporter, product);

            Assert.Equal(0, result);
            Assert.Single(Directory.GetFiles(this.outputDir, "*.html", SearchOption.AllDirectories));
            string log = File.ReadAllText(this.logPath);
            Assert.Contains("Failed to export PDF: pdf down", log);
            Assert.Contains("Failed to export PowerPoint: pptx down", log);
            Assert.Contains("these requested exports failed: PDF, PowerPoint.", log);
            Assert.Contains("HTML not opened. Option not enabled", log);
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void Export_BothOptionalExportsSucceed_WritesBothAndLogsNoFailure(string product)
        {
            CHtmlExporter exporter = this.NewExporter(
                pdf: (_, path) => File.WriteAllText(path, "pdf"),
                pptx: (_, path) => File.WriteAllText(path, "pptx"));

            int result = Export(exporter, product);

            Assert.Equal(0, result);
            Assert.Single(Directory.GetFiles(this.outputDir, "*.pdf", SearchOption.AllDirectories));
            Assert.Single(Directory.GetFiles(this.outputDir, "*.pptx", SearchOption.AllDirectories));
            Assert.DoesNotContain("requested exports failed", File.ReadAllText(this.logPath));
        }

        private CHtmlExporter NewExporter(Action<string, string> pdf, Action<string, string> pptx) =>
            new CHtmlExporter("TestServer") { PdfConverter = pdf, PptxConverter = pptx };

        private static int Export(CHtmlExporter exporter, string product) =>
            product == "VBR" ? exporter.ExportVbrHtml(Html, false) : exporter.ExportHtmlVb365(Html, false);
    }
}
