# Repositories free-space threshold sets the value instead of the shade (broken low-space warning)

**Category:** reporting-vb365
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:410-413`

## Summary
In `Vb365Repos()`, the "critically low free space" branch assigns `freeSpace = 1;` (the parsed free-space *value*) instead of `freeShade = 1;` (the cell color). This is a copy-paste/typo bug: the variable that drives the danger coloring is never set, so a repository below 5% free space is **never flagged red**, and the stray write to `freeSpace` is dead (the value is not re-emitted; `g.Free` is rendered instead).

## Evidence
`CM365Tables.cs:401-413`:
```csharp
double.TryParse(g.Free, out double freeSpace);
double.TryParse(g.Capacity, out double capacity);

if (freeSpace / capacity * 100 < 10)
{
    freeShade = 3;          // warning (yellow) — correct
}

if (freeSpace / capacity * 100 < 5)
{
    freeSpace = 1;          // BUG: should be freeShade = 1 (danger/red)
}
```
The cell is then rendered with `freeShade`:
```csharp
s += this.form.TableData(g.Free, string.Empty, freeShade);   // line 446
```
So a repo at <5% free gets `freeShade == 3` (yellow) at most, never `1` (red).

## Impact
The most severe repository condition — nearly-full backup storage, which causes backup failures — is downgraded to a mild yellow warning or no escalation. Field engineers reviewing the health report will miss imminent out-of-space situations on VB365 repositories.

## Suggested Fix
```csharp
if (freeSpace / capacity * 100 < 5)
{
    freeShade = 1;
}
```
Also guard `capacity == 0` to avoid NaN/Infinity (see related divide-by-zero finding).

## Labels
bug, copy-paste, threshold, coloring, vb365, repositories
