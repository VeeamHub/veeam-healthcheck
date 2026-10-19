---
title: "Guard RunNow/TestConnection against missing exe — Process.Start exception escapes into Task.Run and strands the GUI"
severity: Medium
labels: [reliability, bug]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:176
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:190
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:282
confidence: High
---

## Summary

`RunNow()` and `TestConnection()` invoke `RunProcess(ExeInstalledPath, ...)` without checking `IsInstalled()` and without any try/catch. `RunProcess` calls `process.Start()` bare (line 282), which throws `Win32Exception` if the exe was deleted/moved (or AV-quarantined). The GUI's run button executes `RunNow()` inside `Task.Run` with no try/catch (`VhcGui.xaml.cs:725-735`), so the exception becomes an unobserved task exception: the `Dispatcher.Invoke` continuation never runs, the button stays disabled, and the status text reads "Running..." forever.

## Impact

A common, user-reachable failure (monitor exe removed, AV quarantine, profile cleanup) produces a silently hung UI element with no error message and no log entry. The CLI path (`CArgsParser.cs:697`) gets an unhandled-exception crash instead of a clean error.

## Evidence

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:176-188` — no existence check, no exception handling:
```csharp
public static (int exitCode, string output) RunNow()
{
    CGlobals.Logger.Info("Running vhc-monitor on demand...", false);
    var (exitCode, stdout, stderr) = RunProcess(ExeInstalledPath,
        $"all --config \"{ConfigPath}\"", 120000);
```
`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:282` —
```csharp
process.Start();
```
Caller with no catch, `vHC/HC_Reporting/VhcGui.xaml.cs:725-734`:
```csharp
System.Threading.Tasks.Task.Run(() =>
{
    var (exitCode, output) = CVhcMonitorIntegration.RunNow();
    this.Dispatcher.Invoke(() => { ... });
});
```
(Contrast with the install buttons at VhcGui.xaml.cs:673-690, which do wrap their Task bodies in try/catch.)

## Suggested fix

In `RunNow()`/`TestConnection()`, check `IsInstalled()` and `File.Exists(ConfigPath)` first and return a failure tuple; wrap `process.Start()` in `RunProcess` with a catch that logs and returns `(-1, "", ex.Message)`. Add the same try/catch pattern to `monitorRunBtn_Click`'s Task body as the install handlers already use.
