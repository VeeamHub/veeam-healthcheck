# MatchRepoIdToRepo re-parses Repo and SOBR CSVs from disk on every job row — O(n*m) full re-reads

**Category:** csv-datatypes
**Severity:** High
**Type:** Performance
**File(s):** `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:561-582` (called from `JobInfo()` at `:552`)

## Summary
`MatchRepoIdToRepo` calls `this.SobrInfos()` and `this.RepoInfo()` **inside the lookup**, and it is called once per plugin-job row in `JobInfo()`. Both `SobrInfos()` and `RepoInfo()` re-open and re-parse their CSV files (SOBRs, SOBRExtents/CapTier, Repositories) from disk every single call. For P plugin jobs this re-reads those CSVs P times instead of once.

## Evidence
```csharp
// CDataTypesParser.cs:561-582
private string MatchRepoIdToRepo(string repoId)
{
    foreach (var e in this.SobrInfos())   // re-parses SOBR + CapTier CSV every call
    {
        if (repoId == e.Id) return e.Name;
    }
    foreach (var r in this.RepoInfo())     // re-parses Repositories CSV every call
    {
        if (repoId == r.Id) return r.Name;
    }
    return string.Empty;
}
```
Call site, once per plugin job row:
```csharp
// JobInfo() :545-554
foreach (var r in rec)            // rec = plugin jobs
{
    ...
    j.RepoName = this.MatchRepoIdToRepo(r.TargetRepositoryId);  // full CSV re-parse x rows
}
```
Note the class already has materialized `this.RepoInfos` and `this.SobrInfo` lists populated in `Init()`, but `MatchRepoIdToRepo` ignores them and re-derives from scratch — and `SobrInfos()` itself also re-parses CapTier via the CSV path rather than reusing `this.CapTierInfos`.

## Impact
On environments with many plugin jobs and large repo/SOBR inventories, report generation does P repeated disk reads + parses of the same CSVs (combined with the undisposed-reader leak in ISSUE-01, also P leaked file handles). This is a quadratic blow-up in both I/O and allocations, directly slowing the report and amplifying the handle leak.

## Suggested Fix
Build the id→name maps once and reuse them. Since `Init()` already populates `this.RepoInfos`/`this.SobrInfo` before plugin jobs are processed (or can be reordered to), replace the method body with dictionary lookups over the already-materialized lists:
```csharp
private string MatchRepoIdToRepo(string repoId) =>
    _sobrById.TryGetValue(repoId, out var s) ? s :
    _repoById.TryGetValue(repoId, out var r) ? r : string.Empty;
```
Populate `_sobrById`/`_repoById` once from the cached lists.

## Labels
performance, repeated-file-read, quadratic, caching
