---
title: "PDF export failure marks the whole HTML export as failed; converter never disposed on throw"
severity: Medium
labels: [reliability, bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:104
  - vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:126
confidence: High
---

## Summary

`ExportVbrHtml` wraps HTML write, JSON export, PDF export, PPTX export, and browser-open in a single try/catch that returns 1 on any failure. PPTX export has its own internal try/catch (`ExportHtmlStringToPptx`, :157-179), but PDF export does not: any exception from `HtmlToPdfConverter.ConvertHtmlToPdf` (which deliberately *throws* on timeout and conversion error) propagates to the outer catch, so a PDF hiccup makes the run report "Failed at HTML Export" even though the HTML file was already written successfully. Additionally, `pdf.Dispose()` is only called on the success path — on throw, the converter (and its native wkhtmltopdf state) is never released.

## Impact

- A wkhtmltopdf failure or 5-minute timeout converts a successful HTML report into a reported failure (`RunFullVbrReport` logs "Init full report...failed!"), and skips `OpenHtmlIfEnabled`.
- Outer catch logs only `e.Message`, no stack trace, so the actual PDF failure cause is hard to diagnose.
- Asymmetric design: PPTX failures are isolated and logged; PDF failures abort the remaining export steps.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:104-123` —

```csharp
// test export to PDF:
if (!scrub && CGlobals.EXPORTPDF)
{
    this.ExportHtmlStringToPDF(htmlString);   // throws on timeout/conversion error
}
...
catch (Exception e)
{
    this.log.Error("Failed at HTML Export:");
    this.log.Error("\t" + e.Message); return 1;
}
```

`vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:150-151` —

```csharp
pdf.ConvertHtmlToPdf(htmlShowAll, this.latestReport.Replace(".html", ".pdf"));
pdf.Dispose();   // skipped entirely if the line above throws
```

## Suggested fix

Mirror the PPTX pattern: wrap `ExportHtmlStringToPDF` in its own try/catch that logs (message + stack) and continues, and use `try/finally` (or make `HtmlToPdfConverter` implement `IDisposable` and use `using`) so disposal always runs. Reserve the return-1 path for failures of the HTML file itself.
