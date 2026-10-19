# CHotfixDetector.ClearTargetPath deletes the entire parent dir inside the per-subdir loop

**Category:** startup-cli
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CHotfixDetector.cs:212-234`

## Summary
`ClearTargetPath(path)` is meant to clear a directory's contents. After deleting files, it loops over subdirectories and for each one calls `Directory.Delete(path, true)` — passing the **outer `path`**, not the subdirectory `dir`. So inside a loop iterating subdirectories it repeatedly tries to recursively delete the parent directory it is currently enumerating. The recursion on `dir` plus the parent-delete is contradictory.

## Evidence
```csharp
string[] dirs = Directory.GetDirectories(path);
foreach (string dir in dirs)
{
    this.ClearTargetPath(dir);          // recurse into child (clears child contents)
    try
    {
        Directory.Delete(path, true);   // BUG: deletes `path` (the parent), not `dir`
    }
    catch (Exception) { }
}
```

## Impact
The first subdirectory iteration recursively deletes the entire `path` tree (because `Directory.Delete(path, true)` is recursive), which then invalidates the `dirs` array being enumerated; subsequent iterations throw `DirectoryNotFoundException` which is swallowed by the empty catch. The net effect is "delete everything under path including path itself" rather than "clear path's contents," and it works only by accident via the swallowed exceptions. If `Directory.Delete(path, true)` ever fails partway (file lock), the empty catch hides it and extraction proceeds against a partially-cleared directory, risking stale log files contaminating hotfix results.

## Suggested Fix
Delete the subdirectory, not the parent, and don't swallow blindly:
```csharp
foreach (string dir in dirs)
{
    try { Directory.Delete(dir, true); }
    catch (Exception ex) { this.LOG.Warning(this.logStart + $"Failed to clear {dir}: {ex.Message}", false); }
}
```
(The recursive `ClearTargetPath(dir)` call becomes unnecessary once `Directory.Delete(dir, true)` is used.)

## Labels
bug, filesystem, hotfix, exception-swallowing, medium
