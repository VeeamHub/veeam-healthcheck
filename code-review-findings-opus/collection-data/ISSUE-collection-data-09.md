# ExecutePsScript / RunVbrLogCollect / RunServerDump leak undisposed Process objects

**Category:** collection-data
**Severity:** Low
**Type:** Resource Leak
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:402-467`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:600-621`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:245-247`

## Summary
Several process-launch methods create `Process` instances without `using`/`Dispose`. `ExecutePsScript` does `var res1 = new Process()` and never disposes it; `RunServerDump`/`RunVbrLogCollect` capture the result of `Process.Start(...)` without disposing; `TestMfa` creates `var res = new Process()` at method top and never disposes it. Each leaks the underlying process/handle (and the stdout/stderr pipe handles) until GC finalization.

## Evidence
```csharp
// PSInvoker.cs:404-406
var res1 = new Process();
res1.StartInfo = startInfo;
res1.Start();         // never res1.Dispose()
```
```csharp
// PSInvoker.cs:603-605
var result = Process.Start(p);
this.log.Info(... + result.Id ...);
result.WaitForExit();   // result never disposed
```
```csharp
// PSInvoker.cs:247
var res = new Process();   // disposed nowhere in TestMfa
```
Contrast with the correct pattern used in `Ps7Executor` and `CCollections` (`using var process = Process.Start(...)`).

## Impact
Each collection run leaks a handful of process and pipe handles. For a single run this is minor (hence Low), but in long-lived/loop scenarios (e.g. `RunVbrLogCollect` is called once per server in a foreach at `CHotfixDetector.cs:156-159`) handles accumulate until finalization, and the redirected stdout/stderr pipes are held longer than necessary.

## Suggested Fix
Wrap each `Process` in `using` (or call `Dispose()` in a `finally`). E.g. `using var res1 = new Process { StartInfo = startInfo };` and `using var result = Process.Start(p);`.

## Labels
resource-leak, process, collection
