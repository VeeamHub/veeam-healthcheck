---
title: "CCsvsInMemory reports load errors to Console instead of the logger and silently drops bad CSV data"
severity: Low
labels: [reliability, maintainability]
domain: collection-data
files:
  - vHC/HC_Reporting/Common/CCsvsInMemory.cs:31
  - vHC/HC_Reporting/Common/CCsvsInMemory.cs:39
  - vHC/HC_Reporting/Common/CCsvsInMemory.cs:72
confidence: High
---

## Summary

`CCsvsInMemory` is the shared in-memory CSV cache, but all of its diagnostics (`file not found`, `empty file`, `Error loading CSV ...`) go to `Console.WriteLine` rather than `CGlobals.Logger`. In GUI mode (WPF, `CreateNoWindow`) there is no console, so these messages are lost entirely — a failed load surfaces only as a `false`/`null` return that callers may not check. The CsvHelper config also sets `BadDataFound = null`, which silently skips malformed fields with no count or warning.

Minor: the cache is unbounded and keyed by raw `filePath` (no normalization — the same file loaded via different path casing/format is cached twice), and rows are duplicated per-row `Dictionary<string,string>` allocations, which is memory-heavy for large environments. `Clear()` exists but eviction is manual.

## Impact

CSV load failures and malformed rows in collected data are invisible in the vHC log file, which is the primary support artifact. Reports rendered from partially-loaded CSVs look complete.

## Evidence

`vHC/HC_Reporting/Common/CCsvsInMemory.cs:70-74` —

```csharp
catch (Exception ex)
{
    Console.WriteLine($"Error loading CSV {filePath}: {ex.Message}");
    return false;
}
```

`vHC/HC_Reporting/Common/CCsvsInMemory.cs:35-40` —

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    MissingFieldFound = null,
    HeaderValidated = null,
    BadDataFound = null,   // malformed data silently ignored
};
```

## Suggested fix

Route messages through `CGlobals.Logger` (Warning for not-found/empty, Error for exceptions). Replace `BadDataFound = null` with a handler that increments a per-file bad-row counter and logs a summary. Normalize the cache key with `Path.GetFullPath`.
