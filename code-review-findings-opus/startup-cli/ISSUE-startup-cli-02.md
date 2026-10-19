# Main() always returns 0 on success path — real exit code from report run is discarded

**Category:** startup-cli
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/EntryPoint.cs:26-28`, `vHC/HC_Reporting/Startup/CArgsParser.cs:662-671`

## Summary
`Main()` captures the result of `InitializeProgram()` into `res`, logs it, and then unconditionally `return 0`. The carefully-threaded `int` return value that flows up from `CReportModeSelector.Run()` → `CClientFunctions.Import()` → `StartAnalysis()` → `FullRun()` → `ParseAllArgs()` is computed and then thrown away. Any non-zero failure code produced by report generation is reported to the OS as success.

## Evidence
```csharp
CArgsParser ap = new(args);
var res =  ap.InitializeProgram();
CGlobals.Logger.Info("The result is: " + res, true);
return 0;        // <-- res ignored; always success
```
For contrast, `StartAnalysis()` deliberately returns `1` on import-path failure:
```csharp
if (!this.ResolveImportPath())
{
    this.LOG.Error(... "Failed to resolve import path. Exiting.", false);
    return 1;
}
```
That `1` propagates up to `res` and is then discarded.

## Impact
Unattended / Task Scheduler / fleet runs (the explicit use case in the `/silent` help text) cannot detect failures via process exit code on the normal completion path. A failed import or failed analysis exits 0, so automation treats broken reports as healthy. Only exceptions (caught at line 30) yield exit 1; in-band error codes are lost. This directly undermines the silent-mode exit-code contract documented in `CMessages.helpMenu`.

## Suggested Fix
Return the computed result:
```csharp
var res = ap.InitializeProgram();
CGlobals.Logger.Info("The result is: " + res, true);
return res;
```

## Labels
bug, exit-code, silent-mode, automation, high
