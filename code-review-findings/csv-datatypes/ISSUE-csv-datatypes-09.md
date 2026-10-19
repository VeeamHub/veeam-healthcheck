---
title: "MatchRepoIdToRepo re-parses SOBR and Repo CSVs from disk for every plugin job row"
severity: Medium
labels: [performance, reliability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:561
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:552
confidence: High
---

## Summary

`MatchRepoIdToRepo` calls `this.SobrInfos()` and `this.RepoInfo()` — both of which open and fully re-parse their CSV files from disk and run the complete mapping pipeline (host matching, RAM/CPU lookup, provisioning calc) — and it is invoked **once per plugin job row** inside the `JobInfo()` loop (`CDataTypesParser.cs:552`).

There is also an initialization-order wrinkle: `JobInfo()` runs as the *first* step of `Init()` (line 66), so these nested `SobrInfos()`/`RepoInfo()` calls execute while `this.serverInfo` (populated at line 70) and `this.CapTierInfos` (line 77) are still empty — the very dependency the comment at lines 118-119 warns about ("CapTierCsvInfos() must be called before SobrInfos() in Init()"). The Id/Name lookup still works, but the derived fields are computed from empty inputs and the invariant is silently violated.

## Impact

- An environment with N plugin jobs performs 2·N full CSV parses of the SOBRs and Repositories files (each opening an undisposed file handle — compounds ISSUE-02). With large CSVs and many plugin jobs this is measurable report-generation slowdown and handle pressure.
- The hidden ordering dependency makes future refactors hazardous: anyone moving `JobInfo()` later or relying on `SobrInfos()` output inside this path inherits stale/empty cap-tier and server data.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:545-554` (caller, per-row):

```csharp
var rec = this.csvParser.PluginCsvParser()?.ToList() ?? new List<CPluginCsvInfo>();
if (rec != null)
    foreach (var r in rec)
    {
        CJobTypeInfos j = new();
        ...
        j.RepoName = this.MatchRepoIdToRepo(r.TargetRepositoryId);   // line 552
```

`CDataTypesParser.cs:561-582` (callee, re-parses CSVs each call):

```csharp
private string MatchRepoIdToRepo(string repoId)
{
    foreach (var e in this.SobrInfos())   // full CSV parse + mapping, every call
    ...
    foreach (var r in this.RepoInfo())    // full CSV parse + mapping, every call
```

## Suggested fix

Build the Id→Name dictionary once: either reorder `Init()` so SOBR/Repo lists are populated before `JobInfo()` and look up against `this.SobrInfo`/`this.RepoInfos`, or lazily cache `Dictionary<string,string> repoNamesById` on first use. Either change removes both the O(N×M) re-parsing and the implicit ordering dependency.
