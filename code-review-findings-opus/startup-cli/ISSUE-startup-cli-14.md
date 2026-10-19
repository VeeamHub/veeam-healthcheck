# CGlobals static state persists across runs/tests — no reset, hidden init-order coupling

**Category:** startup-cli
**Severity:** Medium
**Type:** Concurrency
**File(s):** `vHC/HC_Reporting/Common/CGlobals.cs:13-226`, `vHC/HC_Reporting/Common/CCsvsInMemory.cs:13`

## Summary
`CGlobals` holds the entire execution state as static mutable fields (flags, paths, DB info, `DtParser`, `FullReportJson`, `CsvValidationResults`, `_runTimestamp`, etc.) with no reset/Init method. There is no mechanism to clear this between runs within a single process (the GUI keeps the process alive across an Import then a Run, and tests share the AppDomain). Combined with `CCsvsInMemory.csvData` (a process-wide static cache keyed by file path) and `CScrubHandler`'s static singleton, stale state from a prior run/test leaks into the next.

## Evidence
- `_runTimestamp` (CGlobals.cs:47, 218-225) is memoized once and never reset — a second run in the same process reuses the first run's timestamp, so output lands in the prior run's timestamped directory (`GetVbrDirWithTimestamp` uses it).
- `CGlobals.IsVbr/IsVb365` are set `true` by `ModeCheck`/`ResolveImportPath` but never set back to `false`; an Import that detects VB365 leaves `IsVb365=true` for any subsequent action in the same session.
- `CCsvsInMemory.GetCsvData` returns cached rows for a path forever; if a path is re-collected with new data under the same name, the stale cache wins (no invalidation).

## Impact
- GUI users who run Import then a fresh collection (or vice versa) in one session can get output written to the wrong timestamped folder and product flags that reflect the previous operation.
- xUnit tests in `VhcXTests` that touch `CGlobals`/`CCsvsInMemory`/`CredentialStore` pollute each other's state (order-dependent test failures), since none of these expose a reset and statics live for the AppDomain.

## Suggested Fix
Add a `CGlobals.ResetForNewRun()` that clears per-run fields (`_runTimestamp`, product flags, validation results, DtParser, FullReportJson) and call it at the start of each CLI/GUI run; have it also call `CCsvsInMemory.Clear()`. Provide test fixtures that reset these statics in `IDisposable.Dispose`/`IAsyncLifetime`.

## Labels
concurrency, static-state, test-pollution, stale-state, medium
