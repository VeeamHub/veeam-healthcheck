---
title: "Consolidate copy-pasted helpers drifting across table classes (SetSection, progress bar, BoolCell, totals block)"
severity: Low
labels: [maintainability]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:1600
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Repositories/CRepoTable.cs:185
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Proxies/CProxyTable.cs:150
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobSessionSummaryTable.cs:423
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummaryHelper.cs:114
confidence: High
---

## Summary

Several helpers were copy-pasted during the extraction of tables out of `CHtmlTables` and are now maintained in parallel. They are identical today, but this is the exact mechanism behind the drift already visible in this domain (TryGetValue fixed in one table, 22 left behind — ISSUE-02; InvariantCulture in some parsers, not others — ISSUE-04). Each duplicate is one future fix away from divergence.

## Impact

Bug fixes applied to one copy silently miss the others. Concretely: a styling/threshold change to one `RenderStorageProgressBar` desyncs repo vs. SOBR-extent storage bars; a fix to the totals block in `RenderByJob` won't reach `AddOffloadsTable`.

## Evidence

- `RenderStorageProgressBar(decimal)` — duplicated verbatim at `CHtmlTables.cs:1600-1607` and `Repositories/CRepoTable.cs:185-192`.
- `private static void SetSection(...)` JSON-capture helper — duplicated at `Repositories/CRepoTable.cs:194`, `Proxies/CProxyTable.cs:150`, `Managed Server Table/CManagedServerTable.cs:142`, while other tables correctly call the shared `CHtmlTables.SetSectionPublic` (`CHtmlTables.cs:392`).
- `BoolCell(bool)` — duplicated at `CHtmlTables.cs:410`, `CSectionTable.cs:98`, and `SOBR/CSobrTable.cs:115`.
- The ~90-line "render rows + accumulate totals + emit TOTALS row" block — duplicated between `Jobs Info/CJobSessionSummaryTable.cs:256-362` (`RenderByJob`) and `:450-540` (`AddOffloadsTable`), including the same 20-column row template that also appears a third time in `RenderFlat` (lines 98-124).
- `JobSessionInfoList()` (filter + double `OrderBy` + `Reverse`) — duplicated between `Job Session Summary/CJobSessSummaryHelper.cs:114-135` and `Job Session Summary/IndividualJobSessionsHelper.cs:25-44` (`ReturnJobSessionsList`). Both contain the same quirk: `OrderBy(x => x.Name)` is immediately discarded by the subsequent `OrderBy(y => y.CreationTime)` (should be `ThenBy` if name ordering was intended).

## Suggested fix

Move `RenderStorageProgressBar` and `BoolCell` into `CHtmlFormatting`; delete the three private `SetSection` copies in favor of `CHtmlTables.SetSectionPublic`; extract a `RenderJobSessionRow(CJobSummaryTypes)` + `JobSessionTotalsAccumulator` used by all three render paths; share one session-list provider between the two Job Session Summary helpers (and fix the discarded `OrderBy` while there). Migrating legacy tables to `CSectionTableBase<T>` (which already centralizes most of this) is the structural fix.
