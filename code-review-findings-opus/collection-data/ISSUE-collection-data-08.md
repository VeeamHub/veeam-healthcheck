# CVmcReader: NullReferenceException when no VMC.log exists; LOGLOCATION can be null

**Category:** collection-data
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:48-64`, `vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:67-69`

## Summary
In the `vb365` branch of `GetLogDir`, the code filters log files for `VMC.log` and then calls `fileInfoList.FirstOrDefault().Name`. If no matching file exists, `FirstOrDefault()` returns `null` and `.Name` throws a `NullReferenceException`. Also, `OrderBy` is called but its result is discarded (`fileInfoList.OrderBy(...)` does not sort in place), so the "first" file is order-undefined. If `GetLogDir` throws or leaves `LOGLOCATION` null, `ReadVmc` constructs a `StreamReader(null)` and throws.

## Evidence
```csharp
// CVmcReader.cs:61-63
fileInfoList.OrderBy(x => x.Name);                       // result discarded — no effect
string fileName = fileInfoList.FirstOrDefault().Name;    // NRE if list empty
this.LOGLOCATION = Path.Combine(this.vb365Logs + fileName);
```
`Directory.GetFiles(this.vb365Logs)` (`CVmcReader.cs:50`) will also throw `DirectoryNotFoundException` if `C:\ProgramData\Veeam\Backup365\Logs\` is absent. `ReadVmc` then does `new StreamReader(this.LOGLOCATION)` (`CVmcReader.cs:69`) with no null/existence guard.

## Impact
On a VB365 host with no `*VMC.log*` yet (fresh install, or logs rotated away), the install-ID/SQL-version collection throws NRE. The outer `try/catch` in `PopulateVmc` swallows the message to the log, so collection silently loses `INSTALLID` and config-DB version with only a terse logged exception — hard to diagnose. The discarded `OrderBy` also means "latest log" selection is wrong.

## Suggested Fix
Guard for empty results and missing directory; assign the sorted result. Example:
```csharp
if (!Directory.Exists(this.vb365Logs)) { /* log + return */ }
var file = fileInfoList.OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
if (file == null) { /* log "no VMC.log found" + return */ }
this.LOGLOCATION = file.FullName;
```
And in `ReadVmc`, return early if `LOGLOCATION` is null or the file does not exist.

## Labels
bug, null-deref, linq-misuse, collection
