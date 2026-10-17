# Remote registry reads dereference null RegistryKey, leaking host/connectivity errors and crashing collection

**Category:** collection-security
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/Security/CSecurityInit.cs:145-215`

## Summary
`GetInstalledApps` and `IsRdpEnabled` open a remote registry hive against the operator-supplied `VBRSERVER` (`CGlobals.REMOTEHOST`) and immediately call `.OpenSubKey(...)` / `.GetValue(...).ToString()` on the result without null-checking. If the remote host is unreachable, access is denied, or the key/value is absent, this throws (NullReferenceException / IOException / SecurityException) and the exception message — which can include the remote host name and Win32 error detail — propagates up uncaught.

## Evidence
```csharp
using (RegistryKey key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, this.VBRSERVER).OpenSubKey(registry_key))
{                                                            // line 149 — OpenSubKey may return null
    ...
    foreach (string subkey_name in key.GetSubKeyNames())     // line 152 — NRE if key == null
```
```csharp
using (RegistryKey key = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, this.VBRSERVER).OpenSubKey(registryKey))
{
    ...
    var v = key.GetValue(keyName).ToString();                // line 197 — NRE if key or value null
```
Neither method wraps the remote-open in try/catch (the inner per-subkey loop in `GetInstalledApps` does swallow exceptions at line 180, but the outer open does not).

## Impact
A wrong/unreachable `REMOTEHOST` crashes the security-collection phase rather than degrading gracefully to "Undetermined" (the value `CSecurityGlobalValues` is designed to hold). Uncaught exception messages may surface the remote host name and Win32 error codes into logs/UI. Low security impact, real reliability/robustness impact for the security data the report depends on.

## Suggested Fix
- Null-check the result of `OpenRemoteBaseKey(...).OpenSubKey(...)` and the result of `GetValue(...)` before dereferencing; set the corresponding `CSecurityGlobalValues.*` to `"Undetermined"` on failure.
- Wrap each remote-registry open in try/catch and log a sanitized message (no raw exception text) so a bad host does not abort collection.

## Labels
bug, null-dereference, robustness, remote-registry
