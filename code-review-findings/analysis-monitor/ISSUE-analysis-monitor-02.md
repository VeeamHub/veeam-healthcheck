---
title: "Fix truncated process output: WaitForExit(timeout) returns before async stdout/stderr drain"
severity: Medium
labels: [bug, reliability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:286
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:42
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:57
confidence: High
---

## Summary

`RunProcess()` uses `BeginOutputReadLine()`/`BeginErrorReadLine()` together with the timeout overload `WaitForExit(timeoutMs)`. Per documented .NET behavior, when `WaitForExit(Int32)` returns `true`, asynchronous output processing may not yet be complete; the parameterless `WaitForExit()` must be called afterwards to flush the redirected-stream event handlers. Without it, `stdoutSb`/`stderrSb` can be missing the tail (or all) of the process output.

## Impact

Every consumer of `RunProcess` makes decisions from possibly-truncated output:
- `IsTaskRegistered()` (line 44) does `stdout.Contains("VHC Monitor")` — a lost stdout line makes it return `false` for a registered task, so the GUI shows "Available — not set up" and `OfferMonitorSetupIfNeeded()` re-prompts the user to install a monitor that is already running.
- `GetInstalledVersion()` (line 58) reports "unknown".
- `RunNow()`/`TestConnection()` return incomplete diagnostics.

This is intermittent and timing-dependent — exactly the class of bug users report as "status flickers / sometimes says not installed".

## Evidence

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:282-294` —
```csharp
process.Start();
process.BeginOutputReadLine();
process.BeginErrorReadLine();

if (!process.WaitForExit(timeoutMs))
{
    ...
}

return (process.ExitCode, stdoutSb.ToString(), stderrSb.ToString());
```
No parameterless `WaitForExit()` after the timed wait, so the `OutputDataReceived` handlers may still have queued data when the StringBuilders are snapshotted.

## Suggested fix

After a successful timed wait, call the parameterless overload before reading the builders:

```csharp
if (!process.WaitForExit(timeoutMs)) { ... timeout path ... }
process.WaitForExit(); // drains async output handlers
return (process.ExitCode, stdoutSb.ToString(), stderrSb.ToString());
```
