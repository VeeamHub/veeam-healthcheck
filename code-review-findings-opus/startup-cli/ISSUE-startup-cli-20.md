# CLogger constructor resolves output dir at first static use — early logs land in default path, never relocated

**Category:** startup-cli
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Common/Logging/CLogger.cs:12-34`, `vHC/HC_Reporting/Common/CGlobals.cs:16`, `vHC/HC_Reporting/Startup/CArgsParser.cs:639-644`

## Summary
`CGlobals.mainlog = new CLogger("HealthCheck")` is a static field initializer, so the log file path is computed (via `CVariables.unsafeDir`, which depends on `CGlobals.desiredPath`) the very first time `CGlobals` is touched — before any `/outdir=` argument is parsed. `ApplyOutDir` works around this by re-creating the logger (`CGlobals.mainlog = new CLogger("HealthCheck")`) after setting `desiredPath`, but every log line written before that point already went to a log file under the default `C:\temp\vHC`, and that first file is orphaned.

## Evidence
```csharp
// CGlobals static field initializer — runs at type init, before arg parsing
public static CLogger mainlog = new("HealthCheck");
```
```csharp
// CLogger ctor pins the path immediately from desiredPath-derived unsafeDir
string currentDir = CVariables.unsafeDir;        // = desiredPath ?? C:\temp\vHC
string logDir = Path.Combine(currentDir + "\\Log");
```
```csharp
// ApplyOutDir tries to fix it by replacing the logger after the fact
CGlobals.desiredPath = parsedOutDir;
CGlobals.mainlog = new CLogger("HealthCheck");   // new file in new dir; earlier file stranded
```

## Impact
- Log lines emitted during `Main` startup and early arg parsing (e.g. EntryPoint's "Starting the application", LogInitialInfo's version/args dump) are written to a log file under `C:\temp\vHC\Original\Log` even when the user specified `/outdir=`. Two log files per run result, and the one with the startup context is in the wrong (default) location — confusing for support diagnostics.
- Re-creating `mainlog` mid-run means any code that cached `CGlobals.Logger` / `mainlog` reference earlier keeps writing to the old file (several classes capture `private readonly CLogger LOG = CGlobals.Logger;` at construction — e.g. CClientFunctions, CHotfixDetector).

## Suggested Fix
Defer log-file creation: make `CLogger` lazily resolve `logFile` on first write (after args are parsed), or introduce an explicit `CGlobals.InitLogging()` called once after `/outdir=` is applied. Avoid swapping the static `mainlog` instance after other objects have captured it; instead have `CLogger` re-target its path.

## Labels
bug, logging, init-order, static-state, outdir, low
