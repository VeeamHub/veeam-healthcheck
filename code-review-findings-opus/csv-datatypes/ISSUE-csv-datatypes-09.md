# Positional [Index] column mapping plus MissingFieldFound=null silently mis-binds on column drift

**Category:** csv-datatypes
**Severity:** Medium
**Type:** Maintainability
**File(s):** `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:85` (`MissingFieldFound = null`), all `[Index(n)]`-mapped models (e.g. `CJobCsvInfos.cs`, `CSobrExtentCsvInfos.cs`, `CServerCsvInfos.cs`, `CRepoCsvInfos.cs`)

## Summary
Almost every typed model binds CSV columns by ordinal position (`[Index(0)]`, `[Index(1)]`, …) rather than by header name, while CsvHelper is configured with `MissingFieldFound = null` and `HeaderValidated = null`. The combination means: if the collection script ever inserts/removes/reorders a column (which happens across VBR version upgrades — the codebase already carries version-specific shims), every field from the insertion point onward shifts by one and binds to the wrong data, with **no error raised**. Missing trailing columns simply bind to null/default silently. There is a rich `PrepareHeaderForMatch` normalizer in CCsvReader.cs:76-84 that is effectively unused by the positional models.

## Evidence
```csharp
// CCsvReader.cs:85-86
MissingFieldFound = null,    // missing column -> null, no warning
HeaderValidated = null,      // header mismatch -> no validation
```
```csharp
// CJobCsvInfos.cs — positional, order-fragile
[Index(13)] public double OriginalSize { get; set; }
[Index(14)] public string RetentionType { get; set; }
```
Drift evidence: `CSobrExtentCsvInfos.cs:10` documents a header that ends at `NfsRepositoryEncoding` (index 38), yet the model reads `FreeSpace`/`TotalSpace`/`GateHosts`/`ObjectLockEnabled` at indexes 39-42 — the documented header and the consumed indexes already disagree, and nothing flags it.

## Impact
A column-order change in any collection script produces a report full of plausible-but-wrong values (e.g. `RetentionType` showing what used to be `OriginalSize`) with zero diagnostics. This is the highest-likelihood future correctness regression in the subsystem given the heavy version-shimming already present.

## Suggested Fix
- Prefer header-name mapping (`.Name("ColumnName")` via ClassMap, as already done for `CMalwareObjectMap`) over `[Index]` for files whose schema evolves; the header normalizer is already in place to make this robust.
- Where positional mapping must stay, at minimum re-enable `HeaderValidated` (or assert expected column count) so drift fails loudly instead of silently mis-binding.

## Labels
maintainability, positional-mapping, schema-drift, csvhelper, silent-failure
