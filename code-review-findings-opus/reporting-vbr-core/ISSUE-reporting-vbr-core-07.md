# O(n^2) string concatenation building the full VBR report in memory

**Category:** reporting-vbr-core
**Severity:** Medium
**Type:** Performance
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs:34,145-421`; `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:184-186,622-626`

## Summary
The entire report is assembled by repeatedly concatenating onto immutable `string` fields
(`this.HTMLSTRING += ...` for ~30 sections, plus `htmldocOriginal += ...` / `htmldocScrubbed += ...` in
the compiler and `AddToHtml`). Each `+=` reallocates and copies the whole accumulated document. For a
large environment the document is multiple MB, so total work is quadratic in document size, and peak
memory holds several full copies at once.

## Evidence
```csharp
// CHtmlBodyHelper.cs:34 then 145..421 — every section appends to one growing string
this.HTMLSTRING = htmlString;
...
private void LicenseTable()      { this.HTMLSTRING += this.tables.LicTable(this.SCRUB); }
private void BackupServerTable() { this.HTMLSTRING += this.tables.AddBkpSrvTable(this.SCRUB); }
// ...~30 more "this.HTMLSTRING += ..." calls...

// CHtmlCompiler.cs:622-626
private void AddToHtml(string infoString)
{
    this.htmldocOriginal += infoString;
    this.htmldocScrubbed += infoString;
}
```
Within individual table builders the same `s += ...` per-row pattern compounds this (those builders are
under VbrTables/ and out of scope here, but the top-level accumulation is in-scope).

## Impact
Report generation time grows quadratically and memory churns badly on large VBR estates (the exact case
where Health Check is most valuable). Noticeable slowdowns and GC pressure for big customers.

## Suggested Fix
Accumulate into a single `StringBuilder` threaded through the body/compiler (or have each section return
a fragment appended to one builder) and materialize the string once at the end. The project already has
`CHtmlBuilder` (StringBuilder-backed) and `CSectionTable` using `StringBuilder` — extend that pattern up
to `CHtmlBodyHelper`/`CHtmlCompiler` so the whole pipeline is StringBuilder-based.

## Labels
performance, string-builder, scalability, reporting
