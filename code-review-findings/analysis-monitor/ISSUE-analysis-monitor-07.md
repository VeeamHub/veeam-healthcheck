---
title: "CLogger.Debug ignores its silent parameter"
severity: Low
labels: [bug, maintainability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Common/Logging/CLogger.cs:70
confidence: High
---

## Summary

`Debug(string message, bool silent)` accepts a `silent` flag but never uses it. The console-visibility decision is made solely from `CGlobals.DEBUG`; a caller passing `Debug(msg, silent: false)` expecting console output (or `true` expecting suppression while DEBUG is on) is silently overridden.

## Impact

Misleading API: callers cannot control console verbosity of debug lines, and code reading call sites draws wrong conclusions about what is printed. Low functional impact (file logging still happens), but it is a latent bug whenever someone relies on the parameter.

## Evidence

`vHC/HC_Reporting/Common/Logging/CLogger.cs:70-81` —
```csharp
public void Debug(string message, bool silent)
{
    message = this.FormLogLine(message, "DEBUG");
    if (CGlobals.DEBUG)
    {
        this.LogLine(message, false, 2);
    }
    else
    {
        this.LogLine(message, true, 2);
    }
}
```
`silent` does not appear in the body — the second argument to `LogLine` is hardcoded in both branches.

## Suggested fix

Honor the parameter, e.g. `this.LogLine(message, silent || !CGlobals.DEBUG, 2);`, or remove the overload's parameter so the API doesn't lie.
