---
title: "CQueries parses @@version with fragile fixed-index token slicing"
severity: Low
labels: [bug, maintainability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/DB/CQueries.cs:75
confidence: High
---

## Summary

`GetSqlServerVersion` builds the SQL version string from fixed whitespace-split token positions: `s2[0] + " " + s2[1] + " " + s2[2] + " " + s2[3]`. `@@version` output format varies by product (SQL Server vs Azure SQL Edge etc.) and build; fewer than four tokens throws `IndexOutOfRangeException`, which the broad `catch (Exception e)` at line 99 turns into a logged message — but `sqlVersion` then remains `null` (the "undetermined" defaults at lines 63-64 only apply when the query itself returned null, not when parsing fails).

Edition detection (lines 78-96) also runs as four independent `if`s with no `else`; harmless but the last match wins if multiple strings appear.

## Impact

Report shows blank/null SQL version instead of "undetermined" on unexpected `@@version` layouts; the broad catch hides the parse failure as a generic error log line. Low severity — display data only, common layouts parse fine.

## Evidence

`vHC/HC_Reporting/Functions/Collection/DB/CQueries.cs:72-75` —

```csharp
string s = r[0].ToString();
string[] s2 = s.Split();

this.sqlVersion = s2[0] + " " + s2[1] + " " + s2[2] + " " + s2[3];
```

`vHC/HC_Reporting/Functions/Collection/DB/CQueries.cs:99` —

```csharp
catch (Exception e) { this.log.Error(e.Message); }   // sqlVersion stays null, not "undetermined"
```

## Suggested fix

Take the first line of `@@version` (`s.Split('\n')[0].Trim()`) or join `s2.Take(4)` guarded by length; initialize `sqlVersion`/`sqlEdition` to "undetermined" before parsing so every failure path degrades consistently.
