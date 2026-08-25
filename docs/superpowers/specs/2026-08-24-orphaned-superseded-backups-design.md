# Design: Orphaned & Superseded Backups Reporting

## Context and Problem Statement

PR #194 (ADR 0021/0022/0023) fixed the 0 MB/0 GB job-sizing bug by adding a
tiered `Get-VBRRestorePoint` sweep for job types where the old per-job path
(`Get-VBRRestorePoint -Backup $Job.GetLastBackup()`) throws or silently loses
restore points. That work surfaced two categories of restore point that
today are invisible in the report but still consume disk space:

1. **Orphaned Restore Points** — no owning Job resolves at all (deleted job,
   imported backup, machine removed from a Policy Job's scope). This was
   issue #192's original scope.
2. **Superseded Restore Points** — resolve by name to a real, currently
   existing Job, but are excluded from that Job's active size: either a
   `BackupId` group suppressed by the Tier 2 gate (ADR 0023) because the Job
   already has Tier 1 matches elsewhere, or (discovered during this design
   session) a restore point whose `ObjectId` is no longer in the Job's
   `GetObjectsInJob()` membership — e.g. a rebuilt/re-registered machine's
   pre-rebuild data.

Investigating category 2 surfaced a **currently-shipping bug** (filed as
issue #197): for job types on the ADR 0022 "safe" allowlist, the per-job
sizing path has no suppression logic at all, so `CalculatedOriginalSize`
and `TotalOnDiskGB` can double-count a rebuilt object's old and new
`ObjectId` restore points under the same job. Both terms are now defined in
`CONTEXT.md`.

A design review (before implementation started) and further live-lab
investigation surfaced three correctness gaps in the first draft of this
design, all fixed below: `GetObjectsInJob()` returns a container object
instead of per-VM entries for at least one already-allowlisted job type
(VMware Cloud Director Backup — documented in ADR 0021 itself), the
classification logic had no way to exclude Tape Backup restore points
(`CONTEXT.md` already distinguishes them from Orphaned, but nothing
implemented that distinction), and the stale-`ObjectId` check was originally
scoped only to non-swept job types, which is silently inert in any mixed
environment because `$NeedsSweep` is a single environment-wide flag, not a
per-job-type one.

Goal: add a new report section that surfaces both categories per-repository
— what jobs/backups they belong to, how much data, how old, and (for
Orphaned rows) what platform the data originally came from — while fixing
#197 as part of the same underlying detection logic.

## Decision

### Scope

- Both categories, in one section, in one branch/PR: `feat/issue-192-orphaned-superseded-backups`.
- VBR only — no VB365 equivalent (VB365 has no restore-point/job-matching model to speak of).
- No age/size threshold — list everything, sorted oldest-first per repo. A
  cleanup-candidate report that hides some candidates because they're "too
  new" undermines its own purpose.
- Runs automatically whenever detection data is available — no new CLI flag.

### Excluded before classification: Tape Backups

Before anything is classified Orphaned or Superseded, each `BackupId`
group is checked for `.GetBackup().IsTapeBackup` (level 1 — the immediate
Backup object for that group, *not* `.GetParentOrThis()`, which walks past
the tape-copy relationship and reports `False`). If `True`, the group is
dropped entirely — not reported as Orphaned, Superseded, or a third visible
category, matching `CONTEXT.md`'s existing framing of Tape Backup restore
points as "unmatched for a different, expected reason," not a cleanup
candidate. `IsTapeBackup` is a property of the Backup object, so every
restore point within one `BackupId` group shares the same value — checking
once per group (at the point where `.GetBackup()` is already being called
for `JobName`/`RepositoryId` resolution) is sufficient.

This has to be a dedicated boolean check, not a string match on
`TypeToString`: live evidence shows `TypeToString` is *not* a reliable tape
signal across platforms. A VMware-to-tape copy read `"VMware Backup to
Tape"` (contains "Tape"), but a Proxmox-to-tape copy read `"Proxmox"` (no
"Tape" substring at all) on the same property, at the same object level.
`IsTapeBackup` was consistent (`True`/`False`) across both.

Why tape restore points would otherwise land in Orphaned: `GetSourceJob()`
on a tape-copied restore point doesn't throw — it resolves to the tape job
itself (a real object, e.g. `"Tape - Proxmox Backups"`), but that job comes
from `Get-VBRTapeJob`, a separate cmdlet family, and is never present in
`$Jobs` (populated by `Get-VBRJob`). Tier 1's existing validation against
`$Jobs`'s own Ids (already in the PR #194 sweep) correctly rejects this
match, and Tier 2's name-based fallback also fails — a tape copy's
`GetParentOrThis().Name` ends in `" on Tape"`, which never matches a live
job's name. Both tiers legitimately fail, same as a true orphan — which is
exactly why the classification logic needs a dedicated exclusion rather
than relying on Tier 1/2 failure to imply Orphaned.

### Detection: two categories, two independent mechanisms

For everything that survives the Tape exclusion above, classification is
`CurrentJobId == Guid.Empty` (or not found in `$Jobs`) → **Orphaned**;
resolves to a real, current Job → **Superseded**. Both `JobName`,
`OriginalJobType` (`.TypeToString`), and `RepositoryId` come from the same
call — `.GetBackup().GetParentOrThis()` — for both categories, so grouping
is uniform regardless of which mechanism flagged the row. Confirmed live:
`.GetBackup().GetParentOrThis()` on a restore point whose owning job was
deleted still returns a valid object with `JobName`, `TypeToString`, and
`RepositoryId` populated, `JobId` zeroed.

Two independent detection paths feed the same classification, kept
separate rather than merged into one mechanism — not because
`GetObjectsInJob()` is avoided for plugin-platform job types (the guard
below is exactly what makes it safe to run there without per-platform
confirmation first), but because the two mechanisms catch different
things: Tier 2 suppression-retention catches a job with *multiple resolved
`BackupId` chains* where tier ordering excluded one, while the
`GetObjectsInJob()` cross-reference catches a *specific stale `ObjectId`*
regardless of how many chains a job has. Neither subsumes the other, so
both run for every swept job type:

- **Swept job types** (ADR 0022 non-allowlisted): reuse the existing Tier
  1/Tier 2 grouping. Tier 2-*suppressed* groups — today discarded via a bare
  `continue` in `Get-VhcJob.ps1`'s matching loop with nothing retained — get
  retained instead, tagged Superseded.
- **Every job, regardless of `$NeedsSweep`**: a new per-job
  `$Job.GetObjectsInJob()` vs restore-point-`ObjectId` cross-reference,
  run unconditionally rather than scoped to "the non-swept path." The first
  draft of this design tied it to the non-swept branch, which meant it
  silently never ran in any mixed environment — `$NeedsSweep` is one
  environment-wide flag (`Get-VhcJob.ps1:98`); if any job needs the sweep,
  *every* job (including otherwise-safe VMware/Hyper-V/Agent ones) routes
  through the swept path, where no equivalent check existed. Running it
  unconditionally, per job, closes that gap and widens #197's fix to swept
  job types too. It's still cheap: `GetObjectsInJob()` is a per-job call
  either way.

  **Safety guard, not a per-type allowlist:** `GetObjectsInJob()` is not
  trustworthy for every allowlisted type — ADR 0021 already measured it
  returning the vApp container instead of 9 nested VMs for a VMware Cloud
  Director Backup job (itself on `$KnownSafeJobTypes`). Trusting it blindly
  would flag all 9 real objects Superseded and zero the job's size — the
  exact regression PR #194 fixed. Rather than maintain an exhaustive
  per-type "is `GetObjectsInJob()` reliable here" list, the check computes
  the overlap between a job's restore-point `ObjectId`s and
  `GetObjectsInJob()`'s current membership: if **zero** restore points
  match any current object, `GetObjectsInJob()` isn't returning per-object
  granularity for this job — skip the check for that job entirely (log it,
  flag nothing) rather than flag everything. If **at least one** restore
  point matches, the check is trusted and the non-matching ones are flagged
  Superseded. This is what distinguishes the Cloud Director failure mode
  (0 of 9 match — self-evidently broken) from the legitimate detection case
  a rebuilt machine produces (the current object still matches; only the
  stale one doesn't).

  **Accepted residual risk:** a job whose entire membership was
  legitimately swapped out (100% stale, 0% overlap — e.g. every VM in a
  job was replaced at once) looks identical to the Cloud Director failure
  signature and gets skipped rather than flagged. That's a false negative
  (misses real Superseded data), not a false positive (never misattributes
  data to the wrong job or zeroes a real size) — consistent with this
  design's existing bias elsewhere (the accepted Category A gap, and
  `Get-VhcJob.ps1`'s own sweep-failure handling, which falls back to the
  per-job path rather than zeroing every job). Scheduled for multi-lab
  validation rather than solved here.

**Orphaned detection requires the global sweep to have run at all.**
Environments made entirely of ADR 0022 "safe" allowlist job types never
trigger it, so Orphaned Restore Points are invisible there today and will
remain so under this design. Accepted trade-off: forcing the sweep
unconditionally for every environment, purely to catch a comparatively rare
deleted-job case in otherwise-safe environments, reintroduces the exact
performance cost ADR 0022 was written to avoid. The report shows an
explicit **"Orphaned Backups: not evaluated for this environment"** state
for those environments rather than a misleading "none found."

This gap is accepted *pending* multi-lab validation before the PR is
raised — if that testing surfaces real-world cases where the gap is too
costly (e.g., large all-safe-allowlist environments carrying significant
orphaned data), the fallback is forcing the global sweep unconditionally,
which would mean superseding ADR 0022 rather than layering on top of it.

### Bundled fix: issue #197 (sizing double-count)

The same `GetObjectsInJob()` cross-reference that detects Superseded rows
also fixes `Get-VhcJob.ps1`'s existing `CalculatedOriginalSize`/
`TotalOnDiskGB` computation: restore points for an `ObjectId` no longer in
current job membership (per the safety-guarded check above) are excluded
from those sums instead of being silently included. Since the check now
runs unconditionally rather than only on the non-swept path, this fix
applies to swept job types too, not just the original non-swept scope #197
was filed against. Ship in the same branch as #192,
`Fixes #192, fixes #197` on the eventual commit(s).

### Collection: new script, shared cache

A new script, `Get-VhcOrphanedSupersededBackups.ps1`, runs after
`Get-VhcJob.ps1` in the collection pipeline. Kept separate rather than
folded into `Get-VhcJob.ps1` — that script already carries the 0MB-bug-fix
history and its own extensive test suite (`Get-VhcJob.Tests.ps1`); a
separate script keeps "size this job" and "find stale data" as distinct
responsibilities and lets the new logic be tested in isolation.

`Get-VhcJob.ps1` changes to support it:

1. The sweep's per-`BackupId` groups — Tier 1 matches, Tier 2 accepted,
   Tier 2 *suppressed*, and unresolved-by-either-tier — are retained into a
   `$script:`-scoped cache instead of being discarded inline. This avoids a
   second global `Get-VBRRestorePoint` sweep, which would double the exact
   cost ADR 0022's allowlist gate exists to save.
2. The new `GetObjectsInJob()` cross-reference (with its zero-overlap
   safety guard, above) runs per-job, for every job regardless of
   `$NeedsSweep`, and writes excluded restore points into the same cache
   shape, tagged Superseded.
3. If the sweep didn't run at all (pure-safe-allowlist environment) or
   failed, the cache is absent/empty and carries an explicit marker so the
   new script (and downstream report) can distinguish "not evaluated" from
   "evaluated, found nothing." This only affects Orphaned coverage — the
   `GetObjectsInJob()` check for Superseded/#197 doesn't depend on the
   sweep having run.

**Grain correction (live evidence):** `BackupId` is *not* 1:1 with
`ObjectId`. A multi-VM job targeting a repository with per-VM chains
disabled (`.GetBackup().IsTruePerVmContainer == $false`) produces restore
points for every protected VM under **one shared `BackupId`** — confirmed
live with a 3-VM Hyper-V job, all three VMs' restore points carrying the
same `BackupId` but three distinct `ObjectId`s. This doesn't threaten the
existing PR #194 sweep's correctness: `GetSourceJob()` on any restore point
in such a group resolves to the same job regardless of which VM it's
called on, so ADR 0023's "resolve ownership once per `BackupId` group"
optimization only ever needed "one job per group," not "one object per
group" — that stronger claim, as stated in PR #194's own description, is
disproven by this evidence and shouldn't be relied on elsewhere. It does
mean this design's original "one CSV row per `BackupId` group" grain would
have silently blended multiple machines' Fulls/Incrementals/sizes/dates
into one row for exactly this repository configuration — undermining the
per-object granularity the feature exists to provide.

Fixed grain: the new script resolves `JobName` / `OriginalJobType` /
`RepositoryId` / Tape exclusion (`IsTapeBackup`) **once per `BackupId`
group** (cheap, correct — these are shared across every `ObjectId` in the
group), then splits that group's restore points **by `ObjectId`** to
compute the actual stats row — `FullCount`/`IncrementalCount`/
`AvgFullSizeBytes`/`AvgIncrementalSizeBytes`/`TotalSizeBytes`/
`OldestRestorePoint`/`NewestRestorePoint`. The CSV's row grain is one row
per `(BackupId, ObjectId)` pair, reusing the once-resolved job-level fields
across however many `ObjectId` rows that group expands into. `BackupId` is
therefore *not* a unique key on its own — multiple rows can share one when
per-VM chains are disabled. The reverse direction still isn't guaranteed
either: a single `ObjectId` could in principle span more than one
`BackupId` over its lifetime (e.g. a job retarget creating a new Backup
object for the same machine, distinct from the rebuild case seen live,
which produced a new `ObjectId` instead) — that case simply produces
multiple rows for that `ObjectId`, same as before.

### CSV schema

One CSV, `_orphanedSupersededBackups.csv` — one row per `(BackupId, ObjectId)` pair:

| Column | Notes |
| --- | --- |
| `RepositoryId` | via `.GetBackup().GetParentOrThis().RepositoryId` |
| `JobName` | via `.GetBackup().GetParentOrThis().Name`/`.JobName`; always populated, even for Orphaned rows |
| `CurrentJobId` | zeroed GUID for Orphaned rows; a real, current Job Id for Superseded rows |
| `Category` | `Orphaned` \| `Superseded` — stored explicitly rather than re-derived downstream |
| `OriginalJobType` | `.TypeToString` — e.g. `Proxmox Backup`, `VMware Backup` (confirmed live values; excludes Tape, see above) |
| `ObjectId` / `BackupId` | both retained; `ObjectId` is the report-facing identifier and, with `BackupId`, forms the row's actual unique key — `BackupId` alone is not unique (see grain correction, above) |
| `ObjectName` | source VM/machine name |
| `FullCount` / `IncrementalCount` | restore point counts by `Type`, scoped to this row's `ObjectId` only |
| `AvgFullSizeBytes` / `AvgIncrementalSizeBytes` | separate averages — not one blended average |
| `TotalSizeBytes` | sum of all retained restore points for this `ObjectId` |
| `OldestRestorePoint` / `NewestRestorePoint` | `CreationTimeUtc` range |

A single CSV (not two per-category files) because the schemas overlap
almost entirely — only the job-context columns differ, and those are simply
blank/zeroed for Orphaned rows. One schema keeps the C# rollup logic (repo
→ job grouping) unified instead of duplicated across two shapes for what is
fundamentally one concept ("this data isn't part of any job's active
count") with two causes.

### C# pipeline

- CSV parsing via the existing generic dynamic path (`CCsvParser`) — no new
  strongly-typed reader class required, consistent with the NAS/Entra
  precedent.
- A new Aggregator (mirroring PR #194's `AgentJobAggregator.cs`) groups CSV
  rows first by `RepositoryId`, then by `JobName`, rolling up
  `FullCount`/`IncrementalCount`/`TotalSizeBytes` and min/max dates to the
  job level, while retaining the individual per-`(BackupId, ObjectId)` rows
  (object-level detail) for the expandable detail view.
- New report DataType classes for the job-level row and the nested
  object-level row.
- New HTML table renderer under
  `Functions/Reporting/Html/VBR/VbrTables/`, wired into `CHtmlTables.cs`,
  `CHtmlBodyHelper.cs`, and the nav link in `MakeNavTable()` — three manual
  touch points, matching how NAS/Entra are wired today (no registry/switch
  exists to add to).
- JSON: a new section via `SetSection("orphanedSupersededBackups", ...)`,
  hand-built headers/rows (JSON export is not automatically derived from
  the DataType in this codebase) — a nested per-object array sits inside
  each job-level entry.

### HTML report section

Grouped per-repository (matching the "in the spirit of NAS/Entra" framing
from issue #192), each repo group showing a summary line (count flagged,
approximate reclaimable size) above a job-level table:

`▸ | Job Name | Status (Orphaned/Superseded badge) | Original Job Type | Fulls | Incrementals | Total Size | Oldest RP | Newest RP`

Clicking a row expands an inline sub-row with the per-object breakdown:

`Object | ObjectId | Fulls | Incrementals | Avg Full Size | Avg Incremental Size | Total Size | Oldest | Newest`

The expand/collapse reuses the existing accordion pattern
(`toggleSection`/`.section-card.open` in `ReportScript.js`/`css.css`)
rather than introducing a new modal/overlay component — no such component
exists anywhere in this codebase today, and the accordion pattern already
has a working `@media print` force-open rule so PDF/PPTX exports render
expanded content correctly. That rule is extended to cover the new
sub-row class.

Job-level rows do **not** show Avg Size (a blended average across
potentially-different-sized objects isn't meaningful); object-level rows
show `AvgFullSizeBytes`/`AvgIncrementalSizeBytes` separately rather than
one blended figure, since full and incremental restore points differ in
size by nature.

## Error Handling

- Sweep absent or failed → new script's CSV output (or a companion
  sentinel) signals "not evaluated"; the report renders that explicitly per
  repo/environment rather than an empty-but-implying-clean section.
- Per-job `GetObjectsInJob()` or repository-resolution failure is caught,
  logged via `Add-VhciModuleError`, and skips just that job — it does not
  fail collection for the whole run, consistent with existing
  `Get-VhcJob.ps1` error-handling conventions.

## Testing and Validation Plan

- Pester tests for the new script, following `Get-VhcJob.Tests.ps1`'s
  existing patterns for Tier 1/Tier 2/suppression fixtures.
- xUnit tests for the new C# Aggregator/DataFormer/table-renderer classes.
- Golden-baseline schema updates for the new CSV
  (`Tools/GoldenBaselines/ObjectSchemas/`).
- **Multi-lab validation before the PR is raised** (owner: repo maintainer,
  not automated):
  - Confirm the accepted Category A gap (pure-safe-allowlist environments
    get no Orphaned detection) is acceptable in practice.
  - Confirm the zero-overlap safety guard actually neutralizes the known
    VMware Cloud Director Backup case (0 of N objects should match,
    triggering skip-not-flag) rather than assuming the fix works from the
    ADR 0021 evidence alone.
  - Test `IsTapeBackup` exclusion against tape copies of at least two
    different source platforms (already done for VMware and Proxmox during
    this design's investigation; extend to others as available) to build
    confidence the boolean, not `TypeToString`, is the stable signal.
  - Watch for other Backup-object flags that might need similar exclusion
    treatment to Tape — the live property dump surfaced many (`IsImported`,
    `IsExported`, `IsBackupCopy`, `IsSnapReplica`, etc.) that weren't
    individually evaluated for this design; if any produce the same
    "legitimately unresolvable by either tier, but not actually a cleanup
    candidate" shape that Tape did, they need the same kind of pre-
    classification exclusion, not folding into Orphaned.
  - More generally, confirm `GetObjectsInJob()`'s behavior on any other
    allowlisted or plugin-platform job type actually encountered in the
    test labs, now that the safety guard reduces (but doesn't eliminate)
    the cost of being wrong about a given type.
  - Confirm the added per-job `GetObjectsInJob()` call is negligible cost
    in a large, 100%-safe-allowlist environment — previously zero extra
    cost there, since the check didn't run at all before it was decoupled
    from `$NeedsSweep`. It's a per-job, not per-restore-point, call, so it
    should stay cheap under ADR 0022's cost model, but this feature's
    history of performance surprises (that's the whole reason ADR 0022
    exists) makes it worth confirming rather than assuming.
  - Confirm the `(BackupId, ObjectId)` grain against a multi-machine job on
    a per-VM-chains-disabled repository (confirmed live with a 3-VM Hyper-V
    job) — verify the new script actually produces one row per `ObjectId`
    rather than one blended row per `BackupId` in this configuration, and
    that the zero-overlap guard and Tier 1/2 retention logic behave
    correctly when a `BackupId` group's restore points span more than one
    `ObjectId`.

## Documentation

- `CONTEXT.md` already updated with **Superseded Restore Point** (alongside
  the existing **Orphaned Restore Point** entry) during this design
  session.
- Five ADRs written after the implementation plan, covering the decisions
  that clear the domain-modeling skill's bar (hard to reverse, surprising
  without context, a real trade-off), in the same vein as ADR
  0021/0022/0023:
  - [ADR 0024](../../adr/0024-superseded-backup-detection-two-mechanisms.md) — Superseded detection via two independent mechanisms + the zero-overlap safety guard.
  - [ADR 0025](../../adr/0025-orphaned-detection-bounded-by-sweep-gate.md) — Orphaned detection's dependency on the sweep, and the accepted coverage gap.
  - [ADR 0026](../../adr/0026-tape-exclusion-via-istapebackup.md) — Tape exclusion via `IsTapeBackup`, not `TypeToString`.
  - [ADR 0027](../../adr/0027-backupid-objectid-grain-correction.md) — the `(BackupId, ObjectId)` grain correction, which also corrects PR #194's own description.
  - [ADR 0028](../../adr/0028-nested-json-via-dedicated-property.md) — nested JSON via a dedicated `CFullReportJson` property, not `SetSection`/`HtmlSection`.

## Open Items

- The exact fallback UX/copy for "not evaluated for this environment"
  (per-repository vs whole-section messaging) — left to the implementation
  plan.
- PR #194's own description states `BackupId` is "confirmed, live, to be
  scoped to exactly one protected object's chain within one job" — this
  design's investigation disproves the "one object" half of that for
  repositories with per-VM chains disabled (see `CONTEXT.md`'s corrected
  "Backup" entry). The merged PR's text can't be edited, but worth
  flagging to the maintainer so nothing else in the codebase leans on the
  stronger claim.
- Whether other Backup-object flags beyond `IsTapeBackup` need the same
  pre-classification exclusion treatment (see Testing) — the property
  surface is large and this design only evaluated Tape in depth.
- Whether `GetObjectsInJob()`'s behavior generalizes cleanly to every
  plugin platform (HPE Morpheus, Nutanix, oVirt, Proxmox) beyond the one
  confirmed-bad case (Cloud Director) is still unconfirmed; the zero-overlap
  safety guard bounds the damage from being wrong, but multi-lab validation
  should still exercise it directly rather than relying on the guard alone.
  The two detection mechanisms stay separate regardless of what that
  validation finds — they catch genuinely different failure shapes (see
  Detection, above), not just "not yet confirmed to generalize."
- Multi-lab validation (see Testing) happens before, not after, the PR is
  raised — if it surfaces problems with the accepted Category A gap, the
  fallback is forcing the global sweep unconditionally and superseding ADR
  0022, which would be a materially larger change than this design assumes.
