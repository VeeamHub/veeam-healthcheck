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

### Detection: two categories, two independent mechanisms

Classification is `CurrentJobId == Guid.Empty` (or not found in `$Jobs`) →
**Orphaned**; resolves to a real, current Job → **Superseded**. Both
`JobName`, `OriginalJobType` (`.TypeToString`), and `RepositoryId` come from
the same call — `.GetBackup().GetParentOrThis()` — for both categories, so
grouping is uniform regardless of which mechanism flagged the row. Confirmed
live: `.GetBackup().GetParentOrThis()` on a restore point whose owning job
was deleted still returns a valid object with `JobName`, `TypeToString`, and
`RepositoryId` populated, `JobId` zeroed.

Two independent detection paths feed the same classification, deliberately
*not* unified into one mechanism (unifying would require confirming
`GetObjectsInJob()` behaves reliably across plugin platforms like HPE
Morpheus/Nutanix/Proxmox — the same APIs that throw for other calls on
those types — which isn't worth blocking this feature on):

- **Swept job types** (ADR 0022 non-allowlisted): reuse the existing Tier
  1/Tier 2 grouping. Tier 2-*suppressed* groups — today discarded via a bare
  `continue` in `Get-VhcJob.ps1`'s matching loop with nothing retained — get
  retained instead, tagged Superseded.
- **Non-swept ("safe" allowlist) job types**: a new per-job
  `$Job.GetObjectsInJob()` vs restore-point-`ObjectId` cross-reference.
  Cheap and local — `GetObjectsInJob()` and the existing
  `Get-VBRRestorePoint -Backup $Job.GetLastBackup()` call are both scoped to
  one job already, not the expensive global sweep ADR 0022 exists to avoid.
  Any restore point whose `ObjectId` isn't in current membership is tagged
  Superseded.

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
for non-swept job types also fixes `Get-VhcJob.ps1`'s existing
`CalculatedOriginalSize`/`TotalOnDiskGB` computation: restore points for an
`ObjectId` no longer in current job membership are excluded from those sums
instead of being silently included. Ship in the same branch as #192,
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
2. For non-swept job types, the new `GetObjectsInJob()` cross-reference
   (above) runs per-job and writes excluded restore points into the same
   cache shape, tagged Superseded.
3. If the sweep didn't run at all (pure-safe-allowlist environment) or
   failed, the cache is absent/empty and carries an explicit marker so the
   new script (and downstream report) can distinguish "not evaluated" from
   "evaluated, found nothing."

The new script reads the cache, resolves `JobName` / `OriginalJobType` /
`RepositoryId` via `.GetBackup().GetParentOrThis()` for each group, and
writes one CSV row per `BackupId` group — a `BackupId` is confirmed (ADR
0023, live evidence) to be scoped to exactly one `ObjectId`'s chain within
one job, so in practice each row carries one object's data. The reverse
isn't guaranteed: a single `ObjectId` could in principle span more than one
`BackupId` over its lifetime (e.g. a job retarget creating a new Backup
object for the same machine, distinct from the rebuild case seen live,
which produced a new `ObjectId` instead). If that happens, it simply
produces multiple rows for that `ObjectId`, which the report shows as
separate object-level sub-rows rather than a false merge.

### CSV schema

One CSV, `_orphanedSupersededBackups.csv` — one row per `BackupId` group:

| Column | Notes |
| --- | --- |
| `RepositoryId` | via `.GetBackup().GetParentOrThis().RepositoryId` |
| `JobName` | via `.GetBackup().GetParentOrThis().Name`/`.JobName`; always populated, even for Orphaned rows |
| `CurrentJobId` | zeroed GUID for Orphaned rows; a real, current Job Id for Superseded rows |
| `Category` | `Orphaned` \| `Superseded` — stored explicitly rather than re-derived downstream |
| `OriginalJobType` | `.TypeToString` — e.g. `Proxmox VE`, `VMware Backup` |
| `ObjectId` / `BackupId` | both retained for correlation/troubleshooting; `ObjectId` is the report-facing identifier |
| `ObjectName` | source VM/machine name |
| `FullCount` / `IncrementalCount` | restore point counts by `Type` |
| `AvgFullSizeBytes` / `AvgIncrementalSizeBytes` | separate averages — not one blended average |
| `TotalSizeBytes` | sum of all retained restore points for this object |
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
  job level, while retaining the individual per-`BackupId`-group rows
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
  not automated): confirm the accepted Category A gap is acceptable in
  practice, and confirm the `GetObjectsInJob()` cross-reference doesn't
  misclassify real-world edge cases — in particular, folder- or tag-based
  dynamic job membership, where `GetObjectsInJob()` may return a container
  object rather than per-VM entries and could produce false positives if
  not accounted for.

## Documentation

- `CONTEXT.md` already updated with **Superseded Restore Point** (alongside
  the existing **Orphaned Restore Point** entry) during this design
  session.
- A new ADR is expected during the implementation-planning phase, covering
  the `GetObjectsInJob()`-based Superseded detection and
  `GetParentOrThis()`-based repository/job-type resolution — both are
  non-obvious, hard-to-reverse decisions with real trade-offs (per the
  domain-modeling skill's ADR criteria), in the same vein as ADR
  0021/0022/0023.

## Open Items

- The exact fallback UX/copy for "not evaluated for this environment"
  (per-repository vs whole-section messaging) — left to the implementation
  plan.
- Whether `GetObjectsInJob()`'s behavior generalizes cleanly to plugin
  platforms (HPE Morpheus, Nutanix, oVirt, Proxmox) is unconfirmed; shipping
  as two separate mechanisms (this design) avoids blocking on that
  confirmation, but unifying them remains a future simplification if it
  turns out to generalize.
- Multi-lab validation (see Testing) happens before, not after, the PR is
  raised — if it surfaces problems with the accepted Category A gap, the
  fallback is forcing the global sweep unconditionally and superseding ADR
  0022, which would be a materially larger change than this design assumes.
