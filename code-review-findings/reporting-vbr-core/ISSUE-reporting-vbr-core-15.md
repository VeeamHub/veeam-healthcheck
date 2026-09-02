---
title: "Dead code cluster in VBR report core: broken DivIdClass format string, no-op AddToHtml overload, unused proxy CSV loads, duplicated LoadCsvToMemory, empty classes"
severity: Low
labels: [maintainability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:612
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:628
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:1313
  - vHC/HC_Reporting/Functions/Reporting/Html/CBackupServerTableHelper.cs:247
  - vHC/HC_Reporting/Functions/Reporting/Html/CReportDataModel.cs:9
  - vHC/HC_Reporting/Functions/Reporting/Html/DataFormers/CMultiRoleServer.cs:5
  - vHC/HC_Reporting/Functions/Reporting/Html/ConcurentTracker.cs:7
confidence: High
---

## Summary

A cluster of dead/latent-bug code in the report core:

- `CHtmlCompiler.DivIdClass()` (:612-615): `return string.Format("<div id={0} class={1}");` — a format string with two placeholders and **no arguments**. Currently uncalled; the first caller gets a `FormatException` at runtime.
- `CHtmlCompiler.AddToHtml(string infoString, bool scrub)` (:628-630): empty body — silently discards whatever a caller passes. A booby trap next to the real `AddToHtml`.
- `CHtmlCompiler.ExportToPdf()` (:97-107): fully commented-out body.
- `CDataFormer.CheckProxyRole` (:1313-1368) is never called, yet the constructor (:71-74) still eagerly reads four proxy CSVs (`GetDynViProxy/HvProxy/NasProxy/CdpProxy`) solely to populate the fields it used — wasted I/O on every `CDataFormer` construction.
- `CBackupServerTableHelper.LoadCsvToMemory` (:247-307) is a verbatim, uncalled copy of `CHtmlCompiler.LoadCsvToMemory` (:357-421) — divergence risk for version-detection logic.
- `CReportDataModel` is an empty class; `CMultiRoleServer` is a comment-only stub; `ConcurentTracker` (misspelled, with `DayofTheWeeek`/`hourMinute` typo'd members) is a plain DTO whose name implies concurrency machinery that doesn't exist.
- Unused string-array scaffolding: `string[] s = new string[30]` in `SobrInfoToXml` (:442) and `string[] s = new string[18]` populated but discarded in `RepoInfoToXml` (:644, 672-677) — the values are written then never read.
- `CVbrSummaries.SummaryTemplate`, `MissingJobsSUmmary` (typo) — template/dead methods kept alongside live ones.

## Impact

No runtime impact today, but real traps (FormatException, content-discarding overload), wasted I/O per construction, and persistent reviewer noise.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:612-615` —

```csharp
private string DivIdClass()
{
    return string.Format("<div id={0} class={1}");   // FormatException if ever invoked
}
```

`CHtmlCompiler.cs:628-630` —

```csharp
private void AddToHtml(string infoString, bool scrub)
{
}
```

## Suggested fix

Delete `DivIdClass`, the empty `AddToHtml` overload, `ExportToPdf`, `CheckProxyRole` plus the four constructor CSV loads, the duplicated `LoadCsvToMemory`, the discarded `string[] s` blocks, `CReportDataModel`, and `CMultiRoleServer`. Rename `ConcurentTracker` → `ConcurrencyPoint` (fix member typos) when touching the concurrency code next.
