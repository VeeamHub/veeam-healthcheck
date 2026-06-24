# Username not escaped/validated in TestMfaVB365 and credfile loader allows control chars beyond a denylist

**Category:** collection-security
**Severity:** Low
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:357-365`, `vHC/HC_Reporting/Startup/CArgsParser.cs:531-550`

## Summary
The username escaping/validation strategy is inconsistent and denylist-based. `TestMfaVB365` places the username in a single-quoted `PSCredential('{escapedUser}', ...)` argument using `EscapeForPowerShellSingleQuotes` (correct), but the `/credfile=` loader independently re-validates usernames with a hand-rolled character **denylist** rather than relying on the same escaping. Denylists are fragile: the credfile list omits characters that the escaping would otherwise have to handle, and the two mechanisms can drift.

## Evidence
Single-quote escape path (correct, PSInvoker.cs:359-365):
```csharp
string escapedUser = CredentialHelper.EscapeForPowerShellSingleQuotes(creds.Value.Username);
...
$"$cred = New-Object System.Management.Automation.PSCredential('{escapedUser}', $secpw); "
```
Denylist validation in the credfile loader (CArgsParser.cs:531-550):
```csharp
char[] forbiddenUsernameChars = new[] { '"', '\'', '`', '$', ';', '\n', '\r' };
...
if (kvp.Value.Username.IndexOfAny(forbiddenUsernameChars) >= 0) { ...reject... }
```
This denylist does not include other shell/PS-significant characters (e.g. `|`, `&`, `(`, `)`, `%`, `<`, `>`, NUL `\0`) that `ContainsProblematicCharacters` (CredentialHelper.cs:116) considers problematic, nor the `\0` null byte. Where the username is later used unquoted (the unescaped paths in ISSUE-01) those characters matter; where it is quoted+escaped the denylist is redundant. Either way the two layers are not coherent.

## Impact
Low in isolation (the primary VB365/VBR paths do escape correctly), but the divergence means a credfile username can carry characters (`|`, `&`, `(`, `)`, `%`, NUL) that are blocked nowhere and would be dangerous on any code path that forgets to quote/escape. Maintenance hazard that invites a future injection.

## Suggested Fix
- Standardize on a single allowlist for usernames (e.g. `^[A-Za-z0-9._\\@ -]{1,256}$` covering `DOMAIN\user` and UPN forms) applied at every boundary, instead of per-call denylists.
- Always pair the validation with the existing escaping rather than treating the denylist as the protection.

## Labels
security, validation, denylist, consistency
