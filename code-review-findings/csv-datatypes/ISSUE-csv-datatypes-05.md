---
title: "Isolate per-section failures in Init() — one parser error silently empties the whole report, and JobSessionInfo's null return guarantees it"
severity: High
labels: [bug, reliability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:61
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:68
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:684
confidence: High
---

## Summary

`CDataTypesParser.Init()` runs the entire 13-step parse pipeline inside a single try/catch (`CDataTypesParser.cs:61-99`). Any exception in any step skips **all subsequent steps**, leaving their public lists at their empty defaults, and the only symptom is a log line — the constructor completes "successfully" and report generation proceeds against empty data.

Worse, there is a guaranteed trigger path: `JobSessionInfo()` returns `null` on any internal error (`CDataTypesParser.cs:684`), and `Init()` immediately calls `.ToList()` on that result (`CDataTypesParser.cs:68`) → `NullReferenceException` → outer catch → `ServerInfos`, `ProxyInfos`, `ExtentInfo`, `SobrInfo`, `RepoInfos`, `WanInfos`, `ConfigBackup`, `NetTrafficRules` all remain empty.

## Impact

A single malformed CSV (sessions or jobs) degrades the output from "this section is missing" to "the whole report is empty/wrong" — silently. For a health-check tool whose output drives sizing and security recommendations, an empty-but-rendered report is a data-correctness failure, not just a UX one.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:66-99`:

```csharp
this.JobInfos = this.JobInfo();
this.log.Info("[CDataTypesParser] Parsing JobSessionInfo...");
this.JobSessions = this.JobSessionInfo().ToList();   // line 68: NRE if JobSessionInfo returned null
...
catch (Exception ex)
{
    this.log.Error($"[CDataTypesParser] Failed to initialize data parser: {ex.Message}");
    // execution continues with whatever lists were populated so far
}
```

`CDataTypesParser.cs:681-686`:

```csharp
catch (Exception e)
{
    this.log.Error("JobSessionInfo Error: "); this.log.Error("\t" + e.Message);
    return null;                                      // line 684: null instead of empty list
}
```

Note the inconsistency: the same method's early-exit path correctly returns `new List<CJobSessionInfo>()` (line 614), so the `null` in the catch is almost certainly unintentional.

## Suggested fix

- Wrap each `Init()` step in its own try/catch (or a small helper `SafeParse(Func<T> parse, string name)`) so one section's failure costs only that section.
- Change `JobSessionInfo()`'s catch to `return new List<CJobSessionInfo>();` to match its other failure path.
- Consider surfacing a "sections failed to parse: X, Y" marker into the report/exit code instead of only the log file.
