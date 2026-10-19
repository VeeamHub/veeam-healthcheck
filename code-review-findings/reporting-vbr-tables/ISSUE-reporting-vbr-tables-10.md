---
title: "Fix On-Disk totals cell in CJobInfoTable: GB value rendered with TB/MB tooltip, conversions dead"
severity: Medium
labels: [bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobInfoTable.cs:327
confidence: High
---

## Summary

In the per-section Totals row of the Job Info tables, the on-disk total computes `diskTotalTB` and `diskTotalMB` but never displays them: all three branches render the raw `onDiskTotalGB` value, attaching "TB", "GB", or "MB" as the cell *tooltip*. The source-size total directly above it does the conversion correctly (renders `totalSizeTB + " TB"` etc.), so the on-disk branch is half-finished.

## Impact

For environments over 1 TB on disk, hovering the totals cell claims the number is TB while the displayed value is GB (e.g. shows "5120" with tooltip "TB"); under 1 GB it claims MB while showing a GB fraction. The column header says "Est. On Disk GB", so the displayed number is defensible — but the tooltip actively asserts the wrong unit, and the computed conversions are dead code.

## Evidence

`Jobs Info/CJobInfoTable.cs:312-338`:

```csharp
double diskTotalTB = Math.Round(onDiskTotalGB / 1024, 2);
double diskTotalMB = Math.Round(onDiskTotalGB * 1024, 2);
...
if (diskTotalTB > 1)
{
    s += this.form.TableData(onDiskTotalGB.ToString(), "TB");   // value is GB, tooltip says TB
}
else if (onDiskTotalGB > 1)
{
    s += this.form.TableData(onDiskTotalGB.ToString(), "GB");
}
else
{
    s += this.form.TableData(onDiskTotalGB.ToString(), "MB");   // value is GB, tooltip says MB
}
```

Contrast the correct sibling block at lines 314-325 which renders `totalSizeTB.ToString() + " TB"` / `totalSizeMB.ToString() + " MB"` inline.

Related nit in the same row loop: per-row source size uses threshold `trueSizeGB > 999` for TB but the totals use the same value unconverted — fine — while per-row on-disk (`row += this.form.TableData(onDiskGB.ToString(), ...)` at line 183) is always GB, consistent with the header.

## Suggested fix

Either always render GB to match the header (`onDiskTotalGB.ToString() + " GB"`, drop the dead conversions), or mirror the source-size pattern and render `diskTotalTB + " TB"` / `diskTotalMB + " MB"` in the value itself. Don't smuggle units through tooltips.
