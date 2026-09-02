# Import-mode product detection only sets flags true, never resets — combined-product imports mis-routed

**Category:** startup-cli
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CClientFunctions.cs:417-425`, `vHC/HC_Reporting/Startup/CReportModeSelector.cs:38-49`

## Summary
`ResolveImportPath()` sets `CGlobals.IsVbr = true` or `CGlobals.IsVb365 = true` based on a single `validationResult.ProductType` string ("VBR" or "VB365"), which is mutually exclusive — there is no "Both" branch. Then `CReportModeSelector.FileChecker()` decides which reports to generate by checking `Directory.Exists(CVariables.vb365dir)` and `Directory.Exists(CVariables.vbrDir)`. During import, **both** of those properties return the same `ResolvedImportPath` (see CVariables.cs:38-48 and 59-69), so `Directory.Exists` is true for both regardless of which product the data actually is, and both `StartM365Report()` and `StartVbrReport()` may run against the same directory.

## Evidence
```csharp
// CClientFunctions.ResolveImportPath
if (validationResult.ProductType == "VBR") CGlobals.IsVbr = true;
else if (validationResult.ProductType == "VB365") CGlobals.IsVb365 = true;   // no "Both"
```
```csharp
// CVariables — during import both dirs resolve to the same path
public static string vb365dir { get { if (CGlobals.IMPORT && !string.IsNullOrEmpty(ResolvedImportPath)) return ResolvedImportPath; ... } }
public static string vbrDir   { get { if (CGlobals.IMPORT && !string.IsNullOrEmpty(ResolvedImportPath)) return ResolvedImportPath; ... } }
```
```csharp
// CReportModeSelector — both Directory.Exists checks pass for the same import path
if (Directory.Exists(CVariables.vb365dir) && CGlobals.RunFullReport) this.StartM365Report();
if (Directory.Exists(CVariables.vbrDir)   && CGlobals.RunFullReport) res = this.StartVbrReport();
```

## Impact
On import, the report selector keys off directory existence rather than the detected product type, so a VBR-only import can still invoke the VB365 compiler path (and vice versa) because both `vb365dir` and `vbrDir` point at the identical resolved folder. Coupled with the VB365 compiler being a no-op today (ISSUE-01), the practical fallout is limited, but the routing logic is incorrect and will misbehave once VB365 generation is fixed. The detected `ProductType`/`IsVbr`/`IsVb365` flags are computed but not used to gate report selection in import mode.

## Suggested Fix
Gate report generation on the detected product flags rather than directory existence in import mode:
```csharp
if (CGlobals.EffectiveIsVb365 && CGlobals.RunFullReport) StartM365Report();
if (CGlobals.EffectiveIsVbr   && CGlobals.RunFullReport) res = StartVbrReport();
```
and have `CCsvValidator`/`CImportPathResolver` report a "Both" product type when an import folder contains both VBR and VB365 CSVs.

## Labels
bug, import, report-routing, product-detection, low
