# ExtractLogs swallows zip-extraction failures then deletes the source archive — data loss

**Category:** startup-cli
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CHotfixDetector.cs:190-210`

## Summary
`ExtractLogs()` opens each support-log zip, attempts `zip.ExtractToDirectory(target)` inside a `try { } catch (Exception) { }` that does nothing, and then **unconditionally** `File.Delete(file)` the source archive outside the catch. If extraction fails (corrupt zip, name collision — `ExtractToDirectory` throws if a file already exists, and `target` is shared across all archives), the failure is silently swallowed and the original archive is deleted anyway.

## Evidence
```csharp
foreach (string file in files)
{
    using (ZipArchive zip = ZipFile.OpenRead(file))
    {
        try { zip.ExtractToDirectory(target); }
        catch (Exception) { }            // swallowed
    }
    File.Delete(file);                   // runs even when extraction failed
}
```
Because `target` is the same directory for every archive and `ExtractToDirectory` does not overwrite, the **second** archive containing any same-named entry throws `IOException` — caught and ignored — and is then deleted, losing its contents entirely.

## Impact
Support logs that fail to extract (very likely once two archives share entry names, which Veeam support bundles routinely do) are silently lost: not extracted, and the source zip deleted. The hotfix scan then runs against incomplete data and may report "No hotfixes found" when fixes existed in the discarded archive — a false-negative on a tool whose entire purpose is detecting hotfixes before an upgrade.

## Suggested Fix
- Extract each archive into its own subdirectory (e.g. `Path.Combine(target, Path.GetFileNameWithoutExtension(file))`) to avoid collisions, or use the overload that overwrites.
- Only delete the source after confirmed successful extraction; on failure, log the error and keep the archive.
```csharp
try { zip.ExtractToDirectory(perArchiveTarget); File.Delete(file); }
catch (Exception ex) { this.LOG.Error(this.logStart + $"Extract failed for {file}: {ex.Message}", false); }
```

## Labels
bug, hotfix, data-loss, exception-swallowing, false-negative, medium
