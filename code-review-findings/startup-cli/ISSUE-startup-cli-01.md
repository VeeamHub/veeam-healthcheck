---
title: "EntryPoint.Main discards InitializeProgram's exit code and always returns 0"
severity: High
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/EntryPoint.cs:26
confidence: High
---

## Summary
`Main` captures the result of `CArgsParser.InitializeProgram()` into `res`, logs it, and then unconditionally returns `0`. The carefully-plumbed exit codes from `FullRun` / `CliRun` / `RunMonitorNow` (and any non-zero result from report generation) never reach the OS unless a code path calls `Environment.Exit` directly.

## Impact
The whole silent/unattended feature set (help menu documents exit codes 0-7; Task Scheduler / fleet usage is the advertised scenario) is undermined: a failed run that returns `1` up the call stack still exits the process with `0`. Schedulers, monitoring, and CI wrappers will report success on failed health-check runs. `/monitor:run` exit codes are also swallowed (`return this.RunMonitorNow()` → returned to `Main` → discarded).

## Evidence
`vHC/HC_Reporting/Startup/EntryPoint.cs:26-28`:

```csharp
var res =  ap.InitializeProgram();
CGlobals.Logger.Info("The result is: " + res, true);
return 0;
```

`res` is logged but never returned. Compare with the catch block (lines 30-41), which correctly returns `1`. Meanwhile `CMessages.helpMenu` documents "Exit codes (silent mode): 0 Success / 1 Generic failure / ..." — only the `Environment.Exit(...)` paths inside `CArgsParser` honor that contract; everything that returns through the normal call chain is flattened to 0.

## Suggested fix
```csharp
var res = ap.InitializeProgram();
CGlobals.Logger.Info("The result is: " + res, true);
return res;
```
