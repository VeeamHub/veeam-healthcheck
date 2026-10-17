# Broad catch blocks swallow CSV parse errors, masking malformed/truncated data as empty results

**Category:** csv-datatypes
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:496-543` (`JobInfo`), `:1120-1138` (`ParseToInt`/`ParseToDouble`), `:244-263` (`ParseBool`), `:58-100` (`Init`)

## Summary
Several layers swallow exceptions so that a malformed CSV silently yields partial or empty data with no surfaced error. The most damaging is `JobInfo()`: the entire per-row mapping loop is wrapped in `try { ... } catch (Exception) { }` with an empty body. If any row throws (e.g. a CsvHelper type-conversion error on the strongly-typed `double OriginalSize` / `bool TransformFullToSyntethic` fields), the loop aborts and **every job already not yet processed is dropped silently**, and the partial `eInfoList` is returned as if complete.

## Evidence
```csharp
// CDataTypesParser.cs:502-543
try
{
    if (jobCsv != null)
        foreach (var s in jobCsv)
        {
            ...
            jInfo.ActualSize = s.OriginalSize.ToString();
            eInfoList.Add(jInfo);
        }
}
catch (Exception) { }   // swallows everything; returns whatever was built so far
```
```csharp
// :244-263  empty-string on any bad bool, no log
private string ParseBool(string input)
{
    try { res = bool.Parse(input); ... }
    catch (Exception) { return string.Empty; }
}
```
```csharp
// :1120-1138  returns 0 on any failure, no log
int.TryParse(input, out int i); return i;
```
`Init()` (:95-99) also catches all exceptions and only logs, leaving every downstream list at its empty default if one parser throws early.

## Impact
A single bad/extra/missing column in `_Jobs.csv` (common across VBR version upgrades, given the heavy `[Index(n)]` positional mapping in `CJobCsvInfos`) silently truncates the jobs report rather than failing loudly or skipping the one bad row. Users get an incomplete report with no indication anything was dropped.

## Suggested Fix
- In `JobInfo()`, move the `try/catch` *inside* the loop so a single bad row is logged and skipped, not the whole remainder; log `s.Name` and the exception.
- In `ParseBool`/`ParseToInt`/`ParseToDouble`, log at Debug when `TryParse` fails so silent zeros are traceable.
- Avoid the catch-all in `Init()` swallowing without re-surfacing a user-visible warning that data is incomplete.

## Labels
exception-swallowing, silent-failure, data-loss, csv
