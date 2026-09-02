---
title: "Ps7Executor.IsModuleInstalled always returns true; RunScript invokes script path via -Command"
severity: Low
labels: [bug, maintainability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:39
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:68
confidence: High
---

## Summary

`Ps7Executor` appears to be unused (no references found outside the file), which caps severity at Low, but it contains latent bugs that will bite whoever wires it up:

1. `IsModuleInstalled` writes `'0'`/`'1'` to stdout via `Write-Host` but then returns `process.ExitCode == 0`. pwsh exits 0 whenever the command itself runs without a terminating error — regardless of whether the module exists. The stdout sentinel is never read, so the method returns `true` for any module name.
2. `RunScript` runs `-Command \"{scriptPath}\"` — with `-Command`, the path is parsed as a PowerShell expression: paths with spaces break, and a path is an arbitrary command-string seam; `-File` is the correct switch. It also reads stdout then stderr sequentially before `WaitForExit` (see deadlock pattern in ISSUE-02).
3. `EmbeddedScript`/`vbrConfigScript` fields and `ExecuteScript`'s `ps.AddScript(scriptName)` (treats a *name* parameter as script *content*) suggest half-finished scaffolding.

## Impact

Dead code today; incorrect module detection and fragile script invocation the moment it's adopted. Misleading API for future maintainers.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:39-53` —

```csharp
string args = $"-NoProfile -Command \"if (Get-Module -ListAvailable -Name '{moduleName}') {{ Write-Host '0' }} else {{ Write-Host '1' }}\"";
...
using var process = Process.Start(psi);
process.WaitForExit();
return process.ExitCode == 0;   // always 0 — Write-Host output never inspected
```

`vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:68` —

```csharp
string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{scriptPath}\" ";
```

## Suggested fix

Either delete `Ps7Executor` (PSInvoker covers its responsibilities) or fix it: read stdout and compare the sentinel (or use `exit 0/1` in the PS snippet and test ExitCode), switch `RunScript` to `-File "{scriptPath}"`, and apply the concurrent stream-read pattern.
