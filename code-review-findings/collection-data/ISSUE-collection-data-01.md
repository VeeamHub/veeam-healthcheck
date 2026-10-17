---
title: "Fix ExecutePsScriptWithFailover so it actually fails over to PowerShell 5"
severity: High
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:194
confidence: High
---

## Summary

`ExecutePsScriptWithFailover` advertises PS7-then-PS5 failover, but an unconditional `return false;` at the bottom of the first loop iteration means the loop body executes exactly once. PowerShell 5 is never attempted, and the post-loop `Environment.Exit(1)` at line 198 is unreachable.

## Impact

On any machine where the PS7 attempt throws (e.g., `Process.Start` fails because pwsh.exe is missing — which is guaranteed by the bug in ISSUE-08 where a non-existent default pwsh path is always returned), the method returns `false` without ever trying `powershell.exe`, even when PS5 is installed and would have worked. Collection silently degrades or aborts on systems that should be fully supported.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:141-199` —

```csharp
foreach (var version in new[] { PowerShellVersion.PowerShell7, PowerShellVersion.PowerShell5 })
{
    ...
    try
    {
        ...
        if (process.ExitCode == 0) { return true; }
        else { ...; return false; }
    }
    catch (Exception ex)
    {
        this.log.Error($"[PS] Exception running script with {version}: {ex.Message}", false);
    }

    return false;   // <-- line 194: runs after the catch on the FIRST iteration; PS5 never tried
}

this.log.Error("[PS] Script failed with all available PowerShell versions. Exiting program", false);
Environment.Exit(1);   // unreachable
```

The `return false;` is inside the `foreach` body, after the try/catch, so the loop can never reach its second iteration.

## Suggested fix

Move `return false;` outside the loop (replacing the `Environment.Exit(1)` path or keeping it as the final fallthrough), and use `continue` after the catch so the PS5 iteration runs. Also reconsider `Environment.Exit(1)` inside a utility method — return false and let the caller decide.
