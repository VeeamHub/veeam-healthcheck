# Quadratic string concatenation building VB365 tables and the whole document

**Category:** reporting-vb365
**Severity:** Low
**Type:** Performance
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs` (every method, e.g. `1025-1078`, `1080-1132`, `1440-1528`); `CVb365HtmlCompiler.cs:43-94`

## Summary
The entire VB365 report is assembled with repeated `string += ...` against growing `string` locals/fields. Each table method accumulates rows into `string s` inside a `foreach`, and the compiler accumulates every section into `this.htmldoc += ...`. Because .NET strings are immutable, every `+=` allocates a new string and copies the entire accumulated content so far — O(n²) in total output size. For large tenants the row-heavy sections (Job Sessions, Processing Statistics, Jobs, and the per-user Protection Statistics scan) make this measurably slow and allocation-heavy.

## Evidence
Per-row concatenation in a loop (`CM365Tables.cs:1046-1066`, Job Sessions):
```csharp
foreach (var gl in global)
{
    s += "<tr>";
    foreach (var g in gl)
    {
        ...
        s += this.form.TableData(output, string.Empty);
    }
    s += "</tr>";
}
```
Document-level accumulation (`CVb365HtmlCompiler.cs:61-86`):
```csharp
this.htmldoc += tables.Globals();
this.htmldoc += tables.Vb365ProtStat();
this.htmldoc += tables.Vb365Controllers();
... // ~15 more, each appending a large string
```
The shared `CHtmlBuilder` (used on the newer VBR path) already uses a `StringBuilder` — the VB365 path predates it.

## Impact
Excess CPU and GC pressure proportional to the square of report size; noticeable on VB365 environments with many jobs/sessions/users. Functionally correct, but slow and wasteful.

## Suggested Fix
Replace the `string s` accumulators with `System.Text.StringBuilder` inside each table method (and ideally accumulate the document in a `StringBuilder`), or migrate the VB365 tables onto the existing `CHtmlBuilder`. Return `sb.ToString()` once at the end.

## Labels
performance, allocation, string-builder, vb365, reporting
