---
title: "Error paths call Environment.Exit(0), masking failure; SilentExit.NoProductDetected (7) is never used"
severity: High
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:353
  - vHC/HC_Reporting/Startup/CArgsParser.cs:376
  - vHC/HC_Reporting/Startup/CClientFunctions.cs:100
  - vHC/HC_Reporting/Common/SilentExit.cs:28
confidence: High
---

## Summary
Three clear failure paths terminate the process with exit code 0 (success): `/run /remote` with no host, `/run` with no Veeam product detected, and local VB365 execution without admin rights. `SilentExit` defines `NoProductDetected = 7` and the help menu documents "7 No Veeam product detected and no /host= provided", but the code path that actually hits that condition exits 0 instead.

## Impact
Unattended/scheduled runs (the documented use case for the exit-code table) cannot distinguish these failures from success. A fleet rollout with a typo'd command line or a server where the Veeam service is stopped reports green across the board, and no report is produced.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:351-354`:

```csharp
CGlobals.Logger.Warning("Remote execution selected but no host defined. Please define host: " +
    "/host=HOSTNAME", false);
Environment.Exit(0);
```

`vHC/HC_Reporting/Startup/CArgsParser.cs:373-377`:

```csharp
if (this.functions.ModeCheck() == "fail")
{
    CGlobals.Logger.Error("No compatible software detected or remote host specified. Exiting.", false);
    Environment.Exit(0);
}
```

This is precisely the `SilentExit.NoProductDetected` (= 7) condition from `vHC/HC_Reporting/Common/SilentExit.cs:28` and `CMessages.helpMenu` ("7  No Veeam product detected and no /host= provided") — yet exit code 7 is referenced nowhere in the codebase besides its declaration.

`vHC/HC_Reporting/Startup/CClientFunctions.cs:94-101` — local VB365 without admin:

```csharp
string message = "Please run program as Administrator";
...
CGlobals.Logger.Error(message, false);
Environment.Exit(0);
```

## Suggested fix
- No-host case: `Environment.Exit(SilentExit.GenericFailure)` (or a dedicated code).
- ModeCheck "fail" case: `Environment.Exit(SilentExit.NoProductDetected)`.
- VB365-without-admin: `Environment.Exit(SilentExit.GenericFailure)`.
Keep `Environment.Exit(0)` only for genuinely user-chosen aborts (e.g., declining the admin dialog in GUI mode).
