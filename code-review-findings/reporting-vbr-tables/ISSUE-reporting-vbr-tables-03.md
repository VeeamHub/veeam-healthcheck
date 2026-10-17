---
title: "Fix License Utilization KPI: per-row TryParse overwrites totals instead of summing"
severity: High
labels: [bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:1624
confidence: High
---

## Summary

`AddKpiRow()` computes the "License Utilization" dashboard KPI by looping over all license CSV rows and calling `int.TryParse(..., out licTotal)` / `int.TryParse(..., out licUsed)` inside the loop. Each iteration overwrites the previous values, so only the last license row counts; a non-numeric value (e.g. blank or "Unlimited") on the last row resets the value to 0.

## Impact

Environments with more than one license entry (common: instance license + socket license, or merged/rental licenses) show a wrong License Utilization number and percentage on the executive KPI bar — the most prominent numbers in the report. A trailing unparsable row silently produces "N/A"/0% even when license data exists.

## Evidence

`CHtmlTables.cs:1624-1630`:

```csharp
foreach (var row in lic)
{
    string instLic = row.licensedinstances?.ToString() ?? "";
    string instUsed = row.usedinstances?.ToString() ?? "";
    int.TryParse(instLic, out licTotal);
    int.TryParse(instUsed, out licUsed);
}
```

`licTotal`/`licUsed` are declared once at line 1617 and clobbered per row. Compare `LicTable()` (line 181+) which renders every license row individually — the KPI should aggregate the same rows, not take the last.

Note the same loop-shape exists for the Security Score KPI (lines 1646-1654) but that one correctly accumulates (`secTotal++`, `secPassed++`), confirming the license loop is an oversight.

## Suggested fix

Accumulate: `if (int.TryParse(instLic, out var t)) licTotal += t;` and likewise for `licUsed`. Decide explicitly how to treat unparsable/unlimited rows (skip, not zero).
