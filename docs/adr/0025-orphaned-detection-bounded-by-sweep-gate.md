# ADR 0025: Orphaned Backup Detection Is Bounded by the Sweep's Allowlist Gate — Accepted, Not Overridden

* **Status:** Accepted
* **Date:** 2026-08-25
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design)
* **Relates to:** [ADR 0022](0022-allowlist-gate-for-restore-point-sweep.md) — this decision operates
  entirely within ADR 0022's existing scope and changes nothing about it.

## Context and Problem Statement

Issue #192 needs to detect restore points whose owning job doesn't resolve
at all (a deleted job, a foreign import) — "Orphaned Restore Points" per
`CONTEXT.md`. That detection can only happen as part of the global
`Get-VBRRestorePoint` sweep
([ADR 0021](0021-tiered-restore-point-sweep-for-job-sizing.md)): by
definition, there's no job to make a per-job call against when nothing
resolves.

[ADR 0022](0022-allowlist-gate-for-restore-point-sweep.md) gates that sweep
behind an allowlist of proven-safe job types, purely for job-sizing
performance reasons — environments made entirely of allowlisted types
never trigger it. This means Orphaned Backup detection has zero data
available in exactly those environments, through no fault of its own
detection logic.

## Considered Options

### Option A — Force the global sweep unconditionally whenever Orphaned detection runs

Override ADR 0022's gate specifically for this feature: always sweep, so
Orphaned detection has data everywhere.

**Rejected.** This reintroduces, for every all-safe-allowlist environment
(plausibly the majority of single-platform VMware/Hyper-V/Agent shops —
exactly the environments ADR 0022 exists to protect), the full sweep
performance cost ADR 0022 was written to avoid. The trade is made to catch
a comparatively rare case: a deleted job's leftover restore points, in an
otherwise entirely conventional environment.

### Option B — Accept the gap; report it explicitly (chosen)

## Decision

Orphaned Backup detection is only evaluated when the sweep already ran for
job-sizing reasons — i.e., when ADR 0022's own gate independently decided
to run it. When it didn't, the new report says so explicitly ("not
evaluated for this environment") rather than reporting a misleading "no
orphans found," which would be indistinguishable from "we checked and
there are none."

This is accepted *pending* multi-lab validation before this feature's PR
is raised. If that testing surfaces real-world cases where the gap is too
costly — e.g. a large, all-safe-allowlist environment carrying significant
orphaned data — the fallback is forcing the global sweep unconditionally,
which would then mean **superseding**, not working around, ADR 0022.

## Rationale

- This preserves ADR 0022's cost/correctness trade-off exactly as decided,
  rather than quietly special-casing it the first time a new feature wants
  more coverage than it provides.
- [ADR 0024](0024-superseded-backup-detection-two-mechanisms.md)'s
  Superseded detection is unaffected by this gap — it runs independently of
  whether the sweep fired, via the per-job `GetObjectsInJob()` check.
- An explicit "not evaluated" state is honest and cheap to build; a false
  "none found" is not just unhelpful but actively misleading for a report
  whose purpose is surfacing cleanup candidates.

## Consequences

### Positive
- Zero behavior or performance change to ADR 0022 — no sweep-forcing code
  exists anywhere in this feature.
- The limitation is visible to report readers, not silently absorbed into
  a clean-looking empty section.

### Negative
- Orphaned Backup detection has a real, environment-dependent blind spot:
  any environment made entirely of `$KnownSafeJobTypes` never gets it,
  regardless of how much genuinely orphaned data might exist there.
- Whether that blind spot matters in practice is explicitly not yet known
  — it depends on how common significant orphaned data is in otherwise-safe
  environments, which this design does not attempt to estimate.

## Validation

Pending: multi-lab testing per the implementation plan's Task 10, Step 4 —
confirming the accepted gap is acceptable in practice before this
feature's PR is raised. See
[`docs/superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md`](../superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md)
and
[`docs/superpowers/plans/2026-08-25-orphaned-superseded-backups-implementation.md`](../superpowers/plans/2026-08-25-orphaned-superseded-backups-implementation.md).
