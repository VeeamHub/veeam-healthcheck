# ADR 0023: BackupId-Grouped Tiered Matching Replaces the Type=Snapshot Skip and the Separate Replica Loop

* **Status:** Accepted
* **Date:** 2026-08-22
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design, correction based on live-environment evidence)
* **Amends:** [ADR 0021](0021-tiered-restore-point-sweep-for-job-sizing.md)'s
  Snapshot/Replica-handling section and [ADR 0022](0022-allowlist-gate-for-restore-point-sweep.md)'s
  "Replica job types... are outside this decision entirely" carve-out. The
  rest of both ADRs (the general Tier 1/Tier 2 design, the allowlist-vs-flag
  decision) is unaffected and remains in force.

## Context and Problem Statement

ADR 0021 routed `Type=Snapshot` restore points and `TypeToString -like
"*Replication*"` jobs around the sweep entirely, on the stated basis that
"`Snapshot`-type restore points never resolve via `GetSourceJob()` (100%
throw rate, 5,461/5,461 in the on-prem lab)."

That claim doesn't hold. Live evidence from a different environment (a
`VMware Replication` job, `Replica_VC_NZGDC01`) shows `GetSourceJob()`
resolving a `Type=Snapshot` restore point correctly:

```
$RPs = Get-VBRRestorePoint -Backup $VBRJob.GetLastBackup()   # Type = Snapshot
$RPs[0].GetSourceJob()                                        # -> Replica_VC_NZGDC01, correct
```

Tracing the original measurement: the on-prem lab's two live replica jobs
both reported zero restore points via `GetLastBackup()` at test time, so
the 5,461 throws that were measured could not have come from them. The
actual, near-certain cause: `Type=Snapshot` is shared by two unrelated VBR
features — Replication Jobs and Storage-Snapshot Backups (storage-array
snapshot integration, e.g. NetApp/HPE/Pure) — and the throws came from the
latter, a different backup method entirely, not from replication. ADR 0021
had already flagged this exact gap in its own Consequences section ("the
Replica per-job path... has never been exercised against a live replica
chain with real snapshots") — this ADR is the missing validation, and it
points the opposite direction from the original claim.

Because the code routed on `TypeToString` (job-level), not `RestorePoint.Type`
(point-level), nothing was ever actually misattributed — Replica Jobs have
been sized correctly this whole time via the separate `GetLastBackup()`-based
loop. But that path never got Tier 1's multi-chain summing (restore points
across multiple backup chains, e.g. after a repository retarget, summed
together) — a limitation ADR 0021 fixed for every other job type but left in
place for Replication Jobs specifically, based on a false premise.

## Considered Options

### Option A — Keep the separate Replica loop, add multi-chain support to it

Extend the existing per-job `GetLastBackup()` + `Get-VBRRestorePoint -Backup`
loop to also discover and sum a Replication Job's other historical backup
chains, without touching the sweep's `Type=Snapshot` skip at all.

**Rejected.** Requires a second, parallel multi-chain-discovery mechanism
that duplicates logic the sweep already has, and does nothing for the
underlying misdiagnosis — the ADR 0021 text explaining *why* Replication
Jobs are special would remain factually wrong even if the code worked.

### Option B — Keep per-point resolution, just stop skipping by `Type`

Remove the `Type=Snapshot` skip and let every restore point attempt
`GetSourceJob()` individually, same as today's Tier 1, relying on the
existing try/catch to absorb failures.

**Rejected.** This reintroduces the exact performance risk ADR 0021's skip
existed to avoid — Storage-Snapshot Backups can still produce the same
large, exception-heavy population the original (misattributed) 5,461-throw
measurement actually came from. Fixing the misdiagnosis this way would
trade a correctness bug for a real, unaddressed performance regression.

### Option C — Group restore points by `BackupId`, resolve once per group (chosen)

## Decision

Group every swept restore point by `BackupId` before resolving ownership.
`BackupId` is confirmed, via live data (`Get-VBRRestorePoint | Group-Object
Name,BackupId`), to be scoped to exactly one protected object's chain within
one Job — group sizes matched each VM's restore-point count exactly. Every
restore point in a group therefore must resolve to the same owning Job, so
resolving ownership once per group and applying the result to every point in
it is safe, and turns "one `GetSourceJob()` call per restore point" into "one
call per protected object's retention chain" — the actual source of the
original performance concern, addressed directly instead of by excluding an
entire `Type` value from consideration.

This replaces the `Type=Snapshot` skip (both tiers) and the separate Replica
loop entirely. `VMware Replication` and `Hyper-V Replication` are added to
ADR 0022's `$KnownSafeJobTypes` allowlist, so a replica-only-plus-safe-types
environment still skips the sweep unchanged from today; the moment the sweep
runs for any other reason, Replication Jobs flow through the same grouped
Tier 1/2 mechanism as every other job type and gain multi-chain summing.

Tier 2's gating rule (a name match is only accepted if the resolved job has
zero Tier 1 matches anywhere in the whole population) still requires two
full passes over groups — all groups through Tier 1 first, then only the
unresolved ones through Tier 2 — not interleaved per group, or a stale-name
group processed before a same-job Id-match group arrives could be wrongly
accepted, reintroducing the exact stale-machine misattribution ADR 0021's
gating was built to prevent.

Full algorithm, diagnostics, and removed/changed code detail:
[`docs/superpowers/specs/2026-08-22-restore-point-backupid-grouping-design.md`](../superpowers/specs/2026-08-22-restore-point-backupid-grouping-design.md).

## Rationale

- **Fix the mechanism the performance risk actually lives in, not the
  symptom it was misattributed to.** The original skip conflated "this
  `Type` value is expensive to resolve at scale" with "this `Type` value
  belongs to replication and can't resolve at all" — two different claims,
  only the first of which has real evidence behind it.
- **`BackupId` grouping is a general-purpose fix, not a replication-specific
  carve-out.** It reduces API-call volume for *any* large population sharing
  long retention chains — including Storage-Snapshot Backups, the actual
  source of the original measurement — rather than trading one special case
  for another.
- **Two full passes, not interleaved, preserve an existing invariant.**
  Grouping changes the unit of work (groups instead of points) but must not
  change the order-independence Tier 2's gating rule depends on.

## Consequences

### Positive
- Replication Jobs gain multi-chain summing, matching every other job type.
- The Storage-Snapshot Backup population that actually caused the original
  performance concern gets the same amortization benefit as Replication
  Jobs — addressed directly instead of coincidentally.
- Removes an entire special-cased code path (`$ReplicaJobs`, the Replica
  loop, the `Type=Snapshot` skip in both tiers) in favor of one unified
  mechanism.

### Negative
- The `$HadPriorMatch`/lookup-failure-preservation logic added to the
  Replica loop immediately prior to this decision becomes dead code and is
  removed along with its tests — work done one commit before this one gets
  undone; called out explicitly so it doesn't read as churn.
- The real-world reduction factor for Storage-Snapshot Backups specifically
  is not yet measured against a live population of that type — see
  Validation below.

## Validation

Grouping's core safety property (all restore points sharing a `BackupId`
resolve identically) is confirmed for Replication Jobs via live data (three
VMs in one job, group sizes of 4/7/7 exactly matching each VM's restore-point
count). Full validation plan — a grouping-assumption audit across the whole
live population, old-vs-new sizing comparison including Replication Jobs,
and performance measurement against a Storage-Snapshot Backup population —
is in
[`docs/superpowers/specs/2026-08-22-restore-point-backupid-grouping-design.md`](../superpowers/specs/2026-08-22-restore-point-backupid-grouping-design.md)
and must run against `Test-JobSizingRestorePointMatching.ps1` before this
lands in `Get-VhcJob.ps1`.
