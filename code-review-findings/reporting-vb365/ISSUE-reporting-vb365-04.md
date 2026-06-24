---
title: "Repository free-space warning never fires — parses '1.234 TB (12.34 %)' strings as plain doubles"
severity: High
labels: [bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:401
confidence: High
---

## Summary

`Vb365Repos()` runs `double.TryParse` directly on the `Free` and `Capacity` CSV fields. The collection script writes these with units and a percentage suffix — `Capacity` as `"#,##0.000 TB"` and `Free` as `"#,##0.000 TB (#,##0.00 %)"` — so both parses always fail, both values stay 0, `0/0*100` is `NaN`, and `NaN < 10` is false. Result: the low-free-space shading (the key health signal of this table) can never trigger.

## Impact

Repositories at 9%, 5%, or 1% free space are rendered with no warning color. A core health-check signal is silently dead. (`Vb365ObjectRepos()` got this right by splitting on whitespace first — `CM365Tables.cs:1285-1289` — showing the intended pattern; `Vb365Repos` drifted.)

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:401-407`:

```csharp
double.TryParse(g.Free, out double freeSpace);
double.TryParse(g.Capacity, out double capacity);

if (freeSpace / capacity * 100 < 10)
{
    freeShade = 3;
}
```

Collector output format, `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1:1111-1112`:

```powershell
'Capacity=>($.Capacity/1TB).ToString("#,##0.000 TB")'
'Free=>($.FreeSpace/1TB).ToString("#,##0.000 TB") +" ("+ ... + ")"'
```

`double.TryParse("1.234 TB (12.34 %)")` → false → 0. `0/0` → NaN → no shade, on every row, always.

## Suggested fix

Split on whitespace and parse the first token with `CultureInfo.InvariantCulture` and `NumberStyles.Number` (to accept thousands separators), as `Vb365ObjectRepos` does; guard `capacity > 0` before dividing. Better: extract one shared `TryParseLeadingNumber(string)` helper used by both methods.
