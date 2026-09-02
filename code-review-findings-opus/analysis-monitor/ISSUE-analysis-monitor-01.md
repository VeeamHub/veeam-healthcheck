# VBR credential password written to disk in cleartext YAML

**Category:** analysis-monitor
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:68-118`, `:163-174`

## Summary
`GenerateConfig` writes the VBR account password to `%APPDATA%\VeeamHealthCheck\monitor\vhc-monitor.yaml` in plaintext. The rest of the product is careful to never persist passwords unencrypted — `CredentialStore` encrypts them at rest with DPAPI (`ProtectedData.Protect`, see `Startup/CredentialStore.cs:170`). `InstallFromVhcData` explicitly DPAPI-*decrypts* the stored credential (`CredentialStore.Get` → `ProtectedData.Unprotect`) and then hands the plaintext to `GenerateConfig`, which writes it to a flat file. This undoes the at-rest protection the credential store provides and leaves a long-lived plaintext copy of a Veeam Backup & Replication credential on disk.

## Evidence
```csharp
// CVhcMonitorIntegration.cs:83-84  (inside GenerateConfig)
sb.AppendLine($"    username: \"{EscapeYaml(username)}\"");
sb.AppendLine($"    password: \"{EscapeYaml(password)}\"");   // <-- plaintext password
...
// CVhcMonitorIntegration.cs:116
File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);  // no ACL hardening
```
```csharp
// CVhcMonitorIntegration.cs:163-173  (InstallFromVhcData)
var creds = CredentialStore.Get(server);          // DPAPI-decrypts to plaintext
...
Install(server, creds.Value.Username, creds.Value.Password, ...);  // plaintext flows to YAML
```
Contrast with the credential store's design intent:
```csharp
// Startup/CredentialStore.cs:170-172
var enc = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
```

## Impact
A VBR credential (often a high-privilege backup/admin account) is persisted unencrypted in a predictable path that survives reboots and the VHC process. Anyone able to read the user's `%APPDATA%` (local admins, backup operators, EDR/backup agents copying the profile, an attacker who has any read access to the profile) recovers the password verbatim — no DPAPI scope, no key. The file is also not deleted by `Uninstall()` (which only unregisters the scheduled task, lines 202-216), so it lingers indefinitely.

## Suggested Fix
- Prefer DPAPI-protected delivery to the monitor (e.g., write the password as a DPAPI/`CurrentUser`-scoped blob the monitor can `Unprotect`, or pass it via a Windows credential-manager target the monitor reads) rather than cleartext YAML.
- If cleartext is unavoidable for the monitor's config format, at minimum: (a) set a restrictive ACL on `vhc-monitor.yaml` (owner-only via `FileSecurity`/`SetAccessControl`) immediately after write, and (b) delete the config file in `Uninstall()`.
- Document the residual risk and ensure the file is excluded from any diagnostic/scrubbed bundles.

## Labels
security, credentials, secrets-at-rest, monitor
