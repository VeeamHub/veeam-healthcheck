---
title: "FindExecutableInPath returns hardcoded pwsh.exe default without existence check, poisoning PowerShell selection"
severity: High
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:95
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:555
confidence: High
---

## Summary

When `pwsh.exe` is not found anywhere on PATH, `FindExecutableInPath` returns the hardcoded default `C:\Program Files\PowerShell\7\pwsh.exe` *without* checking `File.Exists`. The same fallback is not applied to `powershell.exe` (returns null). Consequently `this.pwshPath` is always non-null, `preferredVersion` is always `PowerShell7`, and every "prefer pwsh" decision treats PS7 as installed even on machines that don't have it.

## Impact

- `VbrConfigStartInfo` (line 555) and `ConfigStartInfo` (line 751): for VBR v13+, `exePath = this.pwshPath` is chosen because it's non-empty; if PS7 is genuinely absent, `Process.Start` throws `Win32Exception` (file not found), VBR collection fails, and — because of the broken failover in ISSUE-01 — PowerShell 5 is never attempted.
- `ExecutePsScriptWithFailover` wastes its PS7 attempt on a nonexistent binary and (due to ISSUE-01) then returns false.

Net effect: on a VBR v13 server without PowerShell 7 installed, collection aborts instead of falling back.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:93-103` —

```csharp
// Return default path if not found in PATH
if (exeName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase))
{
    return @"C:\Program Files\PowerShell\7\pwsh.exe";   // never verified with File.Exists
}

return null;
```

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:555-558` —

```csharp
if (!string.IsNullOrEmpty(this.pwshPath) && !(CGlobals.VBRMAJORVERSION < 13))
{
    exePath = this.pwshPath;   // pwshPath is ALWAYS non-empty due to the default above
}
```

## Suggested fix

In `FindExecutableInPath`, only return the default path when `File.Exists` confirms it; otherwise return null so `DetectPowerShellVersions` correctly records PS7 as unavailable and selection falls back to `powershell.exe`.
