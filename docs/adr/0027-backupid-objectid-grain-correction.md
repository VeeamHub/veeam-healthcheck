# ADR 0027: CSV/Report Grain Is `(BackupId, ObjectId)` — `BackupId` Is Not 1:1 With `ObjectId`

* **Status:** Accepted
* **Date:** 2026-08-25
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design, correction based on live-environment evidence)
* **Corrects:** a claim in PR #194's own description ("`BackupId` is
  confirmed, live, to be scoped to exactly one protected object's chain
  within one job") — see Context. [ADR 0023](0023-backupid-grouped-tiered-matching-for-all-job-types.md)'s
  actual decision and shipped code are unaffected; only that description's
  stronger corollary claim needs correcting.

## Context and Problem Statement

The first draft of issue #192's design treated "one CSV row per `BackupId`
group" as equivalent to "one row per protected object," on the strength of
PR #194's own description text quoted above.

Live evidence disproves the "one object" half of that claim: a 3-VM
Hyper-V job (`Hyper-V - Test multiple VMs`) targeting a repository with
per-VM chains disabled (`Backup.IsTruePerVmContainer == $false`) produces
restore points for all 3 VMs (`vtestvm01`, `vtestvm02`, `vtestvm03`) under
**one shared `BackupId`**, each with its own distinct `ObjectId`.

This matters for #192 specifically because its report needs true per-object
granularity (fulls/incrementals/sizes/dates per machine) — the whole point
of surfacing "which specific object is stale," not just "something in this
job is stale."

## Considered Options

### Option A — Keep "one row per `BackupId`" grain; blend multiple objects' stats when this occurs

**Rejected.** Silently loses exactly the per-object granularity issue #192
exists to provide, for a repository configuration (per-VM chains disabled)
that isn't rare.

### Option B — Switch grain entirely to "one row per `ObjectId`," drop `BackupId`-group resolution

**Rejected.** Would need its own job/repository/type resolution path keyed
per `ObjectId` rather than reusing the sweep's existing once-per-`BackupId`-group
resolution — duplicating work ADR 0023 already amortized for exactly this
reason (turning "one call per restore point" into "one call per retention
chain").

### Option C — Resolve job/repo/type once per `BackupId` group, split into one row per `ObjectId` within it (chosen)

## Decision

CSV/report grain is `(BackupId, ObjectId)`, not `BackupId` alone.
`BackupId` is **not** a unique key on its own — multiple rows can share one
when per-VM chains are disabled on the target repository.

Job name, original job type, repository Id, and Orphaned/Superseded
classification are still resolved **once per `BackupId` group** — this
remains safe and cheap, since ADR 0023's actual safety property is "every
restore point sharing a `BackupId` resolves to the same owning job," which
still holds (`GetSourceJob()` on any of the 3 VMs' restore points resolves
to the same job). Only the object-level stats (`FullCount`,
`IncrementalCount`, average/total size, oldest/newest restore point) are
computed per `ObjectId` within that group.

## Rationale

- This doesn't threaten [ADR 0023](0023-backupid-grouped-tiered-matching-for-all-job-types.md)'s
  shipped correctness at all: its optimization only ever needed "one job
  per `BackupId` group," not "one object per group." ADR 0023 itself does
  not need superseding — only its description's overstated corollary claim
  does, which this ADR corrects.
- Reusing the once-per-group resolution keeps this feature's added cost
  proportional to the number of retention chains, not the number of
  restore points or objects — consistent with ADR 0023's own cost model.
- `CONTEXT.md`'s "Backup" glossary entry is updated alongside this ADR to
  state the corrected fact directly, so it doesn't require reading this
  ADR to avoid repeating the same mistake in future work.

## Consequences

### Positive
- Correct per-object granularity regardless of a repository's per-VM-chains
  setting.
- No change needed to ADR 0023's shipped mechanism.

### Negative
- `BackupId`'s uniqueness can no longer be assumed anywhere in this
  codebase without checking — any future code that keys off `BackupId`
  alone as if it uniquely identified one object needs to be checked
  against this finding.
- The reverse direction (one `ObjectId` spanning more than one `BackupId`
  over its lifetime, e.g. after a job retarget) remains separately true and
  unaffected by this ADR — it simply produces multiple rows for that
  `ObjectId`, which was already the design's expectation.

## Validation

Confirmed live: a 3-VM Hyper-V job on a per-VM-chains-disabled repository,
one shared `BackupId`, three distinct `ObjectId`s, `IsTruePerVmContainer =
$false` on the resulting `Backup` object. See
[`docs/superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md`](../superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md).
The implementation plan's Task 3 tests this grain directly (one CSV row
per `ObjectId` within a shared-`BackupId` group); Task 10 calls for
re-confirming it against a real lab before this feature's PR is raised.
