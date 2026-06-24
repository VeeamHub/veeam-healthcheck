# Cleartext password passed as -Password on the powershell.exe command line in TestMfa (process-listing disclosure)

**Category:** collection-security
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:269`

## Summary
`TestMfa()` builds the PowerShell invocation by interpolating the cleartext password directly into the `-Password '...'` command-line argument of `powershell.exe`. Even though the value is single-quote-escaped (preventing injection) and the *log* line is masked, the process command line itself is readable by any local process that can enumerate processes (Task Manager → Details → Command line, `wmic process get CommandLine`, `Get-CimInstance Win32_Process`, ETW/Sysmon ProcessCreate events). For the brief lifetime of the child process the VBR password is exposed in cleartext system-wide.

## Evidence
```csharp
Arguments = $"Import-Module Veeam.Backup.PowerShell; Connect-VBRServer -Server '{escapedServer}' -User '{escapedUser}' -Password '{escapedPassword}'",   // line 269
```
`escapedPassword` is the plaintext password with single quotes doubled — i.e. cleartext on the command line. Contrast this with the hardened collection paths that deliberately avoid raw `-Password`:
- `TestMfaVB365` (line 357-365) passes `-PasswordBase64` and reconstructs via `ConvertTo-SecureString` inside the script.
- `VbrConfigStartInfo`/`ConfigStartInfo`/`BuildVb365Arguments` all use `-PasswordBase64 "..."`.

`TestMfa` is the one path still using a literal `-Password` on the command line. (Base64 would not fix the disclosure on its own — it is trivially reversible — but the other paths combine it with SecureString reconstruction; the real issue is putting the credential on the argv at all.)

## Impact
A local unprivileged user or endpoint-monitoring agent can capture the live VBR credential by sampling process command lines while Health Check runs its MFA test. The masking applied to logs gives a false sense that the password is protected; the OS-level command line is not masked.

## Suggested Fix
- Do not pass the password as a process argument. Feed it to the child PowerShell via stdin (write the `ConvertTo-SecureString`/`PSCredential` construction script to the process's standard input), or via an environment variable scoped to the child process, or by using the embedded in-process `PowerShell.Create()` API already used in `ExecuteEmbeddedScript`.
- At minimum, align `TestMfa` with `TestMfaVB365`: pass `-PasswordBase64` and build the `PSCredential` inside the script so the raw password never appears on argv (still combine with stdin for full protection).

## Labels
security, credential-disclosure, command-line, process-listing
