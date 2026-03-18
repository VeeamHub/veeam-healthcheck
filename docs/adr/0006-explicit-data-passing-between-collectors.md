# ADR 0006: Collectors Must Pass Data via Explicit Parameters and Return Values

* **Status:** Accepted
* **Date:** 2026-03-03
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (architecture review)

## Context and Problem Statement

After the `feat-refactor-vbr-config` refactor, a post-merge review identified two implicit
`$script:` variable dependencies between collector functions:

1. `Get-VhcBackupSessions` writes collected sessions to `$script:AllBackupSessions` as a
   side effect, and `Get-VhcSessionReport` reads that variable rather than receiving the
   data as a parameter. This creates a hidden execution-order constraint and makes both
   functions impossible to call in isolation without first manipulating module-scope state.

2. `Get-VhcBackupSessions` reads `$script:ReportInterval` directly from module scope rather
   than receiving it as a parameter, even though the value is available as a named variable
   in the orchestrator (`$ReportInterval`).

By contrast, the other data-dependent collector pairs in the same module already use explicit
data passing:
- `Get-VhcServer` returns server objects; the orchestrator captures them in `$VServers` and
  passes them to `Get-VhcConcurrencyData -VServers $VServers`.
- `Get-VhcRepository` returns repo details; the orchestrator captures them in
  `$RepositoryDetails` and passes them to `Get-VhcJob -RepositoryDetails $RepositoryDetails`.

The session pair did not follow this established pattern.

## Decision Drivers

- **Testability:** Functions that read `$script:` state can only be unit-tested by first
  populating that state, coupling every test to the module's initialization sequence.
- **Explicitness:** Hidden ordering constraints should appear in code, not in comments.
- **Consistency:** All other data-dependent collector pairs already use explicit passing.

## Considered Options

### Option A — Status Quo (keep $script: coupling)
Leave `$script:AllBackupSessions` and the `$script:ReportInterval` read in place. The
null-session guard added in `feat-refactor-vbr-config` already prevents silent failure.

**Cons:** Functions remain untestable in isolation; ordering dependency is implicit;
pattern diverges from all other collector pairs.

### Option B — Explicit Parameters and Return Values (chosen)
- `Get-VhcBackupSessions` adds a `[int] $ReportInterval` parameter and returns sessions
  via the pipeline instead of writing `$script:AllBackupSessions`.
- `Get-VhcSessionReport` adds an `[object[]] $BackupSessions` parameter and reads from
  that parameter instead of `$script:AllBackupSessions`. The caller (orchestrator) manages
  object lifetime; the function no longer nulls the variable on exit.
- The orchestrator captures `.Output` from the `Invoke-VhcCollector` result and passes it
  as `-BackupSessions` — identical to how `Get-VhcServer.Output` is passed to
  `Get-VhcConcurrencyData`.

**Cons:** Small orchestrator change required; existing null-session-guard test needs
updating to pass `$null` as a parameter rather than setting `$script:AllBackupSessions`.

## Decision

Option B. All collector functions must receive their data inputs as explicit parameters and
return their outputs as pipeline/return values. `$script:` module state is reserved for
infrastructure configuration set by `Initialize-VhcModule` and consumed only by the
infrastructure helpers (`Write-LogFile`, `Export-VhciCsv`). Collector business logic must
not read or write `$script:` variables to exchange data with other collectors.

## Consequences

* **Good:** Both functions are independently testable without module init.
* **Good:** Execution-order dependency is visible in orchestrator code.
* **Good:** Pattern is consistent with all other collector pairs in the module.
* **Bad:** One existing test (`GetVhcSessionReport_NullSessions_ThrowsDescriptiveError`)
  must be updated to use the new parameter signature.
* **Neutral:** `$script:AllBackupSessions` is removed entirely from the module.
  `Write-LogFile` and `Export-VhciCsv` may continue to use `$script:` state — they are
  infrastructure, not business logic.

## Validation

Verified by:
- Updated unit test confirming `Get-VhcSessionReport -BackupSessions $null` throws a
  descriptive error.
- New unit test confirming `Get-VhcBackupSessions` accepts a `-ReportInterval` parameter
  and fails with a VBR-not-available error (not a ReportInterval error) when called without
  pre-populating any `$script:` state.
- Post-refactor grep confirming `$script:AllBackupSessions` has zero references in the
  Public/ and Private/ directories.
