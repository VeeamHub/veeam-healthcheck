# DinkToPdf converter/PdfTools native resources never released

**Category:** reporting-vbr-core
**Severity:** Medium
**Type:** Resource Leak
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs:14-19,86-90`

## Summary
`HtmlToPdfConverter` constructs `new SynchronizedConverter(new PdfTools())` per instance. `PdfTools`
wraps the native `libwkhtmltox` library and implements `IDisposable`; the wkhtmltox library also has a
global init/deinit lifecycle. `Dispose()` here merely sets the managed reference to `null` and never
disposes `PdfTools`, so the native library handle is leaked on every PDF export.

## Evidence
```csharp
// HtmlToPdfConverter.cs:17-19
public HtmlToPdfConverter()
{
    this.converter = new SynchronizedConverter(new PdfTools());   // native PdfTools created
}

// HtmlToPdfConverter.cs:86-90
public void Dispose()
{
    this.converter = null;    // reference dropped, PdfTools never Disposed
}
```
`CHtmlExporter.ExportHtmlStringToPDF` (`CHtmlExporter.cs:128-151`) creates one converter, calls
`pdf.Dispose()` — which leaks — per report.

## Impact
Native library handles accumulate over the process lifetime. wkhtmltox is also notoriously
single-init; repeatedly newing `PdfTools` without deinit can cause instability or crashes on repeated
exports within one session. Memory/handle leak in a desktop tool that may run multiple reports.

## Suggested Fix
Implement `IDisposable` properly: hold the `PdfTools` reference and call `pdfTools.Dispose()` in
`Dispose()` (with a finalizer/`GC.SuppressFinalize` as appropriate). Preferably create a single
shared/static `SynchronizedConverter` for the process lifetime (the recommended DinkToPdf pattern) and
dispose it once at shutdown rather than per report.

## Labels
resource-leak, native-interop, pdf, dispose
