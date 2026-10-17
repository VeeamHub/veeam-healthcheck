# Scheduled-task PowerShell script built by string interpolation with fragile quote-only escaping

**Category:** analysis-monitor
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:136-156`

## Summary
The scheduled-task registration runs PowerShell built by interpolating `ExeInstalledPath` and `ConfigPath` into a here-string, then passing the whole thing through `-Command "..."` after only replacing `"` with `\"`. The interpolated paths are derived from `Environment.GetFolderPath(ApplicationData)`, which is influenced by the `APPDATA` environment variable and the user's profile path. If that path contains PowerShell metacharacters (a backtick, `$(...)`, or — for a maliciously set `APPDATA` — `"` `;`), the quote-only escaping does not neutralize them and arbitrary PowerShell can execute during install.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:136-143
string ps1 = $@"
$action = New-ScheduledTaskAction -Execute ""{ExeInstalledPath}"" -Argument ""all --config `""{ConfigPath}`""""
$trigger = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 5) -Once -At (Get-Date)
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 4) -StartWhenAvailable $true
Register-ScheduledTask -TaskName ""{TaskName}"" -Action $action -Trigger $trigger -Settings $settings -RunLevel Limited -Force
";
var (exitCode, stdout, stderr) = RunProcess("powershell",
    $"-NoProfile -Command \"{ps1.Replace("\"", "\\\"")}\"", 30000);  // only escapes "
```
`ExeInstalledPath`/`ConfigPath` resolve under:
```csharp
// CVhcMonitorIntegration.cs:15-21
Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "VeeamHealthCheck", "monitor");
```

## Impact
Low likelihood (requires a username/profile path or `APPDATA` containing PowerShell-active characters such as a backtick or `$(`), but the consequence is code execution in the install context. The escaping also simply breaks for any path containing such characters even without malice, causing silent install failure (the base64 `-EncodedCommand` fallback at line 149 is robust and works — so the brittle primary path is also redundant).

## Suggested Fix
- Prefer the `-EncodedCommand` path as the primary mechanism (it already exists as the fallback and avoids all shell-quoting issues).
- Or register the task via the `TaskScheduler` COM/`Microsoft.Win32.TaskScheduler` API instead of shelling out to PowerShell, eliminating string-based command construction entirely.
- Validate/normalize the install path and reject paths containing shell-active characters.

## Labels
security, powershell, command-injection, monitor
