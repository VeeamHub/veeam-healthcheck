---
title: "CRegReader stores per-instance registry results in static fields"
severity: Low
labels: [maintainability, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:17
confidence: High
---

## Summary

`databaseName`, `hostInstanceString`, `host`, `user`, and `passString` are `static`, but they are populated by instance methods (`GetDbInfo`/`SetSqlInfo`) and exposed through instance properties (`DbString`, `HostString`). State from one read leaks into every later `CRegReader` instance for the process lifetime.

## Impact

- A new `CRegReader` that fails to read the registry silently serves the *previous* instance's values (e.g., stale remote-host DB info after switching targets, or test cross-contamination — relevant given `CDbAccessor.RegReader` is an explicit test seam).
- `GetDbInfo`'s guard `if (string.IsNullOrEmpty(databaseName))` (line 59) skips the v12 lookup if any earlier instance ever set it, even for a different host.
- `passString` keeps the (secured) SQL password string rooted in static memory for the process lifetime.
- Not thread-safe if collection ever parallelizes registry reads.

## Evidence

`vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:15-35` —

```csharp
public class CRegReader
{
    private static string databaseName;
    private static string hostInstanceString;
    private static string host;
    private static string user;
    private static string passString;
    ...
    public string DbString
    {
        get { return databaseName; }
    }
```

Instance accessors over static mutable state.

## Suggested fix

Make the fields instance fields. If cross-instance caching is intended, make it explicit (a static `Lazy<DbInfo>` or caching in `CGlobals`) rather than incidental.
