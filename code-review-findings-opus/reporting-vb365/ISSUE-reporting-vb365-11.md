# ProtStat user totals can double-count and percentage ignores stale users

**Category:** reporting-vb365
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1477-1516`

## Summary
`Vb365ProtStat()` tallies three independent counters from the protection-status CSV, then computes "Total Users" as their sum and the unprotected percentage from only two of them. The categories are not guaranteed mutually exclusive: a row with `hasbackup == "False"` **and** `isstale == "True"` increments both `notProtectedUsers` and `stale`, so the "Total Users" sum over-counts that user. Conversely the unprotected-percentage denominator excludes `stale` entirely, so the percent and the displayed totals are computed on inconsistent populations.

## Evidence
`CM365Tables.cs:1479-1498`:
```csharp
if (gl.hasbackup == "True" && gl.isstale == "False") { protectedUsers++; }
if (gl.hasbackup == "False")                          { notProtectedUsers++; }  // may also be stale
if (gl.isstale == "True")                             { stale++; }              // overlaps the above
...
double percent = notProtectedUsers / (notProtectedUsers + protectedUsers) * 100; // ignores stale
```
Total rendered as the sum of all three (`CM365Tables.cs:1513`):
```csharp
s += this.form.TableData((protectedUsers + notProtectedUsers + stale).ToString(), string.Empty);
```
If any user is both unprotected and stale, that user is counted twice in the total; meanwhile the percentage's denominator (`notProtectedUsers + protectedUsers`) silently omits stale-but-backed-up users.

## Impact
"Total Users", "Unprotected Users", "Stale Backups", and the red/yellow unprotected-percentage threshold can disagree with each other and with the actual user count, depending on how stale/unprotected overlap in the source data. The protection summary — a headline metric of the VB365 report — becomes internally inconsistent.

## Suggested Fix
Define the buckets as mutually exclusive (e.g. protected = hasbackup&&!stale, stale = hasbackup&&stale, unprotected = !hasbackup) and derive Total from the row count, not the sum of overlapping counters. Compute percentage against the true total.

## Labels
bug, metrics, double-count, vb365, protection-status
