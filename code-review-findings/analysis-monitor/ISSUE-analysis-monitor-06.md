---
title: "CLogger.LogLine only retries IOException — other exceptions from the logging path can crash the app"
severity: Medium
labels: [reliability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Common/Logging/CLogger.cs:111
  - vHC/HC_Reporting/Common/Logging/CLogger.cs:105
confidence: High
---

## Summary

`LogLine()` wraps the file write in `catch (IOException)` only. `FileStream`'s constructor can also throw `UnauthorizedAccessException` (ACL change, read-only attribute, AV blocking), `DirectoryNotFoundException` (log dir deleted mid-run — `CreateLogFile` only ensures it exists at construction time, line 24-27), or `PathTooLongException`/`ArgumentException` (bad `jobName` chars flowing into the file name at line 30). Any of these escape from a plain `logger.Info(...)` call and take down whatever operation was being logged — the logging path becomes a crash source.

## Impact

Logging is called from nearly every code path (collection, reporting, GUI). An environmental hiccup that should at worst lose a log line instead aborts the health-check run with an unhandled exception that itself cannot be logged. The retry block's inner bare `catch` (line 123) shows the intent was "never crash from logging," but the first attempt doesn't honor that intent for non-IO exceptions.

## Evidence

`vHC/HC_Reporting/Common/Logging/CLogger.cs:103-127` —
```csharp
try
{
    using (var fs = new FileStream(this.logFile, FileMode.Append, FileAccess.Write, FileShare.Write, ...))
    ...
}
catch (IOException)
{
    // If the file is locked, wait and retry once
    System.Threading.Thread.Sleep(10);
    try { ... }
    catch
    {
        // Silent fail - don't crash if logging fails
    }
}
```
`UnauthorizedAccessException` is not an `IOException` subclass in this context's catch scope it matters: it derives from `SystemException`, not `IOException`, so it is not caught.

Also `CLogger.cs:30` builds the file name from unsanitized `jobName`:
```csharp
string logName = String.Format("Job.{1}_{0}_.log", dt.ToString("yyyy.MM.dd_HHmmss"), jobName);
```
Invalid filename characters in `jobName` surface as `ArgumentException`/`IOException` on first write, not at construction.

## Suggested fix

Catch `Exception` (or at minimum add `UnauthorizedAccessException` and re-create the directory on `DirectoryNotFoundException`) on the first attempt, keeping the retry for `IOException`. Sanitize `jobName` with `Path.GetInvalidFileNameChars()` in `CreateLogFile`.
