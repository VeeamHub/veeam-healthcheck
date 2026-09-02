# Job Info totals: on-disk column shows GB value mislabeled, and unit thresholds are inconsistent

**Category:** reporting-vbr-tables
**Severity:** Medium
**Type:** Bug
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobInfoTable.cs:309-338`

## Summary
In the per-table Totals row, the "on-disk total" cell selects its unit by magnitude but always emits the raw `onDiskTotalGB` number while changing only the tooltip to "TB"/"GB"/"MB". So when the total exceeds 1 TB, the report displays the GB figure (e.g. `2048`) with a "TB" tooltip — a misleading value/unit mismatch. The source-size cell directly above it (lines 314-325) does convert (`totalSizeTB`), so the two adjacent total cells use different, inconsistent rules.

## Evidence
```csharp
double diskTotalTB = Math.Round(onDiskTotalGB / 1024, 2);
double diskTotalMB = Math.Round(onDiskTotalGB * 1024, 2);   // computed, never used
if (diskTotalTB > 1)
{
    s += this.form.TableData(onDiskTotalGB.ToString(), "TB");   // value is GB, label says TB
}
else if (onDiskTotalGB > 1)
{
    s += this.form.TableData(onDiskTotalGB.ToString(), "GB");
}
else
{
    s += this.form.TableData(onDiskTotalGB.ToString(), "MB");   // value still GB, label says MB
}
```
Compare the source-size cell directly above (314-325) which emits `totalSizeTB.ToString() + " TB"` / `totalSizeMB.ToString() + " MB"` with the converted value and the unit in the visible cell text. The on-disk branch never uses `diskTotalTB`/`diskTotalMB` and puts the unit only in the (often-invisible) `title` tooltip.

Additional minor inconsistency: the per-row size logic at lines 165-176 switches to TB at `trueSizeGB > 999` (i.e. ~0.976 TB), whereas the totals switch at `tSizeGB > 999`; the boundary "999 GB" is an odd threshold vs a clean 1024.

## Impact
On-disk totals over 1 TB are shown as a large GB number tagged "TB" in a tooltip, which reads as either an absurd value or a wrong unit. Reviewers comparing source vs on-disk totals see two columns formatted by different rules.

## Suggested Fix
Mirror the source-size branch: emit the converted value with the unit in the cell text.
```csharp
if (diskTotalTB > 1)        s += this.form.TableData(diskTotalTB.ToString() + " TB", string.Empty);
else if (onDiskTotalGB > 1) s += this.form.TableData(onDiskTotalGB.ToString() + " GB", string.Empty);
else                        s += this.form.TableData(diskTotalMB.ToString() + " MB", string.Empty);
```
Standardize the GB->TB threshold (use `>= 1024` consistently in rows and totals).

## Labels
bug, units, formatting, totals, correctness
