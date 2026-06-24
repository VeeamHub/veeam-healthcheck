# NullReferenceException dereferencing creds.Value in TestMfa when credentials are absent

**Category:** collection-security
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:256-261`

## Summary
`TestMfa()` calls `CredsHandler.GetCreds()`, which is documented to return `null` (no stored credential, no `/credfile=` entry, or silent mode). Unlike every other call site in this file, `TestMfa` does **not** null-check the result before dereferencing `creds.Value.Password` / `creds.Value.Username`, producing a `NullReferenceException`.

## Evidence
```csharp
CredsHandler ch = new();
var creds = ch.GetCreds();                                                    // line 256 — can return null

// Properly escape the password, username and server ...
string escapedPassword = CredentialHelper.EscapeForPowerShellSingleQuotes(creds.Value.Password);  // line 260 — NRE if null
string escapedUser = CredentialHelper.EscapeForPowerShellSingleQuotes(creds.Value.Username);      // line 261
```

Compare with the guarded call sites that handle the same return value correctly:
- `TestMfaVB365` line 349-355: `if (creds == null) { ... return true; }`
- `VbrConfigStartInfo` line 540-541: `if (creds != null) { ... }`
- `ConfigStartInfo` line 705-706 / 731-732: `if (creds != null) { ... }`
- `BuildVb365Arguments` line 898-899: `if (creds != null) { ... }`

`GetCreds()` explicitly returns `null` in silent mode (CredsHandler.cs:45-51) and on cancelled prompts (CredsHandler.cs:56-60).

## Impact
In silent/unattended mode, or when a user cancels the credential prompt, `TestMfa` throws an unhandled `NullReferenceException` instead of returning a clean failure. The outer `catch (Exception ex)` (line 332) does swallow it, but it logs `ex.Message` (a bare "Object reference not set...") and returns `false`, masking the real "credentials missing" condition and diverging from the deliberate silent-mode exit-code contract that the other paths honor.

## Suggested Fix
Mirror the `TestMfaVB365` guard:
```csharp
var creds = ch.GetCreds();
if (creds == null)
{
    CGlobals.Logger.Error("Credentials required for MFA test.");
    return true; // true = MFA failure, stops collection (matches TestMfaVB365 semantics)
}
```

## Labels
bug, null-dereference, silent-mode, error-handling
