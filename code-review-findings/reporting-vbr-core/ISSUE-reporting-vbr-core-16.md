---
title: "Culture-sensitive decimal ToString leaks locale decimal separators into report and JSON export"
severity: Low
labels: [bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:659
  - vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:216
confidence: Medium
---

## Summary

`RepoInfoToXml` and friends convert computed decimals with bare `ToString()`: `free.ToString()`, `total.ToString()`, `freePercent.ToString()` (CDataFormer.cs:659-661), `FilterZeros` (:1166-1177). On a machine with a comma decimal separator (de-DE, fr-FR, ru-RU — common for VBR servers), repo free-space renders as `1,5` instead of `1.5`. These same string rows are captured into `CGlobals.FullReportJson` sections (e.g., `CRepoTable` serializes `d.FreeSpace.ToString()` into `Rows`) and written by `JsonSerializer` in `ExportJsonReport` (CHtmlExporter.cs:216) — so the machine-readable JSON contains locale-dependent numbers that downstream consumers (the VHC Intelligence Portal ingest path) must guess at.

Note: the csproj suppresses CA1305/CA1307 globally, so the analyzer won't surface these; that suppression makes the JSON case easy to miss.

## Impact

- HTML report: cosmetic inconsistency between locales.
- JSON export: `"1,5"` is not parseable as a number with invariant parsing — silent data corruption for any automated consumer of the JSON sections.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:659-661` —

```csharp
string freeSpace = free.ToString();
string totalSpace = total.ToString();
string percentFree = freePercent.ToString();
```

`vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:216` — `JsonSerializer.Serialize(CGlobals.FullReportJson, options)` serializes those pre-stringified rows verbatim.

## Suggested fix

Use `ToString(CultureInfo.InvariantCulture)` for all numeric values destined for the JSON sections (or better: keep them numeric in `CFullReportJson` and let `JsonSerializer` format them). For the HTML, pick one culture deliberately (invariant for consistency across shared reports).
