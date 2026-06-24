# JobSessionInfo uses SingleOrDefault on duplicate job names — throws and aborts, swallowed per row

**Category:** csv-datatypes
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:647-659`

## Summary
For each session row, `JobSessionInfo()` looks up the matching job with `jobRecords.Where(x => x.Name == s.JobName).SingleOrDefault()`. `SingleOrDefault` throws `InvalidOperationException` if more than one job shares the same name. Veeam job names are not guaranteed unique across job types (and aren't unique in imported/merged datasets), so a duplicate name makes the lookup throw. It's caught by the local `try/catch`, but the result is that `UsedVmSize` is silently left unset for that session and an error is logged per occurrence.

## Evidence
```csharp
// CDataTypesParser.cs:647-659
try
{
    var jobFromCsv = jobRecords.Where(x => x.Name == s.JobName).SingleOrDefault();
    if (jobFromCsv != null)
        jInfo.UsedVmSize = jobFromCsv.OriginalSize /1024 /1024 /1024;
}
catch (Exception e)
{
    this.log.Error("Failed to parse job original size");
    this.log.Error("\t" + e.ToString());
}
```
This runs once per session row, and `jobRecords` is also re-filtered linearly each time (O(sessions * jobs)).

## Impact
- Duplicate job names cause `UsedVmSize` to be wrong/zero for affected sessions, and spam the log with stack traces.
- Combined with the per-row linear `Where`, large session/job sets get an O(n*m) scan on top.

## Suggested Fix
Use `FirstOrDefault` (intent is "find a matching job", not "assert uniqueness"), and precompute a `Dictionary<string, CJobCsvInfos>` (or `ToLookup`) of jobs by name once before the loop:
```csharp
var jobsByName = jobRecords
    .GroupBy(x => x.Name)
    .ToDictionary(g => g.Key, g => g.First());
...
if (jobsByName.TryGetValue(s.JobName, out var jobFromCsv))
    jInfo.UsedVmSize = jobFromCsv.OriginalSize / 1024d / 1024d / 1024d;
```

## Labels
single-or-default, exception, performance, lookup
