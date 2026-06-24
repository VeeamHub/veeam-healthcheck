# CImportPathResolver mutates global product flags as a side effect of a "find" method

**Category:** collection-data
**Severity:** Low
**Type:** Maintainability
**File(s):** `vHC/HC_Reporting/Functions/Collection/CImportPathResolver.cs:82-132`

## Summary
`FindCsvDirectory` is named and documented as a pure lookup ("Find the directory containing CSV files ... returns Full path ... or null"), but it has the hidden side effect of writing `CGlobals.IsVbr = true` / `CGlobals.IsVb365 = true` depending on which subdirectory it happens to match first. Product detection is thereby coupled to filesystem traversal order, and a caller that only wanted a path silently flips global execution mode.

## Evidence
```csharp
// CImportPathResolver.cs:88
CGlobals.IsVbr = true;
return foundPath;
...
// CImportPathResolver.cs:99
CGlobals.IsVb365 = true;
return foundPath;
```
The flags are set in the VBR/VB365 and Original/* branches but NOT in the "direct" (Strategy 1) or "recursive" (Strategy 4) branches, so detection is inconsistent depending on which strategy succeeds.

## Impact
Whether `IsVbr`/`IsVb365` get set depends on directory layout and search-strategy ordering, not on actual content — a flat import (Strategy 1) leaves both flags untouched even though the files clearly identify the product. This makes import behavior surprising and order-dependent, and splits product detection across two mechanisms (here and `ValidateCsvFiles`/`ModeCheck`). It is a correctness foot-gun rather than a visible bug today.

## Suggested Fix
Make `FindCsvDirectory` pure (return the path only). Determine product type separately and consistently from the discovered file set (the logic in `ValidateCsvFiles`/`HasCriticalFiles` already does this) and set the globals in one place.

## Labels
maintainability, hidden-side-effect, global-state, collection
