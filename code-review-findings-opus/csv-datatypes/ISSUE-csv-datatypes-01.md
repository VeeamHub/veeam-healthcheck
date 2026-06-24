# CsvReader / StreamReader never disposed — file-handle leak on every CSV read

**Category:** csv-datatypes
**Severity:** High
**Type:** Resource Leak
**File(s):** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65-70`, `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs` (all `GetRecords` call sites, e.g. `:165, :578, :659, :943`)

## Summary
`CReader` opens a `StreamReader` over the file and wraps it in a CsvHelper `CsvReader`, then returns the `CsvReader` to the caller. Neither the `StreamReader` nor the `CsvReader` is ever disposed anywhere in the codebase. Every CSV parse leaks an open file handle plus the underlying stream until GC eventually finalizes it (and `StreamReader` has no finalizer that closes the OS handle deterministically — the `FileStream` does, but only on GC). With ~80 distinct CSV parser methods invoked per report run, this is a systemic leak.

## Evidence
```csharp
// CCsvReader.cs:65-70
private CsvReader CReader(string csvToRead)
{
    TextReader reader = new StreamReader(csvToRead);   // never disposed
    var csvReader = new CsvReader(reader, this.csvConfig);
    return csvReader;                                   // handed off; no using/Dispose
}
```
The returned `CsvReader` is consumed like this, with no `using` and no `Dispose`:
```csharp
// CCsvParser.cs:572-583  (representative of ~80 methods)
public IEnumerable<CJobSessionCsvInfos> SessionCsvParser()
{
    var res = this.VbrFileReader(this.sessionPath);
    if (res != null)
        return res.GetRecords<CJobSessionCsvInfos>();   // lazy; reader leaks
    return Enumerable.Empty<CJobSessionCsvInfos>();
}
```
`CCsvParser.Dispose()` (CCsvParser.cs:1138) is an empty no-op, and `CDataTypesParser.Dispose()` (CDataTypesParser.cs:102) is likewise empty — so even the `IDisposable` surface does not close anything.

## Impact
- Open file handles accumulate for the duration of a report run. On large environments (hundreds of CSVs across servers/timestamps) this can exhaust handles or, more commonly, hold locks on the CSV files in `C:\temp\vHC\...` so that cleanup/scrub/delete steps fail with "file in use."
- Non-deterministic release means the imported CSV directory can't be reliably deleted or re-collected in the same process.

## Suggested Fix
Make the reader lifetime explicit. Either:
1. Have parser methods materialize inside a `using` and return a `List<T>`:
```csharp
using var reader = new StreamReader(csvToRead);
using var csv = new CsvReader(reader, this.csvConfig);
return csv.GetRecords<T>().ToList();
```
2. Or return an `IDisposable` wrapper and have every caller `using` it.
Because `GetRecords<T>()` is lazy, option 1 (materialize-then-dispose) is the safe, minimal change. Note several methods already call `.ToList()` but still never dispose the reader — those should move the read inside a `using`.

## Labels
resource-leak, csv, file-handle, idisposable
