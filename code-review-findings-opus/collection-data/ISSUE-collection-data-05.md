# ExecutePsScriptWithFailover never fails over: returns/exits inside the version loop

**Category:** collection-data
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:132-200`

## Summary
The method's name and structure promise "try PowerShell 7, then fall back to PowerShell 5". In practice it can never reach PowerShell 5 in the normal path: the first iteration either `return true` (exit 0), `return false` (non-zero exit), or — critically — `return false` unconditionally at the bottom of the loop body (`PSInvoker.cs:194`). The `catch` block logs but then falls through to that same `return false`. The only way to reach iteration 2 is if PowerShell 7's exe path is empty (`continue`), which on this code base is rarely the case because `FindExecutableInPath("pwsh.exe")` returns a hard-coded default path even when pwsh is absent.

## Evidence
```csharp
foreach (var version in new[] { PowerShellVersion.PowerShell7, PowerShellVersion.PowerShell5 })
{
    ...
    try
    {
        ...
        if (process.ExitCode == 0) return true;
        else { this.log.Warning(...); return false; }   // PSInvoker.cs:179-187 — kills failover
    }
    catch (Exception ex)
    {
        this.log.Error(...);                             // logs, then falls through
    }

    return false;                                        // PSInvoker.cs:194 — unconditional, inside loop
}
this.log.Error("[PS] Script failed with all available PowerShell versions...");
Environment.Exit(1);                                     // unreachable in practice
```

## Impact
The PS5 fallback is dead code; a script failure under PS7 is reported as a hard failure even when PS5 would have succeeded. Worse, because `FindExecutableInPath` returns the default `C:\Program Files\PowerShell\7\pwsh.exe` for `pwsh.exe` even when it does not exist (`PSInvoker.cs:95-99`), `GetPowerShellExecutable(PowerShell7)` is non-empty, so the `continue` path that would advance to PS5 is also skipped — leading to a "PowerShell executable not found" exception or a process-start failure rather than graceful failover. The trailing `Environment.Exit(1)` is unreachable, so the intended "all versions failed → exit" contract is also broken.

## Suggested Fix
Move `return false` / exit handling outside the loop. On a failed attempt (non-zero exit or caught exception), record the failure and `continue` to the next version; only return success on exit 0; after the loop, log and exit/return false. Also make `FindExecutableInPath` return null (not a hard-coded path) when pwsh.exe is genuinely absent so the `continue` branch works.

## Labels
bug, control-flow, dead-code, failover, collection
