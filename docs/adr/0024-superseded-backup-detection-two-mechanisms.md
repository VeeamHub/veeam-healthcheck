# ADR 0024: Superseded Backup Detection via Two Independent Mechanisms, Not One Unified Check

* **Status:** Accepted
* **Date:** 2026-08-25
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design, correction based on live-environment evidence)

## Context and Problem Statement

Designing issue #192 (Orphaned & Superseded Backups reporting) surfaced a
second category of restore point beyond the issue's original "no job
resolves at all" scope: restore points that resolve by name to a real,
*currently existing* job, but are excluded from that job's active size for
two different reasons:

1. [ADR 0023](0023-backupid-grouped-tiered-matching-for-all-job-types.md)'s
   Tier 2 suppression gate rejects a `BackupId` group's name match because
   the job already has a Tier 1 match elsewhere — today discarded via a
   bare `continue` in `Get-VhcJob.ps1`, nothing retained.
2. A restore point's `ObjectId` is no longer part of the job's current
   `Job.GetObjectsInJob()` membership — e.g. a rebuilt/re-registered
   machine's pre-rebuild data. This also turned out to be a live bug (filed
   as issue #197): `Get-VhcJob.ps1`'s `CalculatedOriginalSize`/
   `TotalOnDiskGB` computation has no such filter today, so this data is
   currently double-counted for job types outside ADR 0022's swept path.

The first draft of this design assumed `Job.GetObjectsInJob()` could
simply be layered onto every job type as the one mechanism for both
categories. Live evidence disproved that.

## Considered Options

### Option A — Unify onto `GetObjectsInJob()` alone, drop Tier 2 retention

Since `GetObjectsInJob()` operates at `ObjectId` granularity, could it also
subsume the Tier 2 suppression case and become the *only* Superseded
mechanism?

**Rejected.** This requires confirming `GetObjectsInJob()` behaves
reliably across every plugin platform (HPE Morpheus, Nutanix, oVirt,
Proxmox), which is unconfirmed and would block shipping this feature. The
two mechanisms also catch genuinely different failure shapes: Tier 2
suppression catches a job with *multiple resolved `BackupId` chains* where
tier ordering excluded one; `GetObjectsInJob()` catches a *specific stale
`ObjectId`* regardless of how many chains a job has. Unifying risks
silently losing the suppression case if `GetObjectsInJob()` doesn't
generalize.

### Option B — Trust `GetObjectsInJob()` unconditionally for every allowlisted type

**Rejected.** [ADR 0021](0021-tiered-restore-point-sweep-for-job-sizing.md)
already measured `GetObjectsInJob()` returning the vApp container instead
of 9 nested VMs for a VMware Cloud Director Backup job — itself on ADR
0022's `$KnownSafeJobTypes` allowlist. Trusting the call blindly would flag
all 9 real objects Superseded and zero the job's size — the exact
regression PR #194 fixed.

### Option C — Two independent mechanisms, `GetObjectsInJob()` guarded by ObjectId overlap (chosen)

## Decision

Both mechanisms run, feeding the same classification:

- **Swept job types:** Tier 1/Tier 2 grouping is unchanged; Tier
  2-*suppressed* groups are now retained (into a new
  `$script:VhcOrphanedSupersededCache`) instead of discarded.
- **Every job, regardless of `$NeedsSweep`:** a new per-job
  `Job.GetObjectsInJob()` vs. restore-point-`ObjectId` cross-reference,
  guarded by overlap: compute whether *any* of a job's restore-point
  `ObjectId`s match `GetObjectsInJob()`'s current membership. Zero overlap
  means the call isn't returning per-object granularity for this job — skip
  it entirely (flag nothing) rather than flag everything. At least one
  match means the call is trusted, and the non-matching restore points are
  excluded from `CalculatedOriginalSize`/`TotalOnDiskGB` (fixing #197) and
  cached as Superseded candidates.

Running the `GetObjectsInJob()` check unconditionally — not scoped to "the
non-swept path," as the first draft had it — also closes a mixed-environment
gap the first draft missed: `$NeedsSweep` (`Get-VhcJob.ps1:98`) is a single
environment-wide flag, not per-job-type. Any environment with even one job
outside the safe allowlist routes *every* job through the swept path,
where the original, narrower placement of this check never ran at all.
Decoupling it from `$NeedsSweep` also widens #197's fix to swept job types.

## Rationale

- The zero-overlap heuristic is the actual discriminator between "this
  API doesn't return per-object granularity for this job type" and "this
  object is genuinely stale." In the rebuild case, the *current* object
  still matches; only the stale one doesn't. In the Cloud Director case,
  *nothing* matches, because the returned object is the container, not any
  real VM.
- Decoupling from `$NeedsSweep` costs nothing extra: `GetObjectsInJob()`
  and the existing per-job restore-point lookup are already per-job calls,
  not the expensive global sweep ADR 0022 gates.
- **Accepted residual risk:** a job whose entire membership was legitimately
  swapped out at once (100% stale, 0% overlap — e.g. every VM in a job
  replaced simultaneously) is indistinguishable from the Cloud Director
  failure signature and gets skipped rather than flagged. This is a false
  negative (misses real Superseded data), not a false positive (never
  misattributes data or zeroes a real size) — consistent with this
  design's bias elsewhere (the accepted Orphaned-detection gap in [ADR
  0025](0025-orphaned-detection-bounded-by-sweep-gate.md), and
  `Get-VhcJob.ps1`'s own sweep-failure handling, which falls back to the
  per-job path rather than zeroing every job on error).

## Consequences

### Positive
- Closes the mixed-environment gap the first design draft missed.
- Widens #197's fix to swept job types, not just the non-swept path it was
  originally filed against.
- Ships without requiring per-platform confirmation that
  `GetObjectsInJob()` generalizes — the guard bounds the cost of being
  wrong about any given type.

### Negative
- Two mechanisms to maintain instead of one, with some conceptual overlap
  for swept job types (both can, in principle, catch the same underlying
  data via different signals).
- The 100%-membership-swap false-negative case (Rationale, above) is not
  caught by this design and is not yet validated against a real
  environment.

## Validation

Confirmed live: ADR 0021's own on-prem-lab measurement (`GetObjectsInJob()`
returning 1 of 9 real objects for a VMware Cloud Director vApp job). Full
detail, including the stale-`ObjectId` rebuild example
(`MALWARE`/`WindowsAgent08`), in
[`docs/superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md`](../superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md)
and
[`docs/plans/2026-08-25-orphaned-superseded-backups-implementation.md`](../plans/2026-08-25-orphaned-superseded-backups-implementation.md)
(Task 2). Multi-lab validation of the zero-overlap guard against a real
Cloud Director job is still pending before this branch's PR is raised.
