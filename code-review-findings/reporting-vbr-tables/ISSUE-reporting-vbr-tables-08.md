---
title: "Remove O(jobs x sessions) re-parsing inside aggregation loops (Waits CSV, session list, NAS CSV)"
severity: Medium
labels: [performance]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummaryHelper.cs:57
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummary.cs:95
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobInfoTable.cs:119
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Concurrency Tables/CConcurrencyHelper.cs:153
confidence: High
---

## Summary

Several aggregation paths re-read files or re-enumerate the full dataset once **per job/group**, making report generation quadratic in environment size. In large environments (thousands of jobs, tens of thousands of sessions in the report window) these dominate runtime.

## Impact

Slow report generation that scales quadratically; for the job-session summary the cost is `O(groups x sessions)` enumeration plus `O(groups)` full re-reads of the Waits CSV — multiplied again by the double-load issue (ISSUE-07).

## Evidence

1. `Job Session Summary/CJobSessSummaryHelper.cs:57-64` — `GetWaitTimes(jobName)` is called once per job group (via `SetWaitInfo` from `CJobSessSummary.cs:102`) and re-reads the entire Waits CSV from disk every time:

```csharp
private List<TimeSpan> GetWaitTimes(string jobName)
{
    CCsvParser csv = new();
    var rawCsv = csv.WaitsCsvReader();
    if (rawCsv != null) { waitList = rawCsv.ToList(); }
    ...
}
```

2. `CJobSessSummaryHelper.cs:162` — `SessionStats(IEnumerable<string>)` runs `foreach (var session in this.JobSessionInfoList())` per group; `JobSessionInfoList()` (line 114) re-filters and double-sorts the full session list each call. Called once per job group from `CJobSessSummary.cs:112`.

3. `CJobSessSummary.cs:127-128` — per group, a fresh `CCsvParser` re-parses the entire Jobs CSV just to find one row: `jobInfo = csv.JobCsvParser().Where(x => x.Name == j).FirstOrDefault();`

4. `Jobs Info/CJobInfoTable.cs:119` — inside the per-job row loop: `var x = csvparser.GetDynamicNasBackup().ToList();` — re-reads the NAS backup CSV from disk for every NAS job row.

5. `Concurrency Tables/CConcurrencyHelper.cs:153-205` — `ConcurrencyDictionary` is a nested `foreach` over the tracker list (O(n^2)) plus per-minute inner loops; `JobCounter` (lines 80, 100, 127, 142) dedupes with `List<string>.Contains` (O(n) each, O(n^2) total).

## Suggested fix

Load once, index by key: read the Waits CSV once and build a `Dictionary<string, List<TimeSpan>>` keyed by job name; pass the already-loaded session list into `SessionStats`; build a `Dictionary<string, CJobCsvInfos>` from one `JobCsvParser()` call; hoist `GetDynamicNasBackup().ToList()` above the row loop; replace `nameDatesList` with a `HashSet<string>`; group trackers by `DayOfWeek` once instead of the nested scan.
