---
title: "Scheduled task registered without principal/logon type — monitor likely stops when user logs off"
severity: Medium
labels: [reliability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:136
confidence: Medium
---

## Summary

The `Register-ScheduledTask` call in `Install()` supplies only an action, trigger, settings, and `-RunLevel Limited`. No `-Principal` (or `-User`/`-Password`) is given, so the task is registered for the current user with the default interactive logon type — i.e. "Run only when user is logged on". A continuous monitor that fires every 5 minutes will silently stop running the moment the installing user logs off or the server reboots to the lock screen.

## Impact

The whole point of vhc-monitor is unattended, continuous monitoring of backup infrastructure. With the default principal, monitoring only happens during interactive sessions of the installing user — on a server that is almost never. Users believe monitoring/alerting is active when it is not; missed repo-health/retention alerts are the silent consequence. Additionally, the action and config live under that user's `%APPDATA%` (lines 15-21), which would break if the task were later switched to SYSTEM without moving the files.

## Evidence

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:136-141` —
```csharp
string ps1 = $@"
$action = New-ScheduledTaskAction -Execute ""{ExeInstalledPath}"" -Argument ""all --config `""{ConfigPath}`""""
$trigger = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 5) -Once -At (Get-Date)
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 4) -StartWhenAvailable $true
Register-ScheduledTask -TaskName ""{TaskName}"" -Action $action -Trigger $trigger -Settings $settings -RunLevel Limited -Force
";
```
No `-Principal`/`-User` argument; default logon type does not run when the user is logged off (no S4U/ServiceAccount principal, no "whether user is logged on or not" equivalent).

## Suggested fix

Register with an explicit principal that runs without an interactive session, e.g.:

```powershell
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType S4U -RunLevel Limited
Register-ScheduledTask ... -Principal $principal
```

(S4U keeps DPAPI CurrentUser scope working if the config is later DPAPI-protected per ISSUE-01.) Verify the chosen logon type against where the config/exe are stored.
