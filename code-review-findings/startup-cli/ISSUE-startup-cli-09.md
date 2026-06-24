---
title: "Unknown CLI arguments are silently ignored (no default case); /days only accepts 4 hardcoded literals"
severity: Medium
labels: [bug, maintainability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:108
  - vHC/HC_Reporting/Startup/CArgsParser.cs:129
confidence: High
---

## Summary
The argument `switch` in `ParseAllArgs` has no `default` branch, so any unrecognized token — typos (`/sielnt`, `-run`, `/scrub`), unsupported values, or syntax variants — is dropped without a warning. `/days:` in particular is implemented as four exact-string cases (`/days:7`, `/days:30`, `/days:90`, `/days:12`), so `/days:14` or `/days:60` silently runs with the default 7-day interval. `/show:all` is an explicit no-op case.

## Impact
Real-world misuse produces a successful-looking run with wrong behavior: `/days:60` yields a 7-day report; `/scrub` (without `:true`) yields an unscrubbed report; a typo'd `/silnet` runs interactively in a scheduler context. For a tool whose primary consumers are field SEs driving it from documentation and scheduled tasks, silent acceptance of bad flags is a recurring support generator.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:108-274` — the `foreach`/`switch` ends at the `/monitor:disable` case with no `default:`.

`vHC/HC_Reporting/Startup/CArgsParser.cs:129-144`:

```csharp
case "/days:7": ... CGlobals.ReportDays = 7; break;
case "/days:30": ... CGlobals.ReportDays = 30; break;
case "/days:90": ... CGlobals.ReportDays = 90; break;
case "/days:12": ... CGlobals.ReportDays = 12; break;
```

(The help text says "Set reporting interval (7, 12, 30, or 90 days)" — but nothing tells the user other values were ignored at runtime.)

## Suggested fix
- Add a `default:` that logs a visible warning (or exits with usage in silent mode): `CGlobals.Logger.Warning($"Unrecognized argument: {a}. See /help.", false);`
- Parse `/days:` generically: match `^/days:(\d+)$`, `int.TryParse`, validate range, and warn/exit on invalid values instead of falling through.
