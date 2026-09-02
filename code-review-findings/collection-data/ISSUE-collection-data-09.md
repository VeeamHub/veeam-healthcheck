---
title: "CRegReader.DefaultLogDir returns null instead of the default path when the Veeam registry key is missing"
severity: Medium
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:557
confidence: High
---

## Summary

`DefaultLogDir` declares a sensible default (`C:\ProgramData\Veeam\Backup`) but in the `key == null` branch assigns `logDir = dir;` where `dir` is still `null`, and returns it — discarding the default. A second lesser defect: when the key exists but has zero value names, the `foreach` never runs and the method returns `dir` which is `null` as well.

## Impact

Downstream consumers receive null/empty instead of the documented default:
- `CLogParser.InitLogDir` → `LogLocation` null → `Directory.GetDirectories(null)` throws; caught broadly in `CCollections.PopulateWaits`, so `waits.csv` is silently never populated (missing wait analysis in the report).
- `CVmcReader.GetLogDir` → `Path.Combine(regDir + CLogOptions.VMCLOG)` with null `regDir` yields the rootless relative path `\Utils\VMC.log`, so InstallationId parsing fails.

This bites exactly the environments where the registry key is absent or unreadable — the case the default was written for.

## Evidence

`vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:526-560` —

```csharp
var logDir = "C:\\ProgramData\\Veeam\\Backup";
...
    string dir = null;

    if (key != null)
    {
        ...
        return dir;
    }
    else
    {
        logDir = dir;        // dir is null here — overwrites the default with null
        return logDir;       // returns null
    }
```

The assignment is backwards: it overwrites the hardcoded default with the uninitialized `dir`.

## Suggested fix

In the `else` branch, simply `return logDir;` (the default). Also initialize `dir = logDir;` before the loop so an existing key without a `LogDirectory` value still returns the default.
