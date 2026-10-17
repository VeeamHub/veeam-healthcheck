# Unknown / malformed CLI arguments are silently ignored — no error, no usage hint

**Category:** startup-cli
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CArgsParser.cs:108-275`

## Summary
The `ParseAllArgs` `switch` has no `default` case. Any argument that does not match a known flag or regex is silently dropped. A typo like `/scrub:ture`, `/day:30`, `/oudir=...`, or `/run-now` is ignored with zero feedback, and the tool proceeds with default behavior (often: no `run` flag set, so it does nothing and returns 0).

## Evidence
The `foreach`/`switch` block ends at line 274 with no `default:` arm. For example `/days:15` (not one of the hardcoded 7/12/30/90 cases) silently leaves `ReportDays` at its default of 7 with no warning; `/scrub:tru` leaves scrub at its default.

## Impact
Operators get no signal that an argument was misspelled or unsupported. In unattended mode a typo'd `/silent` or `/outdir=` flag is dropped and the run behaves unexpectedly (e.g. prompts interactively, or writes to the default `C:\temp\vHC`) while still exiting 0. This is a usability and silent-misconfiguration hazard, especially given the documented fleet/Task-Scheduler use case.

## Suggested Fix
Add a `default:` arm that logs a warning (and in `/silent` mode, fails fast) for any token starting with `/` that matched no case:
```csharp
default:
    if (a.StartsWith("/"))
    {
        CGlobals.Logger.Warning($"Unrecognized argument ignored: {a}", false);
        if (CGlobals.Silent) SilentExit.ExitSilent(SilentExit.GenericFailure, $"Unknown arg: {a}");
    }
    break;
```
Also parse `/days:<N>` generically (`int.TryParse`) instead of four hardcoded literals so valid values like 15/60 are honored or explicitly rejected.

## Labels
bug, argument-parsing, usability, silent-misconfig, low
