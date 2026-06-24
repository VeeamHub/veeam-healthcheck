---
title: "Vb365ProtStat(): inconsistent denominators between 'Total Users' and unprotected-percentage shading"
severity: Medium
labels: [bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1498
confidence: Medium
---

## Summary

The "Unprotected Users" summary counts three buckets — protected (`hasbackup==True && isstale==False`), not protected (`hasbackup==False`), stale (`isstale==True`) — and displays `Total = protected + notProtected + stale`. But the warning percentage uses `notProtected / (notProtected + protected)`, excluding stale users from the denominator. With many stale users, the unprotected percentage is overstated relative to the displayed Total (e.g. 10 unprotected / 90 protected / 50 stale → shown total 150, but percent computed as 10/100 = 10%, not 10/150 ≈ 6.7%). There are also boundary gaps: `percent == 20` matches neither `> 20` (red) nor `< 20` (yellow) and gets no shade; same at exactly 10.

Additionally, any row where `isstale` is empty and `hasbackup` is `True` (the collector writes empty `Is Stale` only for users without backups, but a defensive view: unexpected values) falls into no bucket, so Total can undercount versus the CSV row count.

## Impact

The headline protection statistics — one of the first numbers a customer sees — can be internally inconsistent: a percentage that doesn't correspond to the displayed totals, and threshold values (exactly 20%/10%) that get no highlighting.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1498-1513`:

```csharp
double percent = notProtectedUsers / (notProtectedUsers + protectedUsers) * 100;
...
if (percent > targetPercent)
{
    shade = 1;
}

if (percent > yellowPercent && percent < targetPercent)
{
    shade = 3;
}

s += "<tr>";
s += this.form.TableData((protectedUsers + notProtectedUsers + stale).ToString(), string.Empty);
```

## Suggested fix

Pick one denominator: `double total = protectedUsers + notProtectedUsers + stale;` and compute `percent = total > 0 ? notProtectedUsers / total * 100 : 0;` (also guards the 0/0 NaN case). Use `>=` on the yellow boundary and `else if` so thresholds are contiguous.
