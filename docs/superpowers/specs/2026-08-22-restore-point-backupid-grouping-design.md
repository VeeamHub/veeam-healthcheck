# Design: BackupId-Grouped Tiered Matching Replaces the Type=Snapshot Skip and the Separate Replica Loop

* **Status:** Approved (brainstorming), pending implementation plan
* **Date:** 2026-08-22
* **Author:** Ben Thomas, with Claude Code (design)
* **Supersedes:** The Snapshot/Replica-handling section of [ADR 0021](../../adr/0021-tiered-restore-point-sweep-for-job-sizing.md)

## Context and Problem Statement

ADR 0021 introduced a two-tier, Id-based + name-based sweep to replace the
old per-job `GetLastBackup()` sizing method, but routed `Type=Snapshot`
restore points and `TypeToString -like "*Replication*"` jobs around the
sweep entirely, on the stated basis that "`Snapshot`-type restore points
never resolve via `GetSourceJob()` (100% throw rate, 5,461/5,461 in the
on-prem lab)."

That claim was wrong, or at least badly overgeneralized. Live evidence from
a different environment (`Replica_VC_NZGDC01`, a `VMware Replication` job)
shows `GetSourceJob()` resolving a `Type=Snapshot` restore point correctly:

```
$RPs = Get-VBRRestorePoint -Backup $VBRJob.GetLastBackup()   # Type = Snapshot
$RPs[0].GetSourceJob()                                        # returns Replica_VC_NZGDC01 correctly
```

Tracing the original measurement's own write-up (the 2026-08-21 design
spec) confirms the root cause: the on-prem lab's 5,461 `Type=Snapshot`
throws did not belong to either of that lab's two live replica jobs (both
reported zero restore points via `GetLastBackup()` at test time). The most
likely explanation, confirmed against VBR's data model by the person who
ran the original test: `Type=Snapshot` is shared by two unrelated VBR
features — VM replication (VMware/Hyper-V Replication jobs) and
storage-array snapshot-based backups (e.g. NetApp/HPE/Pure integration) —
and the throws came from the latter, an entirely different backup method
that was never a replica job at all. The "100% throw rate" was real, but it
was measured on the wrong population to support the conclusion drawn from
it. ADR 0021 already flagged this gap explicitly in its own Consequences
section: "the Replica per-job path... has never been exercised against a
live replica chain with real snapshots."

Because the current code routes on `TypeToString -like '*Replication*'`
(job-level), not on `RestorePoint.Type` (point-level), it never actually
misattributed anything — replica jobs are sized correctly today via the
separate `GetLastBackup()`-based Replica loop. But that path never benefits
from Tier 1's multi-chain summing (restore points across multiple backup
chains — e.g. after a repository retarget — are only summed for
sweep-matched jobs), so a replica job with more than one relevant chain
would undercount, the same limitation ADR 0021 fixed for every other job
type.

## Decision

Fully replace the `Type=Snapshot` skip and the separate Replica loop with a
single, unified mechanism: **group all swept restore points by `BackupId`
before resolving ownership, resolve each group once, and apply the result
to every restore point in the group.**

### Why grouping is safe

`BackupId` is confirmed (live data, `Get-VBRRestorePoint -Backup
$Job.GetLastBackup() | Group-Object Name,BackupId`) to be scoped to exactly
one protected object (VM/agent) within one backup chain — group counts
matched each VM's restore-point count exactly (4, 7, 7 for three VMs in one
job). A single job with N protected objects has N distinct `BackupId`
groups, not one. Every restore point sharing a `BackupId` therefore belongs
to the same object's history and must resolve to the same owning job via
`GetSourceJob()` — grouping does not risk misattributing points within a
group to different jobs.

This also directly explains the performance win: a `BackupId` group
collapses a VM's entire retention chain (e.g. 30 days of increments) into
one `GetSourceJob()`/`GetParentJob()` call instead of one per restore
point. It does not collapse to "one call per job" (a job with many VMs
still needs one call per VM) — the win scales with retention depth, not job
count.

### Algorithm

Two full passes over **groups**, preserving the same two-tier structure and
gating rule ADR 0021 established — order matters here specifically because
Tier 2's gate ("a name match is only accepted if the resolved job has zero
Tier 1 matches, anywhere in the whole population") must see a *complete*
Tier 1 result before any Tier 2 acceptance decision. Interleaving
Tier 1 → Tier 2 per group instead of two full passes would let a stale
name-match land before a later group's Id-match for the same job arrives,
recreating exactly the stale-machine misattribution ADR 0021's gating was
designed to prevent (a decommissioned VM's restore point sharing a display
name with the currently active one under the same policy job).

```
$AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })
$Groups = $AllRestorePoints | Group-Object -Property BackupId

# Pass 1: every group through Tier 1
$UnresolvedGroups = [System.Collections.ArrayList]::new()
foreach ($Group in $Groups) {
    $Representative = $Group.Group[0]
    # GetSourceJob() -> GetParentJob() walk-up. Two distinct failure outcomes,
    # same distinction the current per-point code makes:
    #   - the call itself throws                          -> $Tier1Failed += $Group.Count
    #   - it resolves, but the Id isn't in $KnownJobIds    -> no counter bump, just unresolved
    # Either way the group falls through to tier 2 unresolved.
    if (resolved AND validated against $KnownJobIds) {
        foreach ($RestorePoint in $Group.Group) { bucket into $RestorePointsByJob[$JobIdKey] }
        $Tier1Matched += $Group.Count
    } else {
        [void]$UnresolvedGroups.Add($Group)
        if (the GetSourceJob()/GetParentJob() call itself threw) { $Tier1Failed += $Group.Count }
    }
}

# $Tier1MatchedJobIds is snapshotted from $RestorePointsByJob.Keys once,
# here, after pass 1 fully completes - same one-time-snapshot approach as
# the existing #8 fix from the PR 194 review, for the same reason: a live
# check during pass 2 would let a group Tier 2 itself already placed on a
# job look "already matched" and get dropped instead of summed.
$Tier1MatchedJobIds = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$RestorePointsByJob.Keys, [System.StringComparer]::OrdinalIgnoreCase
)

# Pass 2: only unresolved groups through Tier 2, gated on the now-complete $Tier1MatchedJobIds
foreach ($Group in $UnresolvedGroups) {
    $Representative = $Group.Group[0]
    # GetBackup().GetParentOrThis().Name lookup, gated as today
    if (accepted) {
        foreach ($RestorePoint in $Group.Group) { bucket into $RestorePointsByJob[$JobIdKey] }
        $Tier2Matched += $Group.Count
    }
}
```

`$Tier1Matched`/`$Tier2Matched`/`$Tier1Failed` stay restore-point-denominated
(a group's outcome credited to every point in it) so the existing summary
log line's arithmetic (`Total - Tier1Matched - Tier2Matched = unmatched`)
keeps its current meaning unchanged.

### New diagnostic line

The point-denominated counters don't expose the actual win (fewer API
calls), so a second, separate log line reports it:

```
Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2, $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched) unmatched/orphaned/snapshot"
Write-LogFile "BackupId grouping: $($AllRestorePoints.Count) restore points reduced to $($Groups.Count) groups ($($Groups.Count) tier-1 lookups + $($UnresolvedGroups.Count) tier-2 lookups attempted, vs. $($AllRestorePoints.Count) lookups pre-grouping)"
```

Both wrapped in their own `try {} catch {}`, consistent with every other
`Write-LogFile` call inside the sweep (a logging failure must not be able
to disable the sweep or abort the run).

### Performance gate (ADR 0022) simplification

`'VMware Replication'` and `'Hyper-V Replication'` are added to
`$KnownSafeJobTypes`. This means:
- An environment with only safe-allowlisted job types plus a replica job
  still skips the sweep entirely (unchanged from today — the replica falls
  back to the single-chain `GetLastBackup()` method, same limitation as
  now, just without a dedicated code path for it).
- The moment the sweep runs for *any* reason (any other job type not on the
  allowlist), replica jobs flow through the same grouped Tier 1/2 mechanism
  as everything else and gain multi-chain summing.

Because the replica carve-out was the only reason `$NonReplicaJobs`/
`$ReplicaJobs` existed, `$NeedsSweep` simplifies to one check directly over
`$Jobs`:

```powershell
$NeedsSweep = [bool]($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)
```

### What gets removed

- The `if ($RestorePoint.Type -eq 'Snapshot') { continue }` skip, both
  tiers.
- The entire `foreach ($Job in $ReplicaJobs) { ... }` Replica loop, and the
  `$ReplicaJobs`/`$NonReplicaJobs` split.
- The `$HadPriorMatch`/`$LookupFailed` prior-match-preservation logic added
  for the PR 194 review's finding #3 — it exists only to protect a
  now-deleted code path (the Replica loop overwriting a prior tier-1/2
  match on lookup failure) and becomes dead code. Its dedicated Pester
  tests come out too. This should be called out explicitly in the PR
  description since it directly undoes work from the immediately preceding
  commit.

### What stays unchanged

`$KnownJobIds`/`$JobIdByName` construction, the `$KnownJobIds` validation
step, and the multi-chain summing math in the main consumption loop (it
already just sums whatever restore points land in
`$RestorePointsByJob[$JobIdKey]`, agnostic to how they got there).

## Documentation

- **New ADR 0023** supersedes the Snapshot/Replica-handling section of ADR
  0021: documents the corrected root cause (storage-array-snapshot backups
  and VM replication both report `Type=Snapshot`, conflated in the original
  measurement), the `BackupId`-grouping mechanism, and its validation.
- A short correction note added to the 2026-08-21 design spec (not a
  rewrite — it's a historical record of what was actually tested at the
  time; the correction should point forward to ADR 0023).

## Validation Plan

Before any change to `Get-VhcJob.ps1`, update
`Test-JobSizingRestorePointMatching.ps1` (still scratch-only, not part of
the shipped module) to run three checks against live labs:

1. **Grouping-assumption audit** — for every `BackupId` group, call
   `GetSourceJob()` on *every* point in the group (not just one) and assert
   they all resolve identically (same Job Id, or the same failure mode).
   This generalizes the manual two-example proof to the full live
   population before the optimization is trusted in production.
2. **Old vs. new sizing comparison**, per job, across all job types
   including replicas now routed through the unified path. Zero
   regressions is the bar, same standard ADR 0021's original validation
   used.
3. **Performance measurement** — wall-clock old vs. new, plus the new
   group-count/call-count diagnostic, ideally against an environment with a
   real storage-snapshot-backup population (not just VM replication) to
   measure the reduction factor against the population that actually
   caused the original throw-rate measurement.

Then update the production `Get-VhcJob.Tests.ps1` suite: replace the
Replica-loop-specific `Describe` blocks with grouping-focused ones —
multiple restore points sharing a `BackupId` resolve via one simulated
`GetSourceJob()` call and all land in the same bucket; a failed group
leaves all its points unresolved together; Tier 2's gate still holds across
group boundaries (a stale-name group processed before a same-job Id-match
group must still be suppressed, proving the two-full-passes ordering).

## Open Items

- Whether any job type *other* than storage-array-snapshot backups and VM
  replication also produces `Type=Snapshot` restore points is unconfirmed —
  the design doesn't need to know, since it no longer branches on `Type` at
  all, but worth keeping in mind if the live-lab validation surfaces a
  third category.
- Real-world measurement of the grouping reduction factor against a large
  storage-snapshot-backup population is still needed (validation item 3
  above) — the VM-replication case is confirmed correct and beneficial, but
  the *storage-snapshot* case is exactly where the original performance
  concern lived, and it's the case this design has the least direct
  evidence for so far.
