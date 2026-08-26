# ADR 0026: Tape Backup Exclusion via `IsTapeBackup`, Not `TypeToString`

* **Status:** Accepted
* **Date:** 2026-08-25
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design, correction based on live-environment evidence)

## Context and Problem Statement

Restore points copied to tape resolve neither Tier 1 nor Tier 2 of the
sweep's job-matching logic: `GetSourceJob()` resolves to the tape job
itself, but that job comes from `Get-VBRTapeJob`, a separate cmdlet family
never present in `$Jobs` (populated by `Get-VBRJob`); Tier 2's name-based
fallback also fails, since a tape copy's `GetParentOrThis().Name` ends in
`" on Tape"`, never matching a live job's name. Both tiers legitimately
fail — the exact shape issue #192's classification logic treats as
"Orphaned."

`CONTEXT.md` already documents Tape Backup as a third, expected-unmatched
category, explicitly distinct from Orphaned Restore Point ("unmatched for
a different, expected reason"), and
[ADR 0021](0021-tiered-restore-point-sweep-for-job-sizing.md)'s own
Rationale makes the same point. But nothing in the original #192 design
implemented that distinction — every tape-using environment would have
flooded the new report with false Orphaned rows for perfectly healthy tape
jobs.

## Considered Options

### Option A — Detect tape via `TypeToString` containing "Tape"

The seemingly obvious signal, and consistent with an initial VMware-to-tape
example (`.GetBackup().TypeToString` reads `"VMware Backup to Tape"`).

**Rejected.** Disproven live: a Proxmox-to-tape copy's immediate
`.GetBackup().TypeToString` reads `"Proxmox"` — no `"Tape"` substring at
all — at the exact same object level where the VMware example read
`"VMware Backup to Tape"`. The signal is platform-dependent and unreliable.

### Option B — Detect tape via the Backup's `Name` pattern (`"<job> on Tape"`)

**Considered, not chosen as the primary signal.** String-matching a
display-name convention is fragile to naming/localization edge cases a
dedicated boolean property doesn't have, even though the pattern itself is
documented and confirmed live.

### Option C — `.GetBackup().IsTapeBackup`, checked at the immediate (level-1) object (chosen)

## Decision

Before any Orphaned/Superseded classification, check
`.GetBackup().IsTapeBackup`. If `true`, drop the `BackupId` group entirely
— not reported as Orphaned, Superseded, or a third visible category,
matching `CONTEXT.md`'s framing of Tape Backup as expected, not a cleanup
candidate.

This must be checked on the **immediate** `.GetBackup()` result, not
`.GetParentOrThis()`: `GetParentOrThis()` walks past the tape-copy
relationship to the original disk backup, which correctly reports
`IsTapeBackup = False` (it isn't tape) — checking at that level would
silently defeat the exclusion entirely.

## Rationale

- `IsTapeBackup` is a purpose-built boolean, observed consistent
  (`True`/`False`) across both platforms tested, where `TypeToString` was
  not.
- This mirrors a pattern already established in this codebase's history:
  ADR 0021's own `Type=Snapshot`/Replication conflation was a case of "the
  first example tested looked sufficient, a second platform disproved it."
  The same shape recurred here and gets the same treatment — verify against
  more than one platform before trusting a signal.
- `Name`-pattern matching (Option B) remains available as a secondary
  cross-check if `IsTapeBackup` ever proves unreliable for a platform not
  yet tested, without requiring a design change — it's a fallback worth
  keeping in mind, not a decision made here.

## Consequences

### Positive
- Correctly excludes real tape backups everywhere the boolean is
  populated, across at least the two platforms tested.
- Implements a distinction `CONTEXT.md` and ADR 0021 already called for
  but that no code previously acted on.

### Negative
- Relies on an undocumented, internal object property — not present in the
  public PowerShell SDK reference (`docs/powershell/vbrbackupobject.md`
  and friends have no entry for it) — the same category of risk this
  codebase already accepts for `GetSourceJob()`, `GetParentJob()`, and
  `GetObjectsInJob()`.
- Not yet tested against every backup-to-tape variant (e.g. File Backup to
  Tape, Backup Copy to Tape) — only VM-backup-to-tape copies from two
  source platforms have been confirmed live.

## Validation

Confirmed live against a VMware-to-tape copy and a Proxmox-to-tape copy
during this design's investigation (see
[`docs/superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md`](../superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md)).
Further tape variants to be confirmed during the implementation plan's
multi-lab validation (Task 10, Step 4).
