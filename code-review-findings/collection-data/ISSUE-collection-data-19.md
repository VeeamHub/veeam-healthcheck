---
title: "TryModuleLoad redirects streams it never reads, risking false dynamic-fallback timeouts"
severity: Low
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/CCollections.cs:637
confidence: Medium
---

## Summary

`CCollections.TryModuleLoad` (used by `DynamicFallback` to auto-detect VBR vs VB365 on remote targets) sets `RedirectStandardOutput`/`RedirectStandardError = true` but never reads either stream. If `Import-Module` emits more output than the pipe buffer (~4KB — plausible with module load errors, verbose assembly-binding failures, or banner text), the child blocks writing, `WaitForExit(15s)` times out, the process is killed, and the module is declared "not available" even though it loaded fine.

## Impact

Wrong auto-detection result in the remote + Auto product path: vHC can conclude neither/wrong product is present and either abort with "Unable to connect ... as either VBR or VB365" or collect the wrong product. Intermittent and environment-dependent, hence Low/Medium.

## Evidence

`vHC/HC_Reporting/Functions/Collection/CCollections.cs:627-649` —

```csharp
var processInfo = new ProcessStartInfo
{
    ...
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    ...
};

using var process = Process.Start(processInfo);
bool exited = process.WaitForExit(timeoutSeconds * 1000);   // streams never read

if (!exited)
{
    try { process.Kill(); } catch { }
    CGlobals.Logger.Info($"[Dynamic Fallback] {productLabel} connection timed out", false);
    return false;
}
```

Neither stream is consumed before or after `WaitForExit`, and the stderr content (the actual reason a module failed to load) is discarded, so failures are also unexplained in the log.

## Suggested fix

Drain both streams with `Task.Run(ReadToEnd)` started before `WaitForExit` (same pattern as `ExecutePsScript`), and log captured stderr at Debug level when the module load fails.
