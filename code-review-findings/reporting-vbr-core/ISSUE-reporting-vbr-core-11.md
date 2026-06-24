---
title: "PPTX export: fragile signature-based dedup can drop distinct tables; failure leaves a corrupt partial .pptx"
severity: Medium
labels: [bug, reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs:1490
  - vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs:55
confidence: Medium
---

## Summary

`ParseHtmlTables` runs three overlapping regex extraction passes (content-table class, card divs, collapsible buttons) and de-duplicates with the signature `$"{Title}_{ColumnCount}_{RowCount}"`:

1. **False dedup:** two genuinely different tables that happen to share a title and shape are silently dropped (e.g., Method 1 falls back to `title = "Data"` whenever the preceding 500-char window has no `id=`, so any two unidentified tables with equal dimensions collapse into one).
2. **False duplicates:** the same table found by two methods under *different* titles (card id vs. button caption) produces two signatures → duplicate slides.
3. Regex parsing of `<table[^>]*>(.*?)</table>` with `Singleline` cannot handle nested tables and depends on attribute quoting/order; combined with finding 01 (unencoded data), a job name containing `</table>` truncates extraction.

Separately, `ConvertHtmlToPptx` creates the output file first (`PresentationDocument.Create(outputPath, ...)` inside `using`); if slide generation throws mid-way, the `using` disposes/saves a partially-built, typically unopenable `.pptx`, and the caller (`ExportHtmlStringToPptx`) logs the error but leaves the corrupt file in the output folder next to the good HTML.

## Impact

PPTX deck can silently omit report tables or duplicate them, and failed conversions leave corrupt artifacts that users will attempt to open/share.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs:1489-1495` —

```csharp
string signature = $"{tableData.Title}_{tableData.ColumnCount}_{tableData.Rows.Count}";
if (!processedTableSignatures.Contains(signature))
{
    processedTableSignatures.Add(signature);
    tables.Add(tableData);
}
```

`HtmlToPptxConverter.cs:1478-1483` — fallback title:

```csharp
string title = "Data";
var idMatch = Regex.Match(beforeTable, @"id=""([^""]+)""[^>]*>\s*$", RegexOptions.RightToLeft);
```

`HtmlToPptxConverter.cs:57` — `using (PresentationDocument presentationDocument = PresentationDocument.Create(outputPath, ...))` writes directly to the final path.

## Suggested fix

Key dedup on table *position* (`tableMatch.Index`) rather than title/shape. Prefer a single extraction pass over the known section-card structure (ids are deterministic — see `CHtmlBodyHelper`), or hand the converter structured data (`CGlobals.FullReportJson.Sections`) instead of re-scraping HTML. Build the PPTX to a temp file and move it into place only on success; delete on failure.
