---
title: "Stop writing DPAPI-protected credentials as plaintext to vhc-monitor.yaml"
severity: High
labels: [security]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:84
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:116
  - vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:173
confidence: High
---

## Summary

`GenerateConfig()` writes the VBR REST credentials — including the cleartext password — into `%APPDATA%\VeeamHealthCheck\monitor\vhc-monitor.yaml` with no encryption and no restrictive ACL. The rest of the app deliberately protects these same credentials with DPAPI (`CredentialStore` uses `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`, see `vHC/HC_Reporting/Startup/CredentialStore.cs:170-171`). `InstallFromVhcData()` decrypts the DPAPI-protected secret and immediately persists it in plaintext, defeating the credential-store protection.

## Impact

Any process or user able to read the profile directory (backup software, sync tools, malware running as the user, admins, forensic copies of the disk) obtains working VBR REST API credentials for the backup server — a high-value target (backup infrastructure is the primary ransomware defense). The file also persists after the monitor's usefulness ends; `Uninstall()` removes only the scheduled task, never the YAML (lines 202-216), so the plaintext password lingers indefinitely.

## Evidence

`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:84` —
```csharp
sb.AppendLine($"    password: \"{EscapeYaml(password)}\"");
```
`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:116` —
```csharp
File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
```
`vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:173` (decrypt-then-plaintext path) —
```csharp
Install(server, creds.Value.Username, creds.Value.Password, notifType, notifUrl, minSeverity);
```
The store the password came from is DPAPI-encrypted (`CredentialStore.cs:170-171`), so this is a deliberate protection being downgraded.

## Suggested fix

Preferred: have vhc-monitor read the password from the existing DPAPI-protected `creds.json` (or a DPAPI-encrypted field in the YAML) instead of cleartext. If the external monitor binary cannot be changed, at minimum: (1) write the file with an ACL restricted to the owning user/SYSTEM (`FileSecurity` deny-inheritance, owner-only), (2) delete the YAML in `Uninstall()`, and (3) document the plaintext storage in the GUI consent text.
