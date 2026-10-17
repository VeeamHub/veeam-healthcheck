---
title: "CHotfixDetector.ClearTargetPath deletes the parent directory instead of each subdirectory; ExtractLogs deletes zips even when extraction fails"
severity: Medium
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CHotfixDetector.cs:229
  - vHC/HC_Reporting/Startup/CHotfixDetector.cs:206
confidence: High
---

## Summary
Inside `ClearTargetPath`, the loop over subdirectories calls `Directory.Delete(path, true)` — the *parent* path being cleared — instead of `Directory.Delete(dir, true)`. On the first iteration it recursively deletes the entire parent (including the remaining subdirectories still being iterated); subsequent iterations throw and are swallowed by the empty `catch`. Separately, `ExtractLogs` deletes each collected support-log zip with `File.Delete(file)` even when `ExtractToDirectory` threw (the catch is empty), so failed extractions destroy the only copy of the collected logs.

## Impact
Hotfix detection (`/hotfix`) can silently throw away collected Veeam support logs: a zip whose extraction fails (e.g., name collision, since `ExtractToDirectory` is called repeatedly into the same `extracted` target without overwrite) is deleted anyway, and the log content is never parsed. The wrong-variable delete currently "works by accident" only because the parent is the directory the caller wanted cleared — but it deletes mid-iteration with all errors suppressed, which is fragile and hides real IO failures.

## Evidence
`vHC/HC_Reporting/Startup/CHotfixDetector.cs:212-234`:

```csharp
string[] dirs = Directory.GetDirectories(path);
foreach (string dir in dirs)
{
    this.ClearTargetPath(dir);
    try
    {
        Directory.Delete(path, true);   // <-- deletes PARENT, not 'dir'
    }
    catch (Exception) { }
}
```

`vHC/HC_Reporting/Startup/CHotfixDetector.cs:195-207`:

```csharp
using (ZipArchive zip = ZipFile.OpenRead(file))
{
    try
    {
        zip.ExtractToDirectory(target);
    }
    catch (Exception) { }    // extraction failure swallowed
}

File.Delete(file);            // zip deleted regardless
```

## Suggested fix
- `Directory.Delete(dir, true)` in the loop (or simply `Directory.Delete(path, true)` once, outside any loop, since the intent is to clear `path`).
- Only delete the zip when extraction succeeded; log extraction failures: `zip.ExtractToDirectory(target, overwriteFiles: true)` plus `catch (Exception ex) { LOG.Error(...); continue; }`.
