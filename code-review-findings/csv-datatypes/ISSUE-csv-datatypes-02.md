---
title: "Dispose CsvReader/StreamReader instances — every CSV parse leaks an open file handle"
severity: Medium
labels: [reliability, bug]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:1138
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:102
confidence: High
---

## Summary

`CCsvReader.CReader` opens a `StreamReader` and wraps it in a `CsvReader`; neither is ever disposed by anyone. Every one of the ~60 parser methods in `CCsvParser` opens a fresh reader, and most return the **lazy** `GetRecords<T>()` enumerable, so the underlying file stream's lifetime escapes the method entirely. The `Dispose()` methods that exist are empty no-ops.

## Impact

- Open file handles accumulate for the life of the process; files under `C:\temp\vHC\...` stay locked until GC finalization. Combined with `MatchRepoIdToRepo` re-parsing CSVs in a loop (see ISSUE-09), a single report run can hold dozens-to-hundreds of handles.
- Lazy `IEnumerable<T>` returns mean (a) parse exceptions surface at distant enumeration sites, far from the open call, and (b) the enumerable is single-pass — a second enumeration of the same returned value silently yields nothing/throws, which is a latent footgun for every caller.
- Subsequent operations that move/delete/re-zip the CSV directory can fail with sharing violations while undisposed streams are still alive.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65-70`:

```csharp
private CsvReader CReader(string csvToRead)
{
    TextReader reader = new StreamReader(csvToRead);
    var csvReader = new CsvReader(reader, this.csvConfig);
    return csvReader;        // neither reader is ever disposed
}
```

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:159-170` — lazy records escape with the live reader:

```csharp
private IEnumerable<dynamic> VbrGetDynamicCsvRecs(string file, string vbrOrVboPath)
{
    var res = this.VbrFileReader(file);
    if (res != null)
    {
        return res.GetRecords<dynamic>();   // deferred; reader never disposed
    }
    ...
```

`CCsvParser.cs:1138`: `public void Dispose() { }` — empty, and `CCsvParser` doesn't even implement `IDisposable`. `CDataTypesParser.cs:102`: `public void Dispose() { }` — implements `IDisposable` but disposes nothing.

## Suggested fix

Materialize and dispose at the source: have each parser method enumerate inside a `using` block and return a `List<T>`:

```csharp
using var csv = this.VbrFileReader(file);
return csv == null ? new List<T>() : csv.GetRecords<T>().ToList();
```

`CsvReader.Dispose()` disposes the wrapped `TextReader` by default. This also fixes the single-enumeration and late-exception problems. Remove the no-op `Dispose` methods or make them dispose real resources.
