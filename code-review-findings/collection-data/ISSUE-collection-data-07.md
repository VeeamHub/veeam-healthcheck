---
title: "VB365 collection ignores exit code and stderr; SCRIPTSUCCESS set true unconditionally"
severity: High
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:865
  - vHC/HC_Reporting/Functions/Collection/CCollections.cs:610
confidence: High
---

## Summary

`InvokeVb365Collect` starts the VB365 collection script without redirecting or reading stderr, never checks the process exit code, and logs "VB365 collection complete!" no matter what happened. Its caller `ExecVb365Scripts` then sets `this.SCRIPTSUCCESS = true;` unconditionally. A completely failed VB365 collection (script error, module missing, auth failure) is indistinguishable from success.

## Impact

Silent wrong/missing data: vHC proceeds to report generation against absent or partial CSVs, producing an empty or misleading VB365 health report with a "success" log trail. This is the textbook partial-data-treated-as-complete failure.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:856-868` —

```csharp
var startInfo = new ProcessStartInfo()
{
    FileName = "powershell.exe",
    Arguments = args,
    UseShellExecute = false,
    CreateNoWindow = true
};
...
var result = Process.Start(startInfo);
...
result.WaitForExit();
this.log.Info("[PS] VB365 collection complete!", false);   // no ExitCode check, no stderr capture
```

`vHC/HC_Reporting/Functions/Collection/CCollections.cs:602-611` —

```csharp
private void ExecVb365Scripts(PSInvoker p)
{
    if (CGlobals.EffectiveIsVb365)
    {
        ...
        p.InvokeVb365Collect();
        this.SCRIPTSUCCESS = true;   // unconditional
    }
}
```

Contrast with the VBR path (`ExecutePsScript`, PSInvoker.cs:402-467), which checks exit code and parses stderr. Also note `result` (Process) is never disposed.

## Suggested fix

Route VB365 collection through `ExecutePsScript` (it already handles redirects, stderr parsing, exit codes, and timeout), have `InvokeVb365Collect` return bool, and set `SCRIPTSUCCESS` from that return value.
