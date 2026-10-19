# IsTaskRegistered uses substring match, can report false positive

**Category:** analysis-monitor
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:38-51`

## Summary
`IsTaskRegistered` filters `Get-ScheduledTask` to the exact task name `'VHC Monitor'`, but then validates the result with `stdout.Contains("VHC Monitor")`. Because the check is a substring `Contains` rather than an exact/equality match, any task whose name contains the string "VHC Monitor" (e.g., `VHC Monitor v2`, `Old VHC Monitor`) would also satisfy it. In practice the PowerShell filter already constrains the query, so the real residual risk is mainly that whitespace/casing differences in the returned name are tolerated inconsistently — but the loose match is a latent correctness smell for status reporting.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:42-44
var (exitCode, stdout, _) = RunProcess("powershell",
    "-NoProfile -Command \"Get-ScheduledTask -TaskName 'VHC Monitor' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty TaskName\"");
return exitCode == 0 && stdout.Contains("VHC Monitor");
```

## Impact
Status display in the GUI/CLI may report the monitor as registered when a differently-named task happens to contain the substring, or when the query returns an unexpected line. Low impact (status only), but it can mislead the operator about whether monitoring is actually active.

## Suggested Fix
Compare the trimmed stdout for equality against `TaskName` (`stdout.Trim().Equals("VHC Monitor", StringComparison.OrdinalIgnoreCase)`) or, better, check the exit/`$?` of `Get-ScheduledTask` directly. Reuse the `TaskName` constant (line 26) instead of the hardcoded literal so the name stays in sync.

## Labels
bug, status-check, monitor
