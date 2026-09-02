---
title: "CVmcReader.GetLogDir: discarded OrderBy, NullReferenceException on missing VMC.log, and unprotected call path"
severity: Medium
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:61
  - vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:29
confidence: High
---

## Summary

Three defects in `CVmcReader`:

1. `fileInfoList.OrderBy(x => x.Name);` discards its result — LINQ `OrderBy` is not in-place, so the intended "pick the right VMC.log" ordering is a no-op and `FirstOrDefault()` returns filesystem enumeration order.
2. `fileInfoList.FirstOrDefault().Name` throws `NullReferenceException` when no `VMC.log` file exists in `C:\ProgramData\Veeam\Backup365\Logs\`. `Directory.GetFiles(this.vb365Logs)` also throws `DirectoryNotFoundException` if the folder is absent.
3. `PopulateVmc` only wraps `ReadVmc()` in try/catch — `GetLogDir()` is called *outside* the try, so the exceptions above propagate out of `CLogOptions`'s constructor into `CCollections.ExecVmcReader`, which has no handler either (CCollections.cs:126-137).

Also minor: `Path.Combine(this.vb365Logs + fileName)` and `Path.Combine(regDir + CLogOptions.VMCLOG)` concatenate inside a single-argument `Path.Combine` — the Combine is a no-op decoration.

## Impact

On VB365 systems with an empty/rotated/missing Logs directory, collection crashes with an unhandled NRE (or DirectoryNotFoundException) rather than skipping InstallationId extraction. Where multiple VMC logs exist, an arbitrary one is read instead of the intended one.

## Evidence

`vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:61-63` —

```csharp
fileInfoList.OrderBy(x => x.Name);                       // result discarded
string fileName = fileInfoList.FirstOrDefault().Name;    // NRE when list is empty
this.LOGLOCATION = Path.Combine(this.vb365Logs + fileName);
```

`vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:27-38` —

```csharp
public void PopulateVmc()
{
    this.GetLogDir();          // outside the try — exceptions escape
    try
    {
        this.ReadVmc();
    }
    catch (Exception e) { CGlobals.Logger.Error(e.Message); }
}
```

## Suggested fix

`fileInfoList = fileInfoList.OrderBy(x => x.Name).ToList();` (or `OrderByDescending` by LastWriteTime if newest is wanted); guard `Directory.Exists` and empty list (log + return); move `GetLogDir()` inside the try block.
