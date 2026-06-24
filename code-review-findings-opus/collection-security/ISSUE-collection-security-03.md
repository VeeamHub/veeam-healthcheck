# DPAPI-encrypted credential store written with default ACLs and no file-permission hardening

**Category:** collection-security
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Startup/CredentialStore.cs:20-22`, `vHC/HC_Reporting/Startup/CredentialStore.cs:179-189`

## Summary
`creds.json` stores per-host passwords encrypted with DPAPI (`DataProtectionScope.CurrentUser`), which is correct for encryption-at-rest. However the file is written via `File.WriteAllText` with default inherited ACLs and no explicit access-control hardening. The directory/file inherit whatever the parent `%APPDATA%\VeeamHealthCheck` ACL grants. On multi-admin or roaming-profile machines this widens who can read the blob, and the username is stored in cleartext.

## Evidence
```csharp
private static readonly string StorePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "VeeamHealthCheck", "creds.json");                                   // line 20-22
...
private static void PersistCacheToDisk()
{
    var serializable = _cache.ToDictionary(...);
    File.WriteAllText(StorePath, JsonSerializer.Serialize(serializable, ...));   // line 189 — no ACL set
}
```
`Directory.CreateDirectory(Path.GetDirectoryName(StorePath))` (line 35) likewise relies on inherited ACLs.

DPAPI CurrentUser scope means another user cannot *decrypt* the blob, but the **username is plaintext** (intentionally — see test `StoredCredentials_ShouldBeEncrypted` asserting `Assert.Contains(username, fileContent)`), so credential *enumeration* (which accounts/hosts are targeted) leaks to anyone who can read the file. DPAPI also does not defend against the same-user threat model (any process running as that user can call `ProtectedData.Unprotect`).

## Impact
- Usernames + targeted hostnames disclosed to any principal with read access under default `%APPDATA%` inheritance.
- No defense-in-depth (e.g. additional entropy / a secondary secret passed to `ProtectedData.Protect`) — the `optionalEntropy` parameter is `null` at line 170-171 and 140-141, so any code running as the user trivially decrypts.

## Suggested Fix
- After creating the directory and before/after writing the file, set an explicit `FileSecurity`/`DirectorySecurity` ACL granting access only to the current user SID (and SYSTEM/Administrators per policy).
- Consider supplying non-null `optionalEntropy` to `ProtectedData.Protect`/`Unprotect` (a per-install random value stored separately, or an app-constant) to raise the bar for same-user attackers.
- Treat the stored username/host list as sensitive: document that the file reveals targets even though passwords are encrypted.

## Labels
security, encryption-at-rest, file-permissions, dpapi
