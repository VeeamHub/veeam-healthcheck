# Monitor state timestamp parsed with culture-sensitive DateTime.TryParse

**Category:** analysis-monitor
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:234-237`

## Summary
`GetLastRunStatus` parses the `last_run` timestamp emitted by the (external, cross-platform) `vhc-monitor` tool using `DateTime.TryParse` without an invariant culture or a fixed format. JSON timestamps are conventionally ISO-8601 (`2026-06-12T14:03:00Z`), but `DateTime.TryParse` applies the current culture and local time-zone assumptions, which can mis-parse, shift the time zone, or fail on machines whose culture expects a different date order.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:234-237
if (lastRunProp.ValueKind == JsonValueKind.String &&
    DateTime.TryParse(lastRunProp.GetString(), out var ts))
{
    status.Timestamp = ts;
}
```

## Impact
The "last run" time shown in the GUI/status can be wrong (off by the UTC offset) or silently dropped on non-en-US cultures, undermining the monitor's primary signal ("did it run recently?"). Low severity (display/status only) but a real correctness issue.

## Suggested Fix
Parse with `DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts)` (or `DateTime.TryParse` with `InvariantCulture` + `AdjustToUniversal`/`RoundtripKind`) so ISO-8601 round-trips deterministically regardless of machine culture.

## Labels
bug, culture, datetime, monitor
