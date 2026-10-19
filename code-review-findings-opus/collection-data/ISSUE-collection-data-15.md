# CLogParser constructor does heavy registry + filesystem work and can throw before object is usable

**Category:** collection-data
**Severity:** Low
**Type:** Maintainability
**File(s):** `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:22-43`, `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:67-81`

## Summary
The `CLogParser()` constructor performs side-effecting I/O: it reads the registry for the log directory (`InitLogDir`) and immediately truncates/creates `waits.csv` (`InitWaitCsv`). The CSV path is computed in a field initializer (`pathToCsv = CVariables.vbrDir + @"\waits.csv"`) that runs before the constructor body and assumes `CVariables.vbrDir` is non-null and the directory exists. If `vbrDir` is null/missing, `new StreamWriter(this.pathToCsv, ...)` throws inside the constructor.

## Evidence
```csharp
// CLogParser.cs:22
private readonly string pathToCsv = CVariables.vbrDir + @"\waits.csv";
...
// CLogParser.cs:29-32
public CLogParser()
{
    this.LogLocation = this.InitLogDir();   // registry read
    this.InitWaitCsv();                     // truncates waits.csv on disk
}
```
`InitWaitCsv` (`CLogParser.cs:72`) opens `this.pathToCsv` for write with `append:false`. There is no check that `CVariables.vbrDir` exists before this.

## Impact
Construction has observable side effects (file truncation) and can throw from a field initializer, which is surprising and makes the type hard to test (the test-only ctor `CLogParser(string)` at `CLogParser.cs:45` exists precisely to dodge this, but leaves `pathToCsv`/`LogLocation` in an inconsistent state). If `vbrDir` doesn't exist yet, the parser construction fails. The two-constructor split also means the parameterized ctor produces a half-initialized object (`LogLocation` null), which `GetWaitsFromFiles` would then `Directory.GetDirectories(null)` on.

## Suggested Fix
Move I/O out of the constructor into an explicit `Initialize()`/method call; verify `CVariables.vbrDir` exists (create it) before computing/writing `waits.csv`; remove or fully initialize the secondary constructor.

## Labels
maintainability, constructor-side-effects, testability, collection
