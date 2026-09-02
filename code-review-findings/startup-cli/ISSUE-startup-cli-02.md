---
title: "/run /host=<local machine name> skips ModeCheck, collects nothing, and exits 0"
severity: High
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:250
  - vHC/HC_Reporting/Startup/CArgsParser.cs:366
  - vHC/HC_Reporting/Functions/Collection/CCollections.cs:173
confidence: High
---

## Summary
The Issue #82 fix maps `/host=<name-of-local-machine>` to local mode by setting `REMOTEEXEC = false` and `REMOTEHOST = "localhost"`. But the run dispatch then takes the `REMOTEHOST != string.Empty` branch, which calls `FullRun` directly and **never calls `ModeCheck()`** — the only CLI code path that sets `CGlobals.IsVbr` / `CGlobals.IsVb365`. With `TargetProductType == Auto`, `EffectiveIsVbr`/`EffectiveIsVb365` are both false, and `CCollections.ExecPSScripts`'s dynamic fallback only fires when `REMOTEEXEC` is true — so no collection scripts run at all.

## Impact
`VeeamHealthCheck.exe /run /host=MYVBRSERVER` executed *on* MYVBRSERVER (a very natural invocation, and exactly the scenario Issue #82 targeted) silently does nothing: no scripts run, the VBR output dir is never created, `CReportModeSelector.FileChecker` finds no directories, and the process exits 0 (compounded by ISSUE-startup-cli-01). The user gets no report and no error.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:250-260` — local host detected, `REMOTEHOST` set to `"localhost"` (non-empty):

```csharp
if (CHostNameHelper.IsLocalHost(providedHost))
{
    ...
    CGlobals.REMOTEEXEC = false;
    CGlobals.REMOTEHOST = "localhost";
}
```

`vHC/HC_Reporting/Startup/CArgsParser.cs:366-380` — because `REMOTEHOST != string.Empty`, the dispatch bypasses the `else` branch that runs `ModeCheck()`:

```csharp
else if(CGlobals.REMOTEHOST != string.Empty)
{
    CGlobals.Logger.Debug("Remote execution selected with host: " + CGlobals.REMOTEHOST, false);
    result = this.FullRun(targetDir);
}
else
{
    if (this.functions.ModeCheck() == "fail") ...
```

`CGlobals.IsVbr` is only set in `CClientFunctions.ModeCheck()` (process scan) and the import-path resolver — neither runs here. `vHC/HC_Reporting/Functions/Collection/CCollections.cs:169-176`:

```csharp
bool runVbr = CGlobals.EffectiveIsVbr;      // false
bool runVb365 = CGlobals.EffectiveIsVb365;  // false
// Dynamic fallback when remote + Auto + no local detection
if (CGlobals.TargetProductType == TargetProduct.Auto && CGlobals.REMOTEEXEC && !runVbr && !runVb365)
```

`REMOTEEXEC` is false, so the fallback is skipped and neither `runVbr` nor `runVb365` is ever true.

## Suggested fix
In the dispatch, treat `REMOTEHOST == "localhost"` (or `!REMOTEEXEC`) the same as the no-host case and run `ModeCheck()` before `FullRun`:

```csharp
else if (CGlobals.REMOTEEXEC && CGlobals.REMOTEHOST != string.Empty)
{
    result = this.FullRun(targetDir);
}
else
{
    if (this.functions.ModeCheck() == "fail") { ... exit ... }
    result = this.FullRun(targetDir);
}
```
