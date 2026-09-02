---
title: "Fix inconsistent unit ladder in NasSourceInfo.CalculateStorageString (off-by-1024 fallback)"
severity: Medium
labels: [bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/ProtectedWorkloads/NasSourceInfo.cs:86
confidence: High
---

## Summary

`CalculateStorageString` converts a raw NAS share size to a human-readable string, but the unit ladder is internally inconsistent: the divisions treat the input as **bytes** (`/1024/1024` labeled MB, `/1024^3` labeled GB, ...), while the final fallback returns the **raw value labeled "KB"**. One of the two is wrong by a factor of 1024 no matter what unit the source CSV uses.

## Impact

NAS shares smaller than ~1 MB display the raw byte count labeled "KB" (e.g. a 512 KB share renders as "524288.00 KB" instead of "512.00 KB"), a 1024x error in the Protected Workloads NAS table. If the source is actually KB, then instead every MB/GB/TB/PB label across all NAS rows is one unit step off. Either way a unit shown to the user is wrong.

## Evidence

`ProtectedWorkloads/NasSourceInfo.cs:90-117`:

```csharp
double sizeD = Convert.ToDouble(size);
double sizeMB = sizeD / 1024 / 1024;
double sizeGB = sizeD / 1024 / 1024 / 1024;
...
else if (sizeMB > 1) { return $"{sizeMB:0.00} MB"; }
else { return $"{sizeD:0.00} KB"; }   // raw value labeled KB — bytes->KB needs /1024
```

If `sizeD` is bytes (consistent with the MB/GB/TB rungs), `KB` should be `sizeD / 1024`. If `sizeD` is KB, then `sizeMB` should be `sizeD / 1024`, not `/1024/1024`.

Secondary nits in the same ladder: thresholds use `> 1` instead of `>= 1`, so values that land exactly at a boundary fall through to the smaller (mislabeled) bucket, and values between 1000 and 1024 of the next unit render as e.g. "1023.99 GB" rather than "1.00 TB" (cosmetic, but inconsistent with `CJobInfoTable`'s 999-GB threshold).

## Suggested fix

Confirm the unit emitted by the collection script for `TotalShareSize`/`TotalObjectStorageSize` (PowerShell side), then make the ladder consistent — e.g. assume bytes and change the fallback to `sizeD / 1024` labeled KB. Add a unit test covering each rung boundary. Also parse with `CultureInfo.InvariantCulture` (see ISSUE-04).
