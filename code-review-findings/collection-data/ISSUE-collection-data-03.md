---
title: "Reorder ExecutePsScript timeout handling: stream tasks are awaited before the timeout/kill branch can run"
severity: Medium
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:417
confidence: High
---

## Summary

`ExecutePsScript` correctly reads stdout/stderr on background tasks and uses `WaitForExit(604800000)` (7 days) so it can kill a stuck process. But the code awaits both stream tasks *before* checking `exited`, so when the timeout actually fires, `stdOutTask.GetAwaiter().GetResult()` blocks forever (the stream doesn't reach EOF until the child exits), and the `Kill()` branch is unreachable.

## Impact

The one safety net against a hung collection script can never trigger. If a VBR collection script hangs for 7 days, vHC hangs indefinitely instead of killing it and reporting failure. Low frequency (7-day window), but the timeout code is dead as written.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:417-425` —

```csharp
bool exited = res1.WaitForExit(604800000);
string stdOut = stdOutTask.GetAwaiter().GetResult();   // blocks forever if !exited
string stdErr = stdErrTask.GetAwaiter().GetResult();
if (!exited)
{
    this.log.Error("[PS] Script execution timeout after 7 days", false);
    try { res1.Kill(); } catch { }                      // unreachable on timeout
    return false;
}
```

The stream readers only complete when the child closes its ends of the pipes (i.e., on exit or kill), so on timeout the `GetResult()` calls deadlock before `Kill()` is reached.

Also: `res1` (Process) is never disposed in this method.

## Suggested fix

Check `exited` first; on timeout, `Kill(entireProcessTree: true)` and *then* await the stream tasks (they complete once the process dies). Wrap the Process in `using`.
