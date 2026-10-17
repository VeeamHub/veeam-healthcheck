---
title: "Dead/unreachable code cluster in startup path (ModeCheck, VbrVersionSupportCheck, duplicate dispatch branches, redundant null check)"
severity: Low
labels: [maintainability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CClientFunctions.cs:174
  - vHC/HC_Reporting/Startup/CClientFunctions.cs:108
  - vHC/HC_Reporting/Startup/CArgsParser.cs:52
  - vHC/HC_Reporting/Startup/CArgsParser.cs:361
  - vHC/HC_Reporting/Startup/EntryPoint.cs:11
confidence: High
---

## Summary
Several pieces of dead, unreachable, or no-op code in the startup path that obscure the real control flow and will mislead future maintainers.

## Impact
No direct user-facing breakage, but the dispatch logic in `ParseAllArgs` and `ModeCheck` is already subtle (see ISSUE-02/03); dead branches make every future change riskier.

## Evidence
1. `CClientFunctions.cs:174-178` — unreachable: `ModeCheck` already returned `"fail"` at line 160-165 when neither product is detected, so this second `if (!CGlobals.IsVb365 && !CGlobals.IsVbr)` ("Import mode only" title) can never execute.

2. `CClientFunctions.cs:108-133` — `VbrVersionSupportCheck()` is never called anywhere (its `GetVbrVersion()` call is commented out and no caller exists). If ever revived, `vhcVersionSections[3]` will throw `IndexOutOfRangeException` for versions with fewer than four dot-segments, and the two `int.TryParse` results are used unchecked.

3. `CArgsParser.cs:48-55` — `this.args != null` is checked *after* `this.args.Length` was already dereferenced at line 48; the `else` branch (`LaunchUi`) is unreachable.

4. `CArgsParser.cs:361-370` — the `REMOTEHOST != string.Empty && CGlobals.RunSecReport` branch and the following `REMOTEHOST != string.Empty` branch have identical bodies; the first is redundant.

5. `EntryPoint.cs:11` — `private static readonly CClientFunctions functions = new();` is never used in `EntryPoint`.

6. `CClientFunctions.cs:446-453` — empty `try { /* REST TEST AREA */ } catch (Exception) { }` in `CliRun`.

7. `CClientFunctions.cs:34` — `WebBrowser w1 = new();` in `KbLinkAction` is created and never used (also instantiates a heavyweight WPF control on every KB click).

8. `VhcGui.xaml.cs:248` — `Run(bool import)`'s `import` parameter is unused.

9. `CHotfixDetector.cs:37` — `CCollections col = new();` in `Run()` is created and never used.

## Suggested fix
Delete each listed item (or, for #2, fix the bounds checking if the version gate is still wanted; for #4, collapse the duplicate branches into one).
