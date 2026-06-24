# Uninstall leaves plaintext-credential config and copied exe on disk

**Category:** analysis-monitor
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:202-216`

## Summary
`Uninstall()` only unregisters the scheduled task. It does not remove `vhc-monitor.yaml` (which contains the plaintext VBR password per ISSUE-01), the copied `vhc-monitor.exe`, or the `veeam-monitor-state.json` state file. A user who installs and then "uninstalls" the monitor reasonably expects the credential to be gone, but it persists indefinitely in `%APPDATA%\VeeamHealthCheck\monitor\`.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:202-216
public static void Uninstall()
{
    CGlobals.Logger.Info("Removing VHC Monitor scheduled task...", false);
    try
    {
        RunProcess("powershell",
            "-NoProfile -Command \"Unregister-ScheduledTask -TaskName 'VHC Monitor' -Confirm:$false -ErrorAction SilentlyContinue\"",
            15000);
        CGlobals.Logger.Info("VHC Monitor scheduled task removed.", false);
    }
    ...
}
// No File.Delete(ConfigPath), no Directory.Delete(InstallDir)
```
Files left behind are created at `:116` (`ConfigPath`) and `:129` (`ExeInstalledPath`).

## Impact
Plaintext credential (ISSUE-01) survives an explicit uninstall, widening and lengthening the exposure window and violating user expectation that uninstall removes secrets.

## Suggested Fix
In `Uninstall()`, after unregistering the task, delete `ConfigPath` (and ideally the whole `InstallDir`) and the state file. If keeping the exe is desired for reinstall, at minimum delete the config containing the credential.

## Labels
security, cleanup, secrets-at-rest, monitor
