# Swallowed exceptions hide collection failures in log parsing and PATH scanning

**Category:** collection-data
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:163`, `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:257-258`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:91`

## Summary
Multiple empty catch blocks silently discard exceptions during collection, so per-file or per-record failures vanish with no log entry. This goes beyond the suppressed CA1031 guidance because the catches are not just catch-all — they are completely empty (no log, no counter), which actively hides partial-collection failures from the operator.

## Evidence
```csharp
// CLogParser.cs:163 — per-file parse failure silently dropped
catch (Exception) { }
```
```csharp
// CLogParser.cs:257-258 — inside CheckFileWait per-line loop
catch (System.ArgumentOutOfRangeException) { }
catch (Exception) { }
```
```csharp
// PSInvoker.cs:82-91 — FindExecutableInPath swallows all PATH errors
catch { }
```

## Impact
If a subset of `Job*.log`/`Task*.log` files are unreadable (locked, permissions, corrupt), the wait analysis silently omits them with zero indication in the log, so the report under-reports resource-wait contention and nobody knows data was lost. The empty `catch {}` in `FindExecutableInPath` can mask a malformed PATH entry that prevents PowerShell discovery. Even a `log.Debug` would make these diagnosable.

## Suggested Fix
Add at least a debug-level log (file name + exception message) in each empty catch. For the per-line loop, count and report the number of skipped lines/files at the end of parsing so partial failures are visible.

## Labels
bug, exception-swallowing, observability, collection
