---
title: "CCsvValidator wildcard substring matching can validate the wrong file as present"
severity: Low
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/CCsvValidator.cs:138
  - vHC/HC_Reporting/Functions/Collection/CCsvValidator.cs:143
confidence: Medium
---

## Summary

`ValidateSingleFile` searches with `Directory.GetFiles(_csvDirectory, $"*{fileName}*.csv", SearchOption.AllDirectories)` — a substring match across the entire tree. Two consequences:

1. Any other CSV whose name *contains* the token satisfies the check. The legacy fallback is the clearest case: for `_Jobs` it falls back to `*Jobs*.csv`, which matches files like `JobSessions.csv`, `TapeJobs.csv`, or any future jobs-adjacent export — reporting the critical `_Jobs` file as present (with that other file's record count) when the real one is missing.
2. `SearchOption.AllDirectories` from the CSV root can match stale files in sibling timestamp folders from earlier runs, validating against an old collection.

Additionally `int lineCount = File.ReadLines(filePath).Count();` counts physical lines, so CSV records containing quoted embedded newlines inflate `RecordCount`.

## Impact

Validation reports "present, N records" for a critical file that was never collected; the downstream missing-file warning logic (`GetReportSummary`, `LogValidationSummary`) is bypassed, hiding a real collection failure. Low severity because the exact-name file usually exists alongside; Medium confidence because collisions depend on the file set actually emitted.

## Evidence

`vHC/HC_Reporting/Functions/Collection/CCsvValidator.cs:138-144` —

```csharp
var matches = Directory.GetFiles(_csvDirectory, $"*{fileName}*.csv", SearchOption.AllDirectories);

// If you're validating "Jobs", also accept legacy "_Jobs"
if (matches.Length == 0 && fileName == "_Jobs")
{
    matches = Directory.GetFiles(_csvDirectory, $"*Jobs*.csv", SearchOption.AllDirectories);
}
```

## Suggested fix

Match anchored names only: accept `fileName + ".csv"` or `*_{fileName}.csv` (host-prefixed) via an explicit filename comparison on `Path.GetFileNameWithoutExtension`, as `CImportPathResolver.HasCriticalFiles` already does. Restrict search to the resolved collection directory (TopDirectoryOnly) and count records with CsvHelper if accuracy matters.
