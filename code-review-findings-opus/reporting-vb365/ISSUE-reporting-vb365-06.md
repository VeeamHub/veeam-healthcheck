# Divide-by-zero on license / free-space / size-limit percentages drops whole sections

**Category:** reporting-vb365
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:51-53, 404-410, 1290-1296, 1498`

## Summary
Several percentage calculations divide by a parsed denominator without checking for zero. For `decimal` (Globals license usage) a zero denominator throws `DivideByZeroException`; for `double` (repos/object-storage free space, protection percentage) it yields `Infinity`/`NaN`, which makes the threshold comparisons behave incorrectly. The `decimal` case is thrown into the surrounding empty catch and drops the entire Global Configuration / section.

## Evidence
Globals — decimal divide, throws on `licFor == 0` (`CM365Tables.cs:51-53`):
```csharp
decimal.TryParse(gl.LicensedFor, out decimal licFor);
decimal.TryParse(gl.LicensesUsed, out decimal licUsed);
decimal percentUsed = licUsed / licFor * 100;   // DivideByZeroException if licFor == 0
```
Repos — double divide, `capacity == 0` → NaN (`CM365Tables.cs:404-410`):
```csharp
if (freeSpace / capacity * 100 < 10) { freeShade = 3; }
```
Object storage — `sizeLimitNumber == 0` → NaN (`CM365Tables.cs:1290-1296`).
Protection stats — `notProtectedUsers + protectedUsers == 0` (empty org / no users) → NaN (`CM365Tables.cs:1498`):
```csharp
double percent = notProtectedUsers / (notProtectedUsers + protectedUsers) * 100;
```

## Impact
- Globals: a license CSV with `LicensedFor=0` (unlicensed/trial/edge case) throws and the whole Global Configuration table renders empty.
- Repos/object storage: `NaN < 10` is `false`, so a zero-capacity/zero-limit repo never colors — the danger condition is silently lost.
- ProtStat: with zero users the percentage is `NaN`, so no shading and a misleading 0/0 summary.

## Suggested Fix
Guard each denominator before dividing, e.g.:
```csharp
decimal percentUsed = licFor > 0 ? licUsed / licFor * 100 : 0;
double pct = capacity > 0 ? freeSpace / capacity * 100 : 0;
double percent = (notProtectedUsers + protectedUsers) > 0
    ? notProtectedUsers / (notProtectedUsers + protectedUsers) * 100 : 0;
```

## Labels
bug, divide-by-zero, nan, threshold, vb365, reporting
