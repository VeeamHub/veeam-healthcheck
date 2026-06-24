---
title: "Replace empty catch blocks in every CM365Tables section with logged, row-scoped handling"
severity: Medium
labels: [reliability, maintainability]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:127
confidence: High
---

## Summary

All 12 table builders in `CM365Tables` wrap their whole data loop in `try { ... } catch (Exception) { }` with an empty body — no logging whatsoever. Any error (missing CSV column → `RuntimeBinderException` on dynamic access, null field → NRE like `path.StartsWith("C:")` on a null `Path`, format change, etc.) aborts the section mid-row and the report ships with a silently empty or half-rendered table.

The VBR side logs in its catches (e.g. `CHtmlTables.cs:240`, `CBackupServerSection.cs:98`); the VB365 side drifted to fully silent.

## Impact

- Data-quality failures are invisible: sections just come out blank, and support cases become unreproducible ("the proxies table is empty") with nothing in the log.
- Because the exception aborts mid-loop, an already-appended `<tr>` is left unclosed (e.g. `Vb365Repos` appends `<tr>` at line 374 before touching nullable fields at line 389), producing malformed HTML.

## Evidence

Empty catches at `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:127, 287, 335, 454, 503, 707, 887, 1014, 1069, 1123, 1182, 1315, 1379, 1429, 1519, 1687` — all of the form:

```csharp
catch (Exception)
{
}
```

Example null-crash path inside one: `CM365Tables.cs:389` `if (path.StartsWith("C:"))` — `Path` can be null via `MissingFieldFound = null` (`CCsvReader.cs:85`), killing the whole Repositories table.

## Suggested fix

At minimum log `CGlobals.Logger.Error("[VB365][HTML] <section> failed: " + ex)` in each catch. Preferably move the try inside the row loop (`catch` per row: log, close the `<tr>`, `continue`) so one bad row doesn't erase the section — mirroring the per-row pattern already used on the VBR side (`CHtmlTables.cs:234`).
