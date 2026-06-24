---
title: "Circular static initialization makes CGlobals.desiredPath default to C:\\temp\\vHC\\Original (not C:\\temp\\vHC)"
severity: Medium
labels: [bug, maintainability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Common/CGlobals.cs:73
  - vHC/HC_Reporting/Startup/CVariables.cs:15
confidence: High
---

## Summary
`CGlobals._desiredPath` is initialized from `CVariables.unsafeDir`, but `CVariables.unsafeDir` is itself computed from `CGlobals.desiredPath ?? outDir`. During `CGlobals` static initialization, `_desiredPath` is still null when `unsafeDir` is evaluated, so the default resolves to `Path.Combine("C:\temp\vHC", "Original")` — i.e., the default value of `desiredPath` becomes `C:\temp\vHC\Original` instead of the intended base `C:\temp\vHC`. From then on, any read of `CVariables.unsafeDir` yields `C:\temp\vHC\Original\Original` until something overwrites `desiredPath`.

## Impact
Currently masked on the main flows because both `CliRun` (`CGlobals.desiredPath = targetForOutput` with default `C:\temp\vHC`) and the GUI (`SetUiText` sets `CGlobals.desiredPath = CVariables.outDir`) overwrite the bad default before output paths are used. But any code path that reads `desiredPath`/`unsafeDir` before those points — e.g. the `/hotfix`, `/savecreds`, or `/monitor:*` flows, none of which call `CliRun` — sees the doubled `Original\Original` base. This is a classic landmine: the inline comment in `ApplyOutDir` ("apply immediately so CVariables.unsafeDir resolves correctly") shows the ordering sensitivity has already bitten once.

## Evidence
`vHC/HC_Reporting/Common/CGlobals.cs:73`:

```csharp
private static string _desiredPath = CVariables.unsafeDir;
```

`vHC/HC_Reporting/Startup/CVariables.cs:14-15`:

```csharp
public static string safeDir => Path.Combine(CGlobals.desiredPath ?? outDir, "Anonymous");
public static string unsafeDir => Path.Combine(CGlobals.desiredPath ?? outDir, "Original");
```

Initialization trace: first touch of `CGlobals` runs its field initializers in order; at line 73 it evaluates `CVariables.unsafeDir`, which re-enters `CGlobals.desiredPath` while `_desiredPath` is still null → returns `C:\temp\vHC\Original`. So `desiredPath` defaults to a path that already includes the `Original` segment, and `unsafeDir` then appends `Original` again. (`CLogger.CreateLogFile` also keys off `CVariables.unsafeDir`, so log location depends on this same ordering.)

## Suggested fix
Make the default unambiguous and one-directional — `desiredPath` should default to the base dir, not to a derived dir:

```csharp
private static string _desiredPath = null; // CVariables falls back to outDir via ?? already
// or explicitly:
private static string _desiredPath = @"C:\temp\vHC"; // = CVariables.outDir, but avoid the type cycle
```
