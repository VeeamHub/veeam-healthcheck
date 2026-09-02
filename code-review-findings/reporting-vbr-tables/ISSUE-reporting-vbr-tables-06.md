---
title: "Fix concurrency heatmap window, phantom default rows, and midnight overflow in CConcurrencyHelper"
severity: Medium
labels: [bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Concurrency Tables/CConcurrencyHelper.cs:292
confidence: High
---

## Summary

The Job/Task Concurrency heatmaps (`jobcon`/`taskcon` sections) have three related defects in `CConcurrencyHelper`:

1. **Hardcoded 7-day window vs `CGlobals.ReportDays`** — every call site passes a literal `7` to `ParseConcurrency(session, 7)` while the outer filters use `CGlobals.ReportDays`. With `/days:30` the section header advertises a 30-day window but only sessions from the last 7 days contribute.
2. **Out-of-window sessions add default trackers** — when `diff >= days`, `ParseConcurrency` returns an empty `ConcurentTracker` (default `Date = 0001-01-01`, `DayofTheWeeek = Sunday`, `Duration = 0`) which the caller still `Add`s to the list. Note the key mismatch: `ConcurrencyDictionary` keys on `c.DayofTheWeeek` (default Sunday) but matches rows with `c2.Date.DayOfWeek` (Monday for `DateTime.MinValue`), so phantom day buckets can appear with empty counts.
3. **Durations crossing midnight are silently dropped** — the minute loop increments `hMinute` past 1439 without wrapping; keys ≥1440 map to hour ≥24, which the `for (hour = 0; hour < 24)` aggregation never reads. Long jobs running over midnight lose their post-midnight concurrency, understating overnight load — the exact window this heatmap exists to show.

Also latent: `ParseConcurrency` computes `endTime = startTime.AddMinutes(duration.Minutes)` (line 309) using the minutes *component* (0-59) instead of `TotalMinutes`; `endHour`/`endMinute` are currently unused, but it's a trap.

## Impact

Concurrency heatmaps shown to customers can understate or misplace job/task concurrency — wrong numbers in a section used to justify proxy/repository sizing recommendations.

## Evidence

`Concurrency Tables/CConcurrencyHelper.cs:31-34` (same at 83, 104, 130, 145):

```csharp
if (diff < CGlobals.ReportDays)
{
    ctList.Add(this.ParseConcurrency(session, 7));   // hardcoded 7
}
```

`CConcurrencyHelper.cs:303-327`:

```csharp
if (diff < days) { ...; return ct; }
return ct;   // default tracker still returned and added by caller
```

`CConcurrencyHelper.cs:168-175` — minute spreading without day wrap:

```csharp
for (int i = 0; i < ticks; i++)
{
    minuteMapper.TryGetValue(hMinute, out current2);
    minuteMapper[hMinute] = current2 + 1;
    hMinute++;   // exceeds 1439, never wraps; hours >= 24 are never rendered
}
```

## Suggested fix

Pass `CGlobals.ReportDays` instead of `7`; return `null` (and skip) for out-of-window sessions; wrap `hMinute %= 1440` and carry the overflow to the next `DayOfWeek` bucket (or at minimum clamp at 1439 and document). Use `duration.TotalMinutes` if end-time math is ever revived.
