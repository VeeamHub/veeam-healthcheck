# CHotfixDetector silently no-ops when path is invalid — null path flows into Run()

**Category:** startup-cli
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CHotfixDetector.cs:24-43`, `vHC/HC_Reporting/Startup/CClientFunctions.cs:243-251`

## Summary
The `CHotfixDetector` constructor only assigns `originalPath` / calls `SetPath()` when `VerifyPath(path)` succeeds. If verification fails, `originalPath` and `this.path` remain null, but the object is still constructed and `Run()` is still called by `CClientFunctions.RunHotfixDetector` (which `new CHotfixDetector(path); hfd.Run();` unconditionally). `Run()` → `ExecLogCollection()` then uses the null `this.path` in `Path.Combine`/`ps.RunVbrLogCollect(this.path, server)`.

## Evidence
```csharp
public CHotfixDetector(string path)
{
    this.fixList = new List<string>();
    CClientFunctions funk = new();
    if (funk.VerifyPath(path))     // on false: originalPath/path stay null
    {
        this.originalPath = path;
        this.SetPath();
    }
}
```
Caller does not check construction validity:
```csharp
CHotfixDetector hfd = new(path);
hfd.Run();
```
Note `RunHotfixDetector` also reads `path = Console.ReadLine()` when path is empty (CClientFunctions.cs:247) — a blocking interactive prompt that violates `/silent` mode.

## Impact
A bad `/path=` (or a `Console.ReadLine` that returns null/EOF under redirected stdin) yields a detector with a null working path; `Run()` then either NREs or passes a null/garbage path into PowerShell log collection, producing a confusing failure rather than the clean "invalid path" message that `RunHotfixDetector`'s own pre-check (CClientFunctions.cs:228) was trying to provide. The interactive `Console.ReadLine()` fallback also blocks unattended/silent runs.

## Suggested Fix
- Have the constructor throw (or set a validity flag) on failed verification, and have `RunHotfixDetector` bail before calling `Run()`.
- Guard the `Console.ReadLine()` prompt behind `!CGlobals.Silent` and fail fast (exit code) in silent mode.

## Labels
bug, null-reference, hotfix, silent-mode, low
