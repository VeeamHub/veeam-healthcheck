---
title: "Jobs(): 'break' on empty Organization silently truncates all remaining job rows"
severity: High
labels: [bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1639
confidence: High
---

## Summary

Inside the per-job loop of `Jobs()`, a row whose `Organization` value is empty triggers `break`, which exits the **entire** loop. Every job after that row is dropped from the Jobs table. If the intent was to skip malformed rows, this should be `continue`.

## Impact

One job with a blank/missing organization field (e.g., a copy job variant, a CSV quirk, or a column rename in a future VB365 version) hides an arbitrary number of subsequent jobs from the report — wrong inventory data with no error. Note also the open `<tr>` appended at line 1573 before the check is never closed, producing malformed HTML for the truncated row.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1639-1642`:

```csharp
if (string.IsNullOrEmpty(org))
{
    break;
}
```

within `foreach (var gl in global)` started at line 1557, after `s += "<tr>";` (line 1573).

## Suggested fix

Use `continue` (and emit/close the row consistently — move `s += "<tr>"` after the validity check), or better, render the row anyway with an empty org cell so data is never silently hidden.
