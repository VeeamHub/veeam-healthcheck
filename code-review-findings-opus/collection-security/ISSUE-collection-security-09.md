# Credential store TOCTOU / lost-update and silent swallow on persist failure

**Category:** collection-security
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CredentialStore.cs:147-189`, `vHC/HC_Reporting/Startup/CredentialStore.cs:205-248`

## Summary
`CredentialStore` is a process-wide static cache with no synchronization and non-atomic disk writes. `Set` mutates the shared `_cache` dictionary and then rewrites the entire `creds.json` with a plain `File.WriteAllText`. Concurrent callers (the credential prompt can be marshalled across threads via the WPF dispatcher in `CredsHandler.PromptForCredentialsGui`) can race on `_cache` and on the file, and a crash mid-write leaves a truncated/corrupt store. `PersistCacheToDisk` has no try/catch, so an IO failure there throws to the caller, whereas `Remove`/`Clear` swallow IO errors entirely.

## Evidence
```csharp
public static void Set(string server, string username, string password)
{
    SetCache(server, username, password);   // mutates shared Dictionary, no lock
    PersistCacheToDisk();                    // full-file rewrite, not atomic
}
...
private static void PersistCacheToDisk()
{
    var serializable = _cache.ToDictionary(...);                  // enumerates shared dict, no lock
    File.WriteAllText(StorePath, JsonSerializer.Serialize(...));  // line 189 — no temp-file+rename, no try/catch
}
```
`Remove` (line 205-248) and `Clear` (line 103-123) catch and log-and-continue on failure, so a failed delete/rewrite there reports success-ish behavior. `Dictionary<TKey,TValue>` is not thread-safe; concurrent `Set`/`Remove`/enumeration is undefined behavior.

## Impact
- Corrupt `creds.json` on crash or concurrent write (InitializeCache does tolerate malformed JSON by resetting, but a partial write could also silently drop other hosts' stored credentials — a lost-update).
- Inconsistent error handling: `Set` can throw an IO exception to its caller (mid-collection), while `Remove`/`Clear` silently swallow.
- Potential `InvalidOperationException`/torn reads from unsynchronized `Dictionary` access under the GUI dispatcher marshal path.

## Suggested Fix
- Guard all `_cache` access and all file writes with a single `static readonly object _lock` (or use `ConcurrentDictionary` plus a write lock).
- Write atomically: serialize to a temp file in the same directory and `File.Replace`/`File.Move(overwrite)` into place.
- Make error handling consistent — either all persist paths surface failures or all log-and-continue, but document which.

## Labels
bug, concurrency, toctou, atomicity, error-handling
