---
title: "CSectionTable swallows extractor exceptions per cell, rendering silent blanks"
severity: Low
labels: [reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/Shared/CSectionTable.cs:178
confidence: High
---

## Summary

`CSectionTable<T>.Render` wraps each column extractor in `catch { value = ""; }` — a bare catch with no logging. A buggy extractor (NRE on a null property, format exception) produces an empty cell indistinguishable from genuinely empty data, across every row, with zero log evidence.

A secondary quirk: the raw-vs-encoded decision is `if (value.Contains("&#"))` — any *data* value that happens to contain `&#` (e.g., a literal entity in a job name) is emitted unencoded, partially undermining the class's own encoding guarantee.

## Impact

Data quality bugs hide as blank cells; troubleshooting requires guessing. The `&#` heuristic is a small encoding bypass through attacker-influenceable data.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/Shared/CSectionTable.cs:173-193` —

```csharp
try
{
    value = col.Extractor(item) ?? "";
}
catch
{
    value = "";
}
...
if (value.Contains("&#"))
{
    sb.Append($"<td title=\"\"{alignStyle}>{value}</td>");   // raw pass-through
}
```

## Suggested fix

Log once per column on first failure (`log.Warning($"Extractor for '{col.Header}' failed: {ex.Message}")`, suppress repeats). Replace the `&#` sniffing with an explicit `ColumnDef.IsRawHtml` flag set at definition time (the only intended raw values are the two emoji constants the class itself owns).
