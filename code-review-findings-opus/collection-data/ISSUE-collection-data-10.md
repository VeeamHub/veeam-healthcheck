# CRegReader uses static mutable fields for instance DB state — cross-instance/thread bleed

**Category:** collection-data
**Severity:** Medium
**Type:** Concurrency
**File(s):** `vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:17-21`, `vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:232-285`

## Summary
`CRegReader` stores its discovered DB connection details (`databaseName`, `hostInstanceString`, `host`, `user`, `passString`) in `private static` fields, yet exposes them through *instance* properties (`DbString`, `HostString`) and is constructed fresh in multiple places (`CDbAccessor.SimpleConnectionBuilder`, `CCollections.GetRegistryDbInfo`, `CLogParser.InitLogDir`, `CVmcReader.GetLogDir`). Because the state is static, every `CRegReader` instance shares (and overwrites) the same fields.

## Evidence
```csharp
// CRegReader.cs:17-21
private static string databaseName;
private static string hostInstanceString;
private static string host;
private static string user;
private static string passString;
```
```csharp
// CRegReader.cs:27-35 — instance properties returning static state
public string DbString  { get { return databaseName; } }
public string HostString{ get { return hostInstanceString; } }
```
`SetSqlInfo` (`CRegReader.cs:232`) writes these statics; multiple distinct instances are created across the collection pipeline.

## Impact
Two issues: (1) State from one instance silently leaks into another — a later `new CRegReader()` that fails to read the registry still returns the previous instance's host/db, masking failures and potentially pointing queries at a stale server. (2) If any collection step runs concurrently (the codebase already uses `Parallel.ForEach` in `CLogParser`, and `CRegReader` is used inside `CLogParser.InitLogDir`), these unsynchronized static writes are a data race. The leftover `host`/`user`/`passString` statics also keep credential-adjacent data resident in a static for the process lifetime.

## Suggested Fix
Make the fields instance-level (`private string ...`) so each `CRegReader` owns its own state, or, if a single cached lookup is genuinely desired, encapsulate it behind one explicitly-synchronized singleton with a clear lifecycle. Remove the unused `host`/`user`/`passString` statics if they are not consumed.

## Labels
concurrency, static-state, correctness, collection
