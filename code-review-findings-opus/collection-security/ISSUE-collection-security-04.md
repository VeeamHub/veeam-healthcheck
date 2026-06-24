# Plaintext password round-trips to managed String and is never zeroed (defeats SecureString/DPAPI)

**Category:** collection-security
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Startup/CredentialStore.cs:140-142`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:543`, `vHC/HC_Reporting/Functions/Collection/Security/CredentialHelper.cs:93-105`

## Summary
The credential subsystem advertises SecureString support (`CredentialHelper.ConvertToSecureString`) and DPAPI encryption-at-rest, but in practice the password is decrypted into an immutable managed `System.String`, passed around, Base64-encoded, embedded into command-line argument strings, and never cleared. Managed strings are immutable and GC-managed, so the plaintext lingers in the heap (and in any process memory dump / page file) for an unbounded time. `ConvertToSecureString` is essentially dead code on the collection path.

## Evidence
Decryption returns a plain `string` and tuples it up:
```csharp
var password = Encoding.UTF8.GetString(
    ProtectedData.Unprotect(val.PasswordEnc, null, DataProtectionScope.CurrentUser));   // line 140-141
return (val.Username, password);                                                         // line 142
```
Then it is materialized into byte arrays / Base64 strings repeatedly with no cleanup:
```csharp
byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(creds.Value.Password);  // PSInvoker line 543, 709, 735
string passwordBase64 = Convert.ToBase64String(passwordBytes);                    // never zeroed
```
`ConvertToSecureString` exists (CredentialHelper.cs:93-105) but no collection-path code calls it — `grep` shows it is referenced only by `CredentialHelperTests`. The plaintext also passes through `CredsHandler.PromptForCredentialsCli`/`PasswordBox.Password` as `string`.

## Impact
Sensitive passwords persist in managed heap memory well beyond their useful lifetime, exposed to memory-scraping malware, crash dumps, and the page file. The SecureString/DPAPI machinery gives a false impression of in-memory protection that the actual data flow does not provide.

## Suggested Fix
- Where feasible, keep secrets in `SecureString` / `byte[]` and zero the `byte[]` (`Array.Clear`) immediately after Base64-encoding for the PS arg.
- Pin and zero transient `byte[] passwordBytes` after use in PSInvoker (lines 543/709/735).
- Either wire `ConvertToSecureString` into the real flow or remove it so it does not imply protection that isn't applied.
- Accept that .NET managed strings cannot be reliably zeroed; minimize the number of string copies of the password.

## Labels
security, securestring, memory-hygiene, defense-in-depth
