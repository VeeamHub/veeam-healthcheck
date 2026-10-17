# Remote registry reads (GetRemoteVbrTwelveDbInfo / GetVbrElevenDbInfoRemote) lack null guards and admin-denied handling

**Category:** collection-data
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:189-196`, `vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:297-363`

## Summary
The remote registry paths call `RegistryKey.OpenRemoteBaseKey(...).OpenSubKey(...)` and then dereference the returned key and `.GetValue(...).ToString()` with no null checks, and — unlike their local counterparts — without the `SecurityException`/`UnauthorizedAccessException` handling. If the remote subkey is missing or access is denied, this throws `NullReferenceException` (on `.OpenSubKey` returning null, or `GetValue(...)` returning null) rather than the graceful "skip / running without admin" handling used locally.

## Evidence
```csharp
// CRegReader.cs:191-195 — no null check on OpenSubKey result, no security catch
using (RegistryKey key =
    RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, CGlobals.REMOTEHOST).OpenSubKey("Software\\Veeam\\Veeam Backup and Replication"))
{
    this.SetSqlInfo(key);
}
```
```csharp
// CRegReader.cs:305 and 316 — .ToString() on a possibly-null GetValue
var dbType = key.GetValue("SqlActiveConfiguration").ToString();
...
host = sqlKey.GetValue("SqlServerName").ToString();   // sqlKey may be null; value may be null
```
Compare the local `GetVbrTwelveDbInfo` (`CRegReader.cs:365-457`) which wraps the same logic in try/catch for `SecurityException`/`UnauthorizedAccessException` and checks `key != null`.

## Impact
Remote collection against a server where the Veeam keys are absent, the path is wrong, or the caller lacks remote-registry rights throws unhandled exceptions. `GetDbInfo`'s outer try/catch logs the v11 failure and proceeds to v12, but the v12 remote method (`GetRemoteVbrTwelveDbInfo`) has no catch at all, so an exception there propagates up to `CCollections.GetRegistryDbInfo` and can abort DB-info collection. The `sqlKey`/`pgKey` inner keys are also dereferenced without null checks (`CRegReader.cs:316`, `CRegReader.cs:336`).

## Impact-secondary
`OpenRemoteBaseKey` itself can throw `IOException` (RPC server unavailable) on an unreachable host — also unhandled here.

## Suggested Fix
Mirror the local methods: null-check every `OpenSubKey`/`GetValue` result, and wrap remote reads in try/catch for `SecurityException`, `UnauthorizedAccessException`, and `IOException`, logging a skip rather than throwing.

## Labels
bug, null-deref, registry, error-handling, remote, collection
