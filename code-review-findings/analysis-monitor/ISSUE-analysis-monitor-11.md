---
title: "IsTaskRegistered spawns PowerShell synchronously (up to 30s) and is called on the WPF UI thread"
severity: Medium
labels: [performance, reliability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:38
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:53
confidence: High
---

## Summary

`IsTaskRegistered()` shells out to `powershell -Command Get-ScheduledTask ...` and blocks for up to the 30s default timeout; `GetInstalledVersion()` does the same with the monitor exe. `VhcGui.InitializeMonitorStatus()` calls both directly on the UI thread (`VhcGui.xaml.cs:602-623`), and `monitorRunBtn_Click`'s completion callback calls `IsTaskRegistered()` inside `Dispatcher.Invoke` (`VhcGui.xaml.cs:732`) — i.e., back on the UI thread. PowerShell cold-start alone is routinely 1-3 seconds; a busy server makes it worse.

## Impact

The GUI freezes (no paint, no input) for seconds every time monitor status is refreshed — at window init and after every "Run now". Worst case is a 30-second hang if PowerShell stalls. Users perceive the app as crashed. A duplicated magic string (`'VHC Monitor'` literal at line 43/208 vs the `TaskName` constant at line 26) also means a future rename breaks detection silently.

## Evidence

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:38-44` —
```csharp
public static bool IsTaskRegistered()
{
    try
    {
        var (exitCode, stdout, _) = RunProcess("powershell",
            "-NoProfile -Command \"Get-ScheduledTask -TaskName 'VHC Monitor' -ErrorAction SilentlyContinue | ...\"");
```
UI-thread callers, `vHC/HC_Reporting/VhcGui.xaml.cs:602-604`:
```csharp
bool bundled = CVhcMonitorIntegration.IsExePresentInBundle();
bool installed = CVhcMonitorIntegration.IsInstalled();
bool taskActive = CVhcMonitorIntegration.IsTaskRegistered();
```
and `VhcGui.xaml.cs:730-733` (inside `Dispatcher.Invoke`):
```csharp
this.Dispatcher.Invoke(() =>
{
    this.InitializeMonitorStatus();
    monitorRunBtn.IsEnabled = CVhcMonitorIntegration.IsTaskRegistered();
});
```

## Suggested fix

Make the status check async (`Task<bool> IsTaskRegisteredAsync()` or run `InitializeMonitorStatus`'s probing portion in `Task.Run` and marshal only the UI updates), and/or replace the PowerShell round-trip with the Task Scheduler COM API (`TaskService` via Microsoft.Win32.TaskScheduler or `schtasks /query /tn` which starts far faster). Reuse the `TaskName` constant instead of the duplicated literal at lines 43 and 208.
