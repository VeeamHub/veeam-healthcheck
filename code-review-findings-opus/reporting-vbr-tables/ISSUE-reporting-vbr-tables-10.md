# FormatDuration drops the hours component for compliance scans over 59 minutes

**Category:** reporting-vbr-tables
**Severity:** Low
**Type:** Bug
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/Security/CComplianceTable.cs:188-194`

## Summary
`FormatDuration` formats a duration using `TimeSpan.Minutes` and `TimeSpan.Seconds`, which are the *components* of the timespan (0-59), not the totals. For any scan that runs 60 minutes or longer, the hours are silently dropped and the minutes wrap, displaying a far smaller duration than the truth.

## Evidence
```csharp
private static string FormatDuration(double seconds)
{
    if (seconds < 1)  return "<1s";
    if (seconds < 60) return $"{seconds:F0}s";
    var ts = TimeSpan.FromSeconds(seconds);
    return ts.Minutes > 0 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
}
```
For `seconds = 3725` (1h 2m 5s), `ts.Minutes == 2`, so the output is `"2m 5s"` — the hour is gone and the value reads as ~2 minutes instead of ~62.

## Impact
Compliance scan durations are a diagnostic signal (a multi-hour scan indicates load/timeout risk). Long scans are under-reported, hiding exactly the slow-scan condition the field would want to see. Low severity because compliance scans rarely exceed an hour, but the display is simply wrong when they do.

## Suggested Fix
Include hours, or use total components:
```csharp
var ts = TimeSpan.FromSeconds(seconds);
if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
return ts.Minutes > 0 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
```

## Labels
bug, formatting, timespan, duration
