---
title: "Null CSV fields used as Dictionary keys crash report generation (PreCalculations, JobSummaryInfoToXml)"
severity: Medium
labels: [bug, reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:1187
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:919
confidence: Medium
---

## Summary

Two transforms use raw CSV-derived strings as `Dictionary<string,...>` keys without null guarding:

1. `PreCalculations` (:1185-1196) calls `repoJobCount.ContainsKey(j.RepoName)` / `Add(j.RepoName, 1)`. If any job row has a null `RepoName` (jobs with no repository — e.g., replicas, or a missing CSV column), `ContainsKey(null)` throws `ArgumentNullException`. `PreCalculations` is invoked from `SobrInfoToXml` and `RepoInfoToXml` with no try/catch on the call path, so the SOBR/Repository tables — and depending on the caller's error handling, the report run — abort.
2. `JobSummaryInfoToXml` (:917-922) does `typeSummary.Add(type, ...)` for each `types2.Distinct()` value; a null `JobType` in any row produces `Add(null, ...)` → `ArgumentNullException`.

## Impact

A single job row with a missing repo/type field crashes whole report sections on a common path (job CSVs are mandatory inputs). This is exactly the "exceptions that abort the whole report" class.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:1185-1190` —

```csharp
foreach (var j in jobs)
{
    if (!repoJobCount.ContainsKey(j.RepoName))   // throws if RepoName == null
    {
        repoJobCount.Add(j.RepoName, 1);
    }
```

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:919-922` —

```csharp
foreach (var type in types2.Distinct())
{
    typeSummary.Add(type, types2.Count(x => x == type));   // throws if type == null
}
```

(Also note the redundant `TryGetValue` + indexer dance at :1193-1194 — `repoJobCount[j.RepoName]++` would do.)

## Suggested fix

Normalize keys first: `var repoName = j.RepoName ?? string.Empty;` (or skip rows with null keys, logged at Debug). In `JobSummaryInfoToXml`, filter or coalesce: `types2.Select(t => t ?? "Unknown")`. Consider `GroupBy` for both to simplify counting.
