# CredentialStore static cache is mutated without locking across GUI/CLI threads

**Category:** startup-cli
**Severity:** Medium
**Type:** Concurrency
**File(s):** `vHC/HC_Reporting/Startup/CredentialStore.cs:24,103-248`

## Summary
`CredentialStore` is a static class whose `_cache` dictionary and backing `creds.json` file are mutated by `Set`, `SetTransient`, `Clear`, and `Remove` with no synchronization. The GUI calls `CredentialStore.Get/Remove/GetAllServers` on the UI thread and on background `Task.Run` monitor-setup threads (VhcGui.xaml.cs:673, 700), while collection code may call `Set` concurrently. Reassigning `_cache = new Dictionary(...)` in `Clear()`/`InitializeCache()` while another thread enumerates it (`GetAllServers().ToList()`, `PersistCacheToDisk()'s ToDictionary`) is a race.

## Evidence
```csharp
private static Dictionary<string, (string Username, byte[] PasswordEnc)> _cache;   // no lock
...
public static void Clear() { _cache = new Dictionary<string, (string, byte[])>(); ... }   // replaces ref
public static List<string> GetAllServers() => _cache?.Keys.ToList() ?? new List<string>();  // enumerates
public static void Set(...) { SetCache(...); PersistCacheToDisk(); }   // mutates + writes file
```
Two threads writing `creds.json` via `File.WriteAllText` concurrently (Set on collection thread, Remove on UI thread) can also throw `IOException` or interleave a partial/corrupt file.

## Impact
Race conditions can throw `InvalidOperationException` (collection modified during enumeration), corrupt or truncate `creds.json`, or lose a just-stored credential. Because the store backs unattended auth, a corrupted file degrades to "no creds" on next run, breaking silent execution. Severity is Medium because concurrent credential mutation is not the common path, but the GUI's `Task.Run` monitor flows make it reachable.

## Suggested Fix
Add a `private static readonly object _gate = new();` and lock every read/modify/persist of `_cache` and the file write. Use an atomic write (write temp + `File.Replace`/`Move`) for `creds.json` to avoid partial files.

## Labels
concurrency, thread-safety, credentials, file-corruption, medium
