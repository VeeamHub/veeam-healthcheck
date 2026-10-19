# PowerShell 7 Missing/Broken Preflight (Issue #135) — Design

**Date:** 2026-08-28
**Status:** Proposed

## Problem

Issue #135 asked for a startup preflight that validates PowerShell 7 is
installed and warns clearly if it's missing or broken, instead of letting VHC
fail deep inside a collection script with a confusing PowerShell-level error.

Related work landed since (#186/#187, commits `b484462`/`7d75cea`/`594afea`)
added `CClientFunctions.ValidatePowerShellVersionMeetsVbrRequirement()`, which
compares the installed PS7 version against the minimum the local
`Veeam.Backup.PowerShell` module manifest requires, and hard-exits with an
actionable message if it's too old. That check is correctly scoped: it only
runs when `CGlobals.PowerShellVersion == 7`, which is only set when
`VBRMAJORVERSION >= 13` (`CClientFunctions.cs:557`) — so it never fires
redundantly for pre-v13 targets that only need PowerShell 5.1.

The remaining gap is inside that VBR-13+ path itself. When
`CPowerShellVersionChecker.TryGetInstalledPwshVersion` can't determine an
installed version, `ValidatePowerShellVersionMeetsVbrRequirement()` currently
treats that as one undifferentiated case: Debug-log and silently continue
(`CClientFunctions.cs:614-618`). That method returns `false` both when PS7 is
genuinely absent (`FindPwshExecutable()` found nothing) and when detection
itself was merely inconclusive (process timeout, unparsable output). The
first case is exactly what #135 reported and it's still unhandled: the run
proceeds and fails later with the confusing error #135 exists to prevent.

## Solution

Distinguish "PS7 not installed anywhere" from "PS7 present but version
undeterminable," and hard-fail only the former (reusing the existing
`PowerShellVersionUnsupported` exit path and exit code 8), while keeping the
latter as a soft skip — same conservative behavior as today, to avoid a false
positive block on a transient detection hiccup when PS7 may in fact be fine.

The manifest-required-version read can itself fail independently of pwsh's
presence (`VbrConsoleInstallDir` unknown, or the manifest unparsable), and
today that failure short-circuits the method before pwsh presence is even
checked. Left unchanged, "PS7 completely absent *and* the manifest can't be
read" would still silently pass through — the exact #135 failure, just from a
different trigger. `EvaluatePwshVersionStatus` (below) checks `pwshPath`
first and unconditionally, so `NotInstalled` is returned whether or not the
manifest read succeeded, closing that residual path without restructuring
the method's read order.

Broadening the hard-fail to cover "undeterminable" too (closer to #135's
literal title, which also says "broken") was considered and rejected: it
risks blocking valid runs on a one-off timeout or an unexpected output format,
a risk class the #186/#187 work was deliberately careful about elsewhere. A
fully standalone startup preflight (independent of the existing VBR-targeting
gate) was also considered and rejected: it would re-open the exact call-site
issues that took three follow-up commits (`594afea`, `c8bf129`, `15296c1`) to
fix — GUI hard-exit before the window renders, double pwsh spawn, incorrectly
gating `/hotfix` and `/import`. The existing choke point
(`RunVbrPreflightGateIfTargeted` → `ValidatePowerShellVersionMeetsVbrRequirement`)
already reaches the case that needs fixing.

## Architecture

```
CClientFunctions.ValidatePowerShellVersionMeetsVbrRequirement()  (modified)
    │
    ├─ pwshPath = CPowerShellVersionChecker.FindPwshExecutable()   (visibility: private → internal)
    ├─ requiredVersion = best-effort manifest read (unchanged shape: null if
    │      VbrConsoleInstallDir unknown or manifest unparsable - no longer an
    │      early return, just an input to the status function below)
    ├─ if pwshPath found:
    │      TryGetInstalledPwshVersion(pwshPath, out installedVersion, out rawVersion)
    │      (signature change: takes pwshPath instead of re-resolving it)
    │
    ├─ status = CPowerShellVersionChecker.EvaluatePwshVersionStatus(
    │               pwshPath, installedVersion, requiredVersion)   (new, pure;
    │               requiredVersion is nullable)
    │
    └─ switch (status):
           MeetsRequirement      → return
           VersionInconclusive   → Debug-log, return   (covers: manifest unreadable,
                                                          OR pwsh present but version
                                                          undeterminable - unchanged
                                                          soft-skip behavior)
           NotInstalled          → BuildPwshVersionFailureMessage(...), hard-fail (exit code 8)
           BelowRequirement      → BuildPwshVersionFailureMessage(...), hard-fail (exit code 8)
```

`NotInstalled` is checked first inside `EvaluatePwshVersionStatus`,
independent of whether `requiredVersion` resolved — this is what makes "PS7
missing *and* manifest unreadable" correctly hard-fail instead of silently
passing through the old manifest early-return.

`EvaluatePwshVersionStatus` and `BuildPwshVersionFailureMessage` both live in
`CPowerShellVersionChecker.cs` (the pure, no-WPF/no-CCollections half of the
partial class), so they stay linked into `VhcXTests.CrossPlatform` alongside
the existing manifest/version parsing tests and are verifiable in CI without
Windows.

## Components

**`CPowerShellVersionChecker.cs`** (pure half)
- `FindPwshExecutable()`: `private` → `internal`, so
  `ValidatePowerShellVersionMeetsVbrRequirement` can resolve the path once and
  reuse it, instead of `TryGetInstalledPwshVersion` re-scanning PATH
  internally.
- New: `PwshVersionStatus` enum — `MeetsRequirement`, `NotInstalled`,
  `VersionInconclusive`, `BelowRequirement`.
- New: `EvaluatePwshVersionStatus(string? pwshPath, Version? installedVersion, Version? requiredVersion) -> PwshVersionStatus`
  (`requiredVersion` is nullable — the manifest read can fail independently of
  pwsh's presence). Pure function, no I/O, checked in this order:
  - `pwshPath` null/empty → `NotInstalled` (checked first and unconditionally,
    so a simultaneously-unreadable manifest can't mask this case — see
    Solution)
  - `requiredVersion` null → `VersionInconclusive` (manifest unreadable/unparsable)
  - `installedVersion` null → `VersionInconclusive` (pwsh present but version undeterminable)
  - `installedVersion < requiredVersion` → `BelowRequirement`
  - else → `MeetsRequirement`
- New: `BuildPwshVersionFailureMessage(PwshVersionStatus status, string vbrFullVersion, Version? requiredVersion, string? rawInstalledVersion) -> string`.
  Pure function, no I/O. Only ever called for `NotInstalled`/`BelowRequirement`:
  - `NotInstalled`: "...requires PowerShell {requiredVersion} or higher" when
    `requiredVersion` happens to be known, else the generic "...requires
    PowerShell 7" (manifest may not have been readable); "...but no
    PowerShell 7 installation was found on this computer. Install PowerShell
    7 (https://aka.ms/powershell-release?tag=stable) and re-run..."
  - `BelowRequirement`: today's existing message text, unchanged, just
    relocated into this pure function so it's directly unit-testable (see
    Testing).

**`CPowerShellVersionChecker.Invocation.cs`**
- `TryGetInstalledPwshVersion` signature changes from
  `TryGetInstalledPwshVersion(out Version installedVersion, out string rawVersion)`
  to `TryGetInstalledPwshVersion(string pwshPath, out Version installedVersion, out string rawVersion)`.
  Drops its internal `FindPwshExecutable()` call; returns `false` immediately
  if `pwshPath` is null/empty (preserves current behavior for that case,
  just driven by the caller-supplied path instead of a redundant lookup).

**`CClientFunctions.cs`**
- `ValidatePowerShellVersionMeetsVbrRequirement()`: resolve `pwshPath` and the
  best-effort `requiredVersion` (the existing manifest read, kept as-is
  except it's no longer an early `return` — a null result just flows into
  `EvaluatePwshVersionStatus`), call `TryGetInstalledPwshVersion` only when a
  path was found, compute `status`, and switch on it as shown in Architecture.
  For `NotInstalled`/`BelowRequirement`, get the message from
  `BuildPwshVersionFailureMessage` and route through the existing hard-fail
  plumbing unchanged (`LOG.Error`, `Silent`/`GUIEXEC` branches,
  `Environment.Exit(SilentExit.PowerShellVersionUnsupported)` — reusing exit
  code 8, since both are the same underlying failure class: the installed
  PowerShell doesn't meet the VBR module's requirement).

**`CMessages.cs`**
- Line 95's silent-mode help text for exit code 8 currently reads "Installed
  PowerShell version is below what the VBR PowerShell module requires,"
  which is only accurate for the `BelowRequirement` sub-case. Update it to
  cover both, e.g. "PowerShell 7 missing, or its installed version is below
  what the VBR PowerShell module requires."

## Testing (TDD)

`ValidatePowerShellVersionMeetsVbrRequirement` reaches `SilentExit.ExitSilent`
or `Environment.Exit` directly on the hard-fail paths — both kill the process
unconditionally, with no test seam, and `CClientFunctionsGateTests.cs`'s
existing reflection tests only assert method visibility; none of them invoke
a gated method's hard-fail branch or inspect message content. So all new
logic is pushed into the two pure functions above, which are fully
unit-testable without touching the process-exit paths:

- `VhcXTests.CrossPlatform`: new tests for `EvaluatePwshVersionStatus` (all
  four branches, including the priority case that fixes the residual gap —
  `pwshPath` null with `requiredVersion` also null must still return
  `NotInstalled`, not `VersionInconclusive`) and for
  `BuildPwshVersionFailureMessage` (both statuses, and `NotInstalled` with
  `requiredVersion` both known and null), following the existing
  `CPowerShellVersionCheckerParsingTests.cs` pattern. `FindPwshExecutable`
  itself is an existing, already-implemented method (only its visibility
  changes) and needs no new test coverage.
- `VhcXTests`: mirror the same `EvaluatePwshVersionStatus` and
  `BuildPwshVersionFailureMessage` cases into
  `CPowerShellVersionCheckerTests.cs` (Windows-only mirror of the
  cross-platform tests, matching the existing duplication convention for this
  file).
- `CClientFunctionsGateTests.cs`: no changes. This fix doesn't alter the
  gate's call-site contract (`RunVbrPreflightGateIfTargeted` → private
  `GetVbrVersion` → `ValidatePowerShellVersionMeetsVbrRequirement` stays
  exactly as reachable/private as today), so there's nothing new for that
  file's structural tests to cover, and — per the untestable-exit-path
  problem above — no way to add a meaningful behavioral test there anyway.

## Out of scope

- Treating a "found but broken" pwsh.exe (e.g., corrupted binary that starts
  but crashes) as a hard failure — falls under `VersionInconclusive`,
  unchanged soft-skip behavior, per the false-positive-risk rationale above.
- Any change to the pre-v13 (PowerShell 5.1) path — already correctly
  unaffected by this check.
- A standalone/earlier startup preflight independent of
  `RunVbrPreflightGateIfTargeted` — rejected, see Solution above.

## Branch

`fix/issue-135-pwsh-missing-preflight`, off `dev`.
