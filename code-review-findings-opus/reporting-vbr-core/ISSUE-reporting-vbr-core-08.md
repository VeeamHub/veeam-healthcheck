# PPTX export parses report HTML with regex, mis-handling nested tables and producing wrong/duplicated slides

**Category:** reporting-vbr-core
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs:1460-1576,1617-1665`

## Summary
`ParseHtmlTables` reconstructs report tables from the generated HTML string using regexes like
`<table[^>]*>(.*?)</table>` with `RegexOptions.Singleline`. HTML tables are not a regular language; the
non-greedy `.*?` closes at the **first** `</table>`, so any nested table (e.g. the SQL example table
embedded inside a summary cell in `CVbrSummaries.JobCon`) is truncated and the outer rows after it are
dropped. The three overlapping discovery "methods" then re-scan the same HTML and rely on a
`Title_ColumnCount_RowCount` signature for dedup, which both misses genuinely-distinct same-shaped
tables and can merge unrelated ones.

## Evidence
```csharp
// HtmlToPptxConverter.cs:1516-1517
var tableMatch = Regex.Match(afterDiv, @"<table[^>]*>(.*?)</table>",
    RegexOptions.Singleline | RegexOptions.IgnoreCase);   // stops at first </table>, breaks on nesting

// HtmlToPptxConverter.cs:1490 / 1526 / 1565 — weak dedup key
string signature = $"{tableData.Title}_{tableData.ColumnCount}_{tableData.Rows.Count}";
```
`ParseTable` similarly splits rows with `<tr[^>]*>(.*?)</tr>` and cells with `<t[dh]...>(.*?)</t[dh]>`,
which mis-associates cells when a cell itself contains a nested table or `<td>` markup.

## Impact
PPTX export (best-effort, `!scrub` only) can silently drop rows, split a table at a nested table, or
duplicate/merge sections. The slide deck does not faithfully represent the report. Severity is Low
because PPTX is a secondary, opt-in (`CGlobals.EXPORTPPTX`) format and the HTML/PDF outputs are
unaffected.

## Suggested Fix
Drive the PPTX exporter from the structured report model rather than re-parsing emitted HTML — the data
already exists as typed objects in `CDataFormer`/the table builders before HTML stringification. If
HTML must be parsed, use a real HTML parser (e.g. AngleSharp / HtmlAgilityPack, both compatible with the
existing HtmlToOpenXml dependency stack) instead of regex.

## Labels
bug, pptx, html-parsing, regex, secondary-format
