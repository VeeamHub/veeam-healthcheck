# Password Base64 is not encryption; plaintext password recoverable from process command line and logs

**Category:** collection-data
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:543-547`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:707-713`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:901-903`, `vHC/HC_Reporting/Functions/Collection/CCollections.cs:309-317`

## Summary
For remote execution the user's password is UTF-8 Base64-encoded and passed as a `-PasswordBase64` command-line argument to powershell.exe/pwsh.exe. Comments throughout describe this as "secure transmission" / "encode password for safe transmission", but Base64 is trivially reversible — it is encoding, not encryption. The full plaintext-equivalent password is therefore present on the process command line for the lifetime of the child PowerShell process.

## Evidence
```csharp
// PSInvoker.cs:543-546
byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(creds.Value.Password);
string passwordBase64 = Convert.ToBase64String(passwordBytes);
...
argString += $"-User \"{escapedUser}\" -PasswordBase64 \"{passwordBase64}\" ";
```
Same pattern at `PSInvoker.cs:709-712`, `PSInvoker.cs:735-738`, `BuildVb365Arguments` (`PSInvoker.cs:901-903`), and `CCollections.MfaTestPassed` (`CCollections.cs:309-317`). The remote VB365 MFA path even reconstructs and uses it inline (`PSInvoker.cs:362-365`).

## Impact
Any local user / process that can enumerate process command lines (Task Manager details column, `Get-CimInstance Win32_Process`, ETW, EDR telemetry, crash dumps) can read the Base64 string and instantly decode the credential — which is frequently a VBR/domain service account with broad privileges. The "safe transmission" comments give a false sense of security. (The separate `safeArgString` masking only protects the application's own log file, not the OS-level command line.)

## Suggested Fix
Do not pass secrets on the command line. Options: pipe the Base64/secret to the child process via stdin and read it inside the script; use a Windows DPAPI-protected blob written to an ACL'd temp file that the script reads then deletes; or marshal a `PSCredential` in-process via the `System.Management.Automation` runspace (already used elsewhere) instead of spawning powershell.exe. At minimum, stop labeling Base64 as secure.

## Labels
security, credential-exposure, process-command-line, collection
