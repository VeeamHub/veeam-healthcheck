# Tables built by repeated string `+=` concatenation in row loops instead of StringBuilder

**Category:** reporting-vbr-tables
**Severity:** Low
**Type:** Performance
**File(s):** ~35 renderers in `Functions/Reporting/Html/VBR/VbrTables/`. Representative: `Repositories/CRepoTable.cs:33-148` (`string s ... s += ...` per cell, ~17 cells x N rows); `Managed Server Table/CManagedServerTable.cs:34-100`; `SOBR/CSobrExtentTable.cs:24-95`; `Proxies/CProxyTable.cs:33-115`; `Security/CMalwareTable.cs` (multiple methods); `Jobs Info/CJobInfoTable.cs` (`row += ...` then `s += row`).

## Summary
Each renderer accumulates its HTML into a `string` with `+=` inside the per-row (and per-cell) loop. In .NET, `string` is immutable, so every `+=` allocates a new string and copies the entire accumulated report-section so far. For wide tables (16-17 columns) over thousands of rows this is O(rows^2 x width) allocation/copy.

## Evidence
`CRepoTable.cs` builds `string s` and does `s += form.TableData(...)` ~17 times per row inside `foreach (var d in list)` (lines 72-139), each `+=` reallocating the growing string. The same idiom repeats in essentially every renderer in the folder (35 files use a `string s/t` accumulator with `+=` inside a `foreach`).

## Impact
On large environments (the exact case where the health check matters most), report generation does quadratic string work per section. Memory churn and GC pressure scale poorly; multiple large sections compound it. Pure performance — output is correct.

## Suggested Fix
Use a `StringBuilder` per section (or per row, appended once):
```csharp
var sb = new StringBuilder();
sb.Append(form.SectionStartWithButton(...));
foreach (var d in list)
{
    sb.Append("<tr>");
    sb.Append(form.TableData(d.Name, string.Empty));
    ...
    sb.Append("</tr>");
}
return sb.ToString();
```
Given the count of affected files, consider having the `CHtmlFormatting` helpers write into a passed `StringBuilder`, or add row-builder overloads, to make the safe pattern the path of least resistance.

## Labels
performance, stringbuilder, allocations, scalability
