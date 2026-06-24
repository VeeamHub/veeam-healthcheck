---
title: "Replace positional [Index] CSV mapping with header-name mapping — 27 classes silently corrupt on column reorder"
severity: High
labels: [bug, reliability, maintainability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CJobCsvInfos.cs:11
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CSobrExtentCsvInfos.cs:96
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CServerCsvInfos.cs:10
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/Tape/CTapeJobInfo.cs:12
confidence: High
---

## Summary

27 CSV record classes map every property with explicit `[Index(N)]` attributes. In CsvHelper, an explicitly set index (with no explicit `Name`) makes the field read **positionally, ignoring the header row** — the carefully configured `PrepareHeaderForMatch` normalization in `CCsvReader.GetCsvConfig()` does nothing for these classes. That this is positional in practice is proven by `CSobrExtentCsvInfos`, where headers like `"Options(maxtasks)"` map to a property named `MaxTasks` — a name match is impossible, only the index works.

The CSVs are produced by PowerShell collectors that pipe raw cmdlet objects to `Export-Csv` (e.g. `Get-VBRNetworkTrafficRule | Export-VhciCsv`), so the column order is whatever property order the Veeam PowerShell module emits for that VBR version — it is not pinned by the scripts.

## Impact

If a collector script (or a new Veeam PS module version, for raw-object exports) inserts, removes, or reorders a column, every subsequent field shifts into the wrong property. Because almost all properties are `string`, no conversion error occurs — the report silently shows wrong values in wrong columns (e.g., a repo's `Description` rendered as its `Path`). This is especially dangerous for the **import** workflow, where CSVs collected by an older/newer tool version are re-parsed by the current binary. The trailing `[Optional]` attributes (`CJobCsvInfos.cs:117-151`, `CJobSessionCsvInfos.cs:61-71`) only protect against *truncation* at the tail, not reordering or mid-row insertion.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CJobCsvInfos.cs:11-51` (44 indexed columns):

```csharp
[Index(0)]
public string Name { get; set; }
...
[Index(13)]
public double OriginalSize { get; set; }
```

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CSobrExtentCsvInfos.cs:10` documents headers (`"Options(maxtasks)"`, `"SOBR_Name"`, ...) that cannot match property names — confirming the mapping is purely positional.

Affected files (all under `vHC/HC_Reporting/Functions/Reporting/`):

- CsvHandlers/: `CViProtected.cs`, `CEntraObjects.cs`, `CJobSessionCsvInfos.cs`, `CNetTrafficRulesCsv.cs`, `CJobCsvInfos.cs`, `CConfigBackupCsv.cs`, `CPluginCsvInfo.cs`, `CBnRCsvInfo.cs`, `CBjobCsv.cs`, `CProxyCsvInfos.cs`, `CFileProxyCsvInfo.cs`, `CArchiveTierCsv.cs`, `CSobrExtentCsvInfos.cs`, `CRequirementsCsvInfo.cs`, `CSobrCsvInfo.cs`, `CMalwareObject.cs` (`CMalwareExcludedItem`, `CMalwareInfectedObjects`, `CMalwareEvents`), `CHvProxyCsvInfo.cs`, `CServerCsvInfos.cs`, `CRegOptionsCsv.cs`, `CWanCsvInfos.cs`, `CCdpProxyCsvInfo.cs`, `CRepoCsvInfos.cs`, `CWaitsCsv.cs`, `CCapTierCsv.cs`
- DataTypes/NAS/: `CObjectShareVmcInfo.cs`, `CNasFileDataVmc.cs`
- DataTypes/Tape/: `CTapeJobInfo.cs`

Positive counter-examples already in the codebase: `CComplianceCatalogCsv.cs`/`CComplianceMetaCsv.cs` use `[Name("...")]`, and `CMalwareObjectMap` uses a `ClassMap` with `.Name(...)` + `.Optional()` — robust against reordering.

## Suggested fix

Migrate the indexed classes to `[Name("header")]` (or `ClassMap` with `.Name(...).Optional()` where headers contain decorations like `Options(maxtasks)` — the existing `PrepareHeaderForMatch` already strips spaces/parens/dots, so `[Name("Options(maxtasks)")]` or even matching on the normalized name works). Pin column sets explicitly in the collection scripts with `Select-Object` so the contract is owned on both sides. Do this opportunistically per file when touched; prioritize the high-traffic ones (`CJobCsvInfos`, `CJobSessionCsvInfos`, `CServerCsvInfos`, `CRepoCsvInfos`, `CSobrExtentCsvInfos`).
