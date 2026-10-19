---
title: "Harden CMalwareTable: unguarded DateTime.Parse plus rethrow drops all malware tables on one bad row"
severity: Medium
labels: [reliability, bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Security/CMalwareTable.cs:203
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CSecuritySummarySection.cs:56
confidence: High
---

## Summary

`CMalwareTable.MalwareEventsTable` parses `DetectionTime` with a bare `DateTime.Parse` (no TryParse, current culture) after a hand-rolled `Replace("T"," ").Replace("Z","")` normalization. All four `CMalwareTable` methods `catch { log; throw; }` — unlike every other renderer in this domain, which degrades gracefully. The caller (`CSecuritySummarySection.Render`) appends all four tables inside a single try/catch.

## Impact

One malformed or locale-mismatched `DetectionTime` value throws `FormatException`; because the methods rethrow and the caller wraps all four calls in one catch, the Malware Events table **and every malware table after it** vanish from the Security Summary — a silent loss of security-relevant content. The `Replace("Z", string.Empty)` also strips the UTC designator, so detection times shift by the local UTC offset, which can move events in/out of the `ReportDays` window at the boundary.

## Evidence

`Security/CMalwareTable.cs:202-203`:

```csharp
item.DetectionTime = item.DetectionTime.Replace("T", " ").Replace("Z", string.Empty);
var dt = DateTime.Parse(item.DetectionTime);
```

Rethrow pattern at `CMalwareTable.cs:128-133` (and 179-184, 250-255, 327-333):

```csharp
catch (Exception e)
{
    this.log.Error("Failed to parse Malware Settings table:");
    this.log.Error(e.Message);
    throw;
}
```

Caller, `CSecuritySummarySection.cs:56-68` — one catch guards all four appends, so a throw in `MalwareSettingsTable` (line 59) or `MalwareEventsTable` (line 61) discards the remaining tables:

```csharp
var malware = new CMalwareTable();
s += malware.MalwareSettingsTable();
s += malware.MalwareExclusionsTable();
s += malware.MalwareEventsTable();
s += malware.MalwareInfectedObjectsTable();
```

Also `MalwareSettingsTable` dereferences `mo` from `m.FirstOrDefault()` (line 37) — safe today because of the `!m.Any()` guard, but the rethrow turns any future NRE into a section-wide outage.

## Suggested fix

Use `DateTime.TryParse(item.DetectionTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)` (drop the string surgery) and skip unparsable rows with a log line. Replace `throw;` with `return string.Empty;` to match the domain's graceful-degradation convention, or wrap each of the four appends in its own try/catch at the call site.
