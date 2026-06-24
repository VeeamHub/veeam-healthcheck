---
title: "TestMfa passes plaintext password on the PowerShell command line"
severity: High
labels: [security]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:269
confidence: High
---

## Summary

`TestMfa` builds a `powershell.exe` argument string containing the user's password in plaintext (single-quote escaped, but not encoded or hidden). Process command lines on Windows are readable by any local user via Task Manager, `wmic process`, `Get-CimInstance Win32_Process`, ETW/process-creation auditing (Event ID 4688 with command-line logging), and EDR telemetry.

Every other credential path in this codebase (VbrConfigStartInfo, ConfigStartInfo, BuildVb365Arguments, MfaTestPassed) was migrated to `-PasswordBase64`; `TestMfa` was missed. Base64 is only obfuscation, but `TestMfa` doesn't even have that — and these are VBR administrator credentials.

## Impact

Disclosure of backup-infrastructure admin credentials to any local process/user able to enumerate process command lines, and potential persistence in security event logs that capture process command lines. Backup admin creds are a prime ransomware target.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:265-274` —

```csharp
ProcessStartInfo startInfo = new ProcessStartInfo
{
    FileName = "powershell.exe",
    // Use single quotes for the password to avoid interpretation of special characters
    Arguments = $"Import-Module Veeam.Backup.PowerShell; Connect-VBRServer -Server '{escapedServer}' -User '{escapedUser}' -Password '{escapedPassword}'",
    ...
};
```

The escaping prevents argument injection but the secret is still verbatim on the command line. The masked-logging effort at lines 276-286 protects the vHC log, but not the OS-visible command line.

## Suggested fix

Match the `MfaTestPassed` pattern: invoke `TestMfa.ps1` via `-File` with `-PasswordBase64`, or better, write the secret to the child's stdin / use an environment variable consumed by the script, so it never appears in `Win32_Process.CommandLine`.
