---
title: "CLogParser silently swallows per-file errors and writes unvalidated parse results into waits.csv"
severity: Medium
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:163
  - vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:257
  - vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:277
confidence: High
---

## Summary

Three related defects in the wait-time log parser:

1. Empty `catch (Exception) { }` blocks at lines 163 and 257-258 silently discard per-file and per-line failures — locked files, encoding issues, malformed lines all vanish without a trace (line 257 also has a redundant empty `catch (ArgumentOutOfRangeException)` ahead of the catch-all).
2. `CalcTime` ignores the boolean result of `DateTime.TryParseExact`. On parse failure both timestamps default to `DateTime.MinValue`, producing a zero (or nonsense) `TimeSpan` that is still appended to `waits.csv` and returned to the wait statistics.
3. The format is hardcoded to `"dd.MM.yyyy HH:mm:ss"` with a fixed-width `line.Remove(21)` slice (lines 232, 246). Veeam log timestamp layout differences (e.g., locale-dependent formats in older builds, sub-second variants) silently produce failed parses → defect 2.

## Impact

Wrong-but-plausible wait-time data in the report: zero-duration waits dilute averages, and genuinely unreadable log files contribute nothing with no warning. Because the report section still renders, nobody knows the data is partial.

## Evidence

`vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:160-163` —

```csharp
    waits.AddRange(this.CheckFileWait(f, jobname));
}
}
catch (Exception) { }
```

`vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:277-283` —

```csharp
DateTime.TryParseExact(start, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime tStart);
DateTime.TryParseExact(end, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime tEnd);

var diffTime = tEnd - tStart;
...
this.DumpWaitsToFile(jobName, tStart, tEnd, diffTime);   // written even when both parses failed
```

## Suggested fix

Check both `TryParseExact` returns and skip (and debug-log) the pair on failure. Replace the empty catches with at least a `log.Debug` counter ("N files skipped due to errors") so the report can disclose parser coverage.
