---
title: "CImportPathResolver.ValidateCsvFiles declares import valid with a single critical file and misvalidates unknown product type"
severity: Medium
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/CImportPathResolver.cs:343
  - vHC/HC_Reporting/Functions/Collection/CImportPathResolver.cs:326
confidence: High
---

## Summary

Two validation logic gaps:

1. `result.IsValid = result.MissingCriticalFiles.Count < criticalFiles.Length;` marks the import valid whenever *at least one* critical file is present — a VBR folder missing 4 of 5 critical files (no `_Jobs.csv`, no `Servers.csv`, no `vbrinfo.csv`...) still passes as valid.
2. Product detection: `isVbr` requires `_Jobs.csv`/`vbrinfo.csv` exactly; if neither VBR nor VB365 markers are found, `criticalFiles` silently defaults to the VB365 list (`isVbr ? CriticalVbrFiles : CriticalVb365Files`) while `ProductType` is set to "Unknown" — so an unidentified folder is graded against VB365 expectations.

Note the inconsistency with `HasCriticalFiles` (line 272-294), which requires 3 of 5 VBR / 2 of 3 VB365 matches for discovery — discovery is stricter than validation.

## Impact

Imports with grossly incomplete data are accepted as valid; report generation proceeds and produces mostly-empty sections, which a user can mistake for an actual healthy-but-sparse environment (partial data treated as complete).

## Evidence

`vHC/HC_Reporting/Functions/Collection/CImportPathResolver.cs:322-344` —

```csharp
bool isVbr = csvFiles.Any(f => f.Equals("_Jobs.csv", ...) || f.Equals("vbrinfo.csv", ...));
bool isVb365 = csvFiles.Any(f => f.Equals("Organizations.csv", ...));

string[] criticalFiles = isVbr ? CriticalVbrFiles : CriticalVb365Files;   // Unknown → VB365 list
...
result.IsValid = result.MissingCriticalFiles.Count < criticalFiles.Length; // valid if ≥1 present
result.ProductType = isVbr ? "VBR" : (isVb365 ? "VB365" : "Unknown");
```

Also note `isVbr` checks exact filenames only, while the per-file loop (lines 331-333) accepts the `{servername}_` prefix — a prefixed-only folder (`localhost_vbrinfo.csv`) is detected as VB365/Unknown despite containing VBR data.

## Suggested fix

Align thresholds with `HasCriticalFiles` (e.g., `IsValid = MissingCriticalFiles.Count == 0`, or an explicit minimum-present quorum); when product type is Unknown, return `IsValid = false` with a clear error; use the same prefix-tolerant matching for the `isVbr`/`isVb365` detection.
