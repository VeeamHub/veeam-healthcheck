# Decrypted plaintext passwords passed to monitor install and held in GUI locals

**Category:** startup-cli
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Startup/CredentialStore.cs:133-145`, `vHC/HC_Reporting/Startup/CArgsParser.cs:680-687`, `vHC/HC_Reporting/VhcGui.xaml.cs:647-691`

## Summary
`CredentialStore.Get()` returns the DPAPI-decrypted password as a plain `string`. That plaintext is then handed to `CVhcMonitorIntegration.Install(server, username, password, ...)` from both the CLI `/monitor:setup` path and the GUI quick-setup button. Plaintext credentials in `string` form cannot be zeroed (immutable, GC-managed) and are at risk of landing in the monitor's on-disk config, scheduled-task arguments, or process memory dumps. The username-injection hardening applied to `/credfile=` (CArgsParser.cs:531) is not applied to the password handed to monitor install, and there is no indication the monitor consumer stores it encrypted.

## Evidence
```csharp
// CredentialStore.Get returns plaintext
var password = Encoding.UTF8.GetString(
    ProtectedData.Unprotect(val.PasswordEnc, null, DataProtectionScope.CurrentUser));
return (val.Username, password);
```
```csharp
// CArgsParser.RunMonitorSetup
var creds = CredentialStore.Get(CGlobals.VBRServerName);
CVhcMonitorIntegration.Install(CGlobals.VBRServerName, creds.Value.Username, creds.Value.Password);
```
```csharp
// VhcGui.monitorQuickSetupBtn_Click
string password = creds?.Password ?? string.Empty;
...
CVhcMonitorIntegration.Install(server, username, password, notifType, notifUrl, minSeverity);
```

## Impact
If `CVhcMonitorIntegration.Install` persists the password to `vhc-monitor.yaml` or passes it as a scheduled-task command-line argument, the credential becomes recoverable in cleartext by any local user/process that can read the config or enumerate task definitions — a meaningful downgrade from the DPAPI-at-rest model the credential store otherwise enforces. Even absent that, the plaintext lingers in managed heap memory for the GUI's lifetime.

## Suggested Fix
- Audit `CVhcMonitorIntegration.Install` (out of this category's file scope) to confirm the password is re-encrypted (DPAPI) before being written and never placed on a command line; if it is, that is a Critical finding to file against the Monitor subsystem.
- Prefer passing the still-encrypted blob (or a `SecureString`/`byte[]` the monitor decrypts itself) rather than a plaintext `string`.
- Validate the password for control characters before it can be splatted into any shell/task argument, mirroring the `/credfile=` username sanitization.

## Labels
security, credentials, dpapi, monitor, medium
