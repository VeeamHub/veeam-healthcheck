---
title: "Fix wrong-variable assignment: '<5% free' branch sets freeSpace instead of freeShade"
severity: Medium
labels: [bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:410
confidence: High
---

## Summary

In `Vb365Repos()`, the escalation branch for repositories under 5% free space assigns `freeSpace = 1;` (the parsed data value) instead of `freeShade = 1;` (the cell color). Even after the parsing bug in ISSUE-04 is fixed, a repo below 5% free would show the yellow/warning shade (3) from the <10% branch rather than escalating to danger (1).

## Impact

Critically-full repositories (<5% free) are visually indistinguishable from mildly-full ones (<10%). The mutation of `freeSpace` also corrupts the value for any later use in the loop iteration.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:404-413`:

```csharp
if (freeSpace / capacity * 100 < 10)
{
    freeShade = 3;
}


if (freeSpace / capacity * 100 < 5)
{
    freeSpace = 1;     // <-- should be freeShade = 1
}
```

The parallel logic in `Vb365ObjectRepos()` (`CM365Tables.cs:1290-1299`) correctly sets `freeSpaceShade = 1` in the <5% branch, confirming the intent.

## Suggested fix

Change `freeSpace = 1;` to `freeShade = 1;`.
