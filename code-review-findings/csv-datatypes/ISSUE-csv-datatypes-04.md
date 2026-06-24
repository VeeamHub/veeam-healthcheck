---
title: "Move JobInfo() CSV materialization inside try and stop swallowing row errors with empty catch"
severity: High
labels: [bug, reliability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:500
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:543
confidence: High
---

## Summary

`JobInfo()` has two compounding error-handling defects:

1. The CSV is materialized **outside** the try block (`CDataTypesParser.cs:500`), so any CsvHelper conversion failure escapes `JobInfo()` entirely and is caught only by `Init()`'s outermost catch-all — which aborts *all remaining parsing* (sessions, servers, proxies, SOBRs, repos...) for the run (see ISSUE-05).
2. The row-processing loop is wrapped in `catch (Exception) { }` (`CDataTypesParser.cs:543`) — completely silent. Any mid-loop failure truncates the job list with no log entry; the report then under-reports jobs with zero diagnostic trail.

## Impact

`CJobCsvInfos` has non-nullable typed properties (`double OriginalSize` Index 13, `bool TransformFullToSyntethic` Index 9, `bool EnableFullBackup`, `bool GfsWeeklyIsEnabled`, etc. — CJobCsvInfos.cs:39-109). A single row with an empty/locale-formatted value in any of these makes CsvHelper's converter throw during `.ToList()`:

- Empty `OriginalSize` field → `TypeConverterException` → entire `Init()` aborted → effectively empty report.
- Failure later (e.g., `s.PwdKeyId` access pattern changes) inside the loop → silent truncation of `eInfoList`.

The null check at line 504 (`if (jobCsv != null)`) is also dead — `.ToList()` at line 500 either throws or returns non-null.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:498-543`:

```csharp
this.log.Info("Starting Job Csv Parse..");

var jobCsv = this.csvParser.JobCsvParser().ToList();   // line 500: throws OUTSIDE try
List<CJobTypeInfos> eInfoList = new();
try
{
    if (jobCsv != null)        // dead check
        foreach (var s in jobCsv)
        {
            ...
        }
}
catch (Exception) { }          // line 543: silent swallow, partial data
```

## Suggested fix

- Move the `JobCsvParser().ToList()` call inside the try (or its own try) and log the failure with the file name.
- Replace the blanket silent catch with per-row try/catch that logs the row index/job name and continues, so one bad row costs one row, not the rest of the file:

```csharp
foreach (var s in jobCsv)
{
    try { eInfoList.Add(MapJob(s)); }
    catch (Exception ex) { this.log.Error($"Skipping job row '{s?.Name}': {ex.Message}"); }
}
```

- Consider making the fragile typed columns nullable (`double?`, `bool?`) or strings + safe parse, consistent with the rest of the class.
