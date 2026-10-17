---
title: "Eliminate double data-load: every legacy renderer loads its dataset twice (HTML pass + JSON pass)"
severity: Medium
labels: [performance]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobSessionSummaryTable.cs:88
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CMissingJobsTable.cs:41
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:929
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Repositories/CRepoTable.cs:56
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Proxies/CProxyTable.cs:53
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Managed Server Table/CManagedServerTable.cs:55
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/SOBR/CSobrTable.cs:47
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/SOBR/CSobrExtentTable.cs:49
confidence: High
---

## Summary

The "JSON capture" block appended to each legacy renderer re-invokes the same data loader that the HTML pass just ran, instead of reusing the already-materialized list. Each loader re-reads and re-parses CSV files (and in some cases runs the full session-aggregation pipeline). The newer `CSectionTableBase<T>.Render` (`CSectionTable.cs:52-95`) gets this right — it loads once and feeds both HTML and JSON — so the eight legacy sites below are drift.

## Impact

Report generation does roughly 2x the I/O and CPU for these sections. The worst offenders are not trivial re-reads:

- `CJobSessionSummaryTable` calls `df.ConvertJobSessSummaryToXml(scrub)` twice per render method (lines 88/149 in `RenderFlat`, 193/386 in `RenderByJob`) — this is the entire job-session aggregation including per-job Waits-CSV scans (see ISSUE-08), the single most expensive computation in the report.
- `CMissingJobsTable` calls `st.JobSummaryTable()` twice (lines 41/65) — each call parses ~8 separate CSV files.
- `CHtmlTables.AddProtectedWorkLoadsTable` re-runs `NasTable()` and `EntraTable()` for JSON (lines 655/837 and 749/850).

There is also a correctness wrinkle: if the data source changes between passes (or the loader is non-deterministic), HTML and JSON exports can disagree.

## Evidence

`Repositories/CRepoTable.cs:56` then `:153`:

```csharp
List<CRepository> list = df.RepoInfoToXml(scrub);      // HTML pass
...
var list = df.RepoInfoToXml(scrub) ?? new List<CRepository>();  // JSON pass, full reload
```

Same pattern: `CHtmlTables.cs:929/1026` (`ExtentXmlFromCsv`), `CHtmlTables.cs:1093/1121` and `1153/1181` (`JobConcurrency`), `Proxies/CProxyTable.cs:53/132`, `Managed Server Table/CManagedServerTable.cs:55/113`, `SOBR/CSobrTable.cs:47/86`, `SOBR/CSobrExtentTable.cs:49/109`, `Jobs Info/CJobSessionSummaryTable.cs:88/149/193/386`, `CMissingJobsTable.cs:41/65`.

## Suggested fix

Hoist the loaded list to a local that both the HTML loop and the JSON capture share (the pattern `CSectionTableBase<T>` already uses), or migrate these tables onto `CSectionTableBase<T>`. No behavior change expected; verify HTML and JSON output are byte-identical before/after.
