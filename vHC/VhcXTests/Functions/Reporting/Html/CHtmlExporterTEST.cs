using System;
using System.IO;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// Tests for CHtmlExporter functionality.
    /// Note: Many export methods depend on CGlobals static state, limiting testability.
    /// These tests focus on what can be tested without extensive mocking.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CHtmlExporterTEST : IDisposable
    {
        private readonly string _testOutputDir;
        private readonly string _originalDesiredPath;

        public CHtmlExporterTEST()
        {
            // Save original path and create test output directory
            _originalDesiredPath = CGlobals.desiredPath;
            _testOutputDir = Path.Combine(Path.GetTempPath(), "VhcExporterTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputDir);

            // Set up CGlobals for testing
            CGlobals.desiredPath = _testOutputDir;
        }

        public void Dispose()
        {
            // Restore original path
            CGlobals.desiredPath = _originalDesiredPath;

            // Clean up test directory
            if (Directory.Exists(_testOutputDir))
            {
                try
                {
                    Directory.Delete(_testOutputDir, true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }

        #region Constructor Tests

        [Fact]
        public void CHtmlExporter_Constructor_CreatesOutputDirectories()
        {
            // Arrange & Act
            var exporter = new CHtmlExporter("TestServer");

            // Assert
            Assert.True(Directory.Exists(CGlobals.desiredPath));
            Assert.True(Directory.Exists(CGlobals.desiredPath + CVariables.safeSuffix));
            Assert.True(Directory.Exists(CGlobals.desiredPath + CVariables.unsafeSuffix));
        }

        [Fact]
        public void CHtmlExporter_Constructor_AcceptsServerName()
        {
            // This test just verifies the constructor doesn't throw
            var exporter = new CHtmlExporter("TestServer");
            Assert.NotNull(exporter);
        }

        [Fact]
        public void CHtmlExporter_Constructor_AcceptsEmptyServerName()
        {
            var exporter = new CHtmlExporter(string.Empty);
            Assert.NotNull(exporter);
        }

        #endregion

        #region OpenHtmlIfEnabled Tests

        [Fact]
        public void OpenHtmlIfEnabled_False_ReturnsOne()
        {
            var exporter = new CHtmlExporter("TestServer");
            var result = exporter.OpenHtmlIfEnabled(false);

            Assert.Equal(1, result);
        }

        // Note: OpenHtmlIfEnabled(true) cannot be easily tested as it tries to launch a browser
        // and depends on WPF Application.Current.Dispatcher

        #endregion

        #region Export Method Structure Tests

        // Note: Full export tests require more complex setup due to CGlobals dependencies
        // These tests verify the methods exist and have the expected signatures

        [Fact]
        public void ExportVbrHtml_MethodExists()
        {
            var exporter = new CHtmlExporter("TestServer");

            // Verify method exists with correct signature
            var method = typeof(CHtmlExporter).GetMethod("ExportVbrHtml");
            Assert.NotNull(method);
            Assert.Equal(typeof(int), method.ReturnType);

            var parameters = method.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(string), parameters[0].ParameterType);
            Assert.Equal(typeof(bool), parameters[1].ParameterType);
        }

        [Fact]
        public void ExportHtmlVb365_MethodExists()
        {
            var exporter = new CHtmlExporter("TestServer");

            var method = typeof(CHtmlExporter).GetMethod("ExportHtmlVb365");
            Assert.NotNull(method);
            Assert.Equal(typeof(int), method.ReturnType);
        }

        [Fact]
        public void ExportVbrSecurityHtml_MethodExists()
        {
            var exporter = new CHtmlExporter("TestServer");

            var method = typeof(CHtmlExporter).GetMethod("ExportVbrSecurityHtml");
            Assert.NotNull(method);
            Assert.Equal(typeof(int), method.ReturnType);
        }

        #endregion

        #region Integration Tests (require full setup)

        // These tests actually export HTML and verify file creation
        // They depend on CGlobals being properly configured

        [Fact]
        public void ExportVbrHtml_ValidHtml_CreatesFile()
        {
            // Arrange
            var exporter = new CHtmlExporter("TestServer");
            var testHtml = "<html><head><title>Test</title></head><body>Test content</body></html>";

            // Temporarily disable auto-open and PDF/PPTX export
            bool originalOpenHtml = CGlobals.OpenHtml;
            bool originalExportPdf = CGlobals.EXPORTPDF;
            bool originalExportPptx = CGlobals.EXPORTPPTX;

            CGlobals.OpenHtml = false;
            CGlobals.EXPORTPDF = false;
            CGlobals.EXPORTPPTX = false;

            try
            {
                // Act
                var result = exporter.ExportVbrHtml(testHtml, false);

                // Assert
                Assert.Equal(0, result);

                // Verify a file was created in the unscrubbed directory
                var origDir = Path.Combine(_testOutputDir, CVariables.unsafeSuffix.TrimStart('\\'));
                var files = Directory.GetFiles(origDir, "*.html");
                Assert.NotEmpty(files);
            }
            finally
            {
                // Restore original settings
                CGlobals.OpenHtml = originalOpenHtml;
                CGlobals.EXPORTPDF = originalExportPdf;
                CGlobals.EXPORTPPTX = originalExportPptx;
            }
        }

        [Fact]
        public void ExportVbrHtml_Scrubbed_CreatesFileInScrubbedDirectory()
        {
            // Arrange
            var exporter = new CHtmlExporter("TestServer");
            var testHtml = "<html><head><title>Test</title></head><body>Test content</body></html>";

            bool originalOpenHtml = CGlobals.OpenHtml;
            bool originalExportPdf = CGlobals.EXPORTPDF;
            bool originalExportPptx = CGlobals.EXPORTPPTX;

            CGlobals.OpenHtml = false;
            CGlobals.EXPORTPDF = false;
            CGlobals.EXPORTPPTX = false;

            try
            {
                // Act
                var result = exporter.ExportVbrHtml(testHtml, true);

                // Assert
                Assert.Equal(0, result);

                // Verify a file was created in the scrubbed directory
                var anonDir = Path.Combine(_testOutputDir, CVariables.safeSuffix.TrimStart('\\'));
                var files = Directory.GetFiles(anonDir, "*.html");
                Assert.NotEmpty(files);
            }
            finally
            {
                CGlobals.OpenHtml = originalOpenHtml;
                CGlobals.EXPORTPDF = originalExportPdf;
                CGlobals.EXPORTPPTX = originalExportPptx;
            }
        }

        [Fact]
        public void ExportVbrSecurityHtml_ValidHtml_CreatesFile()
        {
            // Arrange
            var exporter = new CHtmlExporter("TestServer");
            var testHtml = "<html><head><title>Security Report</title></head><body>Security content</body></html>";

            bool originalOpenHtml = CGlobals.OpenHtml;
            CGlobals.OpenHtml = false;

            try
            {
                // Act
                var result = exporter.ExportVbrSecurityHtml(testHtml, false);

                // Assert
                Assert.Equal(0, result);

                // Verify a security report file was created
                var origDir = Path.Combine(_testOutputDir, CVariables.unsafeSuffix.TrimStart('\\'));
                var files = Directory.GetFiles(origDir, "*Security*.html");
                Assert.NotEmpty(files);
            }
            finally
            {
                CGlobals.OpenHtml = originalOpenHtml;
            }
        }

        #endregion
    }
}
