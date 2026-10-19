---
title: "FileFinder picks an arbitrary CSV via recursive FirstOrDefault — wrong dataset in multi-collection import folders"
severity: Medium
labels: [bug, reliability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:43
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:58
confidence: Medium
---

## Summary

`CCsvReader.FileFinder` scans `outpath` with `SearchOption.AllDirectories` and takes `FirstOrDefault` on a suffix match. For live runs the path is already scoped to `{server}\{timestamp}` (`CVariables.GetVbrDirWithTimestamp`), so this is safe. But in **import mode** the path can fall back to the raw `CGlobals.IMPORT_PATH` (`CVariables.cs:65-69`, comment: "fall back to searching"). If that folder contains more than one collection — multiple timestamps, multiple servers, or both VBR trees — `FirstOrDefault` returns whichever file the OS enumerates first, with no determinism guarantee and no warning when multiple candidates exist.

Additionally, the whole method is wrapped in `catch (Exception e)` that logs only `e.Message` and returns `null` (`CCsvReader.cs:58-62`), so I/O problems (access denied, path too long) are indistinguishable from "file not found" to every caller.

## Impact

A report generated from an import directory containing two collections can silently mix data: `..._Jobs.csv` from one timestamp folder and `..._Servers.csv` from another (per-token enumeration order can differ), or report on an older snapshot than the user intended. There is no log line indicating multiple matches were found.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:39-62`:

```csharp
public CsvReader FileFinder(string token, string outpath)
{
    try
    {
        var files = Directory.GetFiles(outpath, "*.csv", SearchOption.AllDirectories);

        string wanted1 = "_" + token + ".csv";   // localhost_Servers.csv
        string wanted2 = token + ".csv";         // Servers.csv (if ever)

        var match = files.FirstOrDefault(p =>
            p.EndsWith(wanted1, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(p), wanted2, StringComparison.OrdinalIgnoreCase));
        ...
    }
    catch (Exception e)
    {
        this.log.Error($"File or Directory {outpath} not found!\n{e.Message}");
        return null;
    }
}
```

A related swallow exists in `CCsvParser.ResolveServerNameFromCsvDir` (`CCsvParser.cs:646-649`): `catch (Exception) { return null; }` with no logging at all.

## Suggested fix

- Collect all matches; if more than one, prefer the newest (`File.GetLastWriteTimeUtc` or the timestamp folder name) and log a warning naming the chosen file and the ignored ones.
- Cache the resolved collection root after the first match so every token resolves within the same `{server}\{timestamp}` subtree, preventing cross-collection mixing.
- Log the full exception (`e`) and distinguish "directory missing" from other I/O failures.
