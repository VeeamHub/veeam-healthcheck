# Malware tables throw (drop whole section) instead of degrading when CSV is missing/malformed

**Category:** reporting-vbr-tables
**Severity:** Medium
**Type:** Bug
**File(s):** `Functions/Reporting/Html/VBR/VbrTables/Security/CMalwareTable.cs:128-133,179-184,250-255,327-333` and unguarded `DateTime.Parse` at `:203`

## Summary
Unlike the repo/proxy/managed-server renderers (which `catch` and log, keeping the page intact), all four Malware table methods end their `catch` blocks with `throw;`. Any parse failure propagates up and aborts report generation for the surrounding section instead of rendering a graceful "no data" placeholder. The most likely trigger is the unguarded `DateTime.Parse(item.DetectionTime)` at line 203.

## Evidence
```csharp
// MalwareEventsTable, line 200-205
foreach (var item in lst)
{
    item.DetectionTime = item.DetectionTime.Replace("T", " ").Replace("Z", string.Empty);
    var dt = DateTime.Parse(item.DetectionTime);   // throws on any non-parseable / empty / non-default-culture value
    TimeSpan diff = DateTime.Now - dt;
    ...
}
...
catch (Exception e)
{
    this.log.Error("Failed to parse Malware Events table:");
    this.log.Error(e.Message);
    throw;     // <-- aborts instead of returning a placeholder
}
```
The same `throw;` ends `MalwareSettingsTable` (133), `MalwareExclusionsTable` (184), and `MalwareInfectedObjectsTable` (332). `MalwareSettingsTable` also dereferences `mo = m.FirstOrDefault()` (line 37) and then uses `mo.InlineMalwareScanEnabled` (line 52) — `m.Any()` is checked first so `mo` is non-null here, but `DetectionEngine`/`Sensitivity`/`NotificationOptions` are emitted raw (see issue 01).

`DateTime.Parse` is also culture-sensitive: a CSV timestamp in an unexpected format for the host culture throws even when the data is valid.

## Impact
A single malformed or unexpectedly-formatted `DetectionTime` (or any other parse hiccup) takes down the entire Malware/Security portion of the report rather than dropping one row. This is the opposite of the resilient behavior the other renderers already implement.

## Suggested Fix
- Replace `DateTime.Parse(item.DetectionTime)` with `DateTime.TryParse(item.DetectionTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)` and skip/flag rows that fail.
- Change the trailing `throw;` in each catch to render a placeholder row (as `CProxyTable.cs:122` does) and `return t;`, so one bad table never aborts the report.

## Labels
bug, exception-handling, robustness, culture, datetime-parse
