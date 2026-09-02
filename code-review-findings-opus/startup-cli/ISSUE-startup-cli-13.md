# ModeCheck() unreachable branches and missing "Both products" handling create dead/contradictory logic

**Category:** startup-cli
**Severity:** Low
**Type:** Maintainability
**File(s):** `vHC/HC_Reporting/Startup/CClientFunctions.cs:135-196`

## Summary
`ModeCheck()` contains an unreachable branch: after the early `if (!IsVb365 && !IsVbr) return "fail";` at line 160, the identical condition `if (!IsVb365 && !IsVbr)` at line 174 can never be true, so the `GuiImportModeOnly` title is dead code. The method also enumerates **all** running processes on every call (`Process.GetProcesses()`) and re-invokes `GetVbrVersion()` inside the loop, and is called twice during GUI startup (`SetUi` line 97 and indirectly), repeating the full process scan and version detection.

## Evidence
```csharp
if (!CGlobals.IsVb365 && !CGlobals.IsVbr)        // line 160 — returns "fail"
{
    ... return "fail";
}
if (CGlobals.IsVbr && CGlobals.IsVb365) { return ...; }
if (!CGlobals.IsVb365 && !CGlobals.IsVbr)        // line 174 — UNREACHABLE (already returned above)
{
    return title + " - " + VbrLocalizationHelper.GuiImportModeOnly;
}
```

## Impact
Dead code misleads maintainers into thinking an "import-only" title path is live when it never executes; the `GuiImportModeOnly` string is effectively unused here. Repeated full process enumeration + registry version reads on each `ModeCheck()` call is wasteful on startup. Low severity (no incorrect user-facing behavior), but it's a maintainability trap and a small startup-perf cost.

## Suggested Fix
Remove the unreachable second `if`. If an import-only title is desired, gate it on `CGlobals.IMPORT` instead of the impossible condition. Cache the process-scan/version result so `ModeCheck()` is idempotent and cheap on repeated calls.

## Labels
maintainability, dead-code, unreachable, performance, low
