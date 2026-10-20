# Orphaned & Superseded Backups: accuracy validation plan

Audience: Adam, Ben (comnam90). Purpose: prove the report is accurate (including that the #221 fix, PR #278, holds) before the `VHC_EXPERIMENTS` gate from PR #282 comes off.
Written 2026-10-19 against `origin/dev` @ `5de0d9fa`. Line numbers below are for that commit unless stated. Nothing in this plan has been run.

## 0. Status of this plan (read first)

- **Progress (2026-10-20).** The three no-lab risks in section 4.3 were tested and all confirmed (R1, R2, R3). They are fixed in #301, with tests that fail on `dev` and pass with the fix. Each fix still rests on assumptions about real VBR object shapes, which the scenarios below confirm. The OnDiskGB sizing fix (R5) is #300.
- **zbook (VBR 13.1 on PostgreSQL) has not been inventoried yet.** Everything about its capacity is labelled **[A]** (assumption). Appendix A is a read-only inventory to run first (`VHC_WINHOST` set to the host) that turns the [A] items into facts.
- **ADR 0025 is stale.** It says "no sweep-forcing code exists anywhere". `Get-VhcJob.ps1:84-109` now forces `$NeedsSweep = $true` (marked TEMPORARY, 2026-08-26). Un-gating should land an ADR that supersedes 0022/0025 or removes the override. Ben owns that call.
- **A sizing fix is open as draft PR #300:** `OnDiskGB` goes back to including removed-object (StaleObject) points via a new `$OnDiskPoints`, while `OriginalSize` still excludes them. The sizing checks in section 5 are written for both behaviours.
- Existing coverage is all mocks. The Pester tests (`Get-VhcJob.Tests.ps1`, `Get-VhcOrphanedSupersededBackups.Tests.ps1`) and xUnit tests (`OrphanedSupersededBackupAggregatorTests`, `COrphanedSupersededBackupsTableTests`, `OrphanedSupersededExperimentsGateTests`) never touch a real VBR. Live CI (`Test-VhcLiveRun.ps1`, `Scrub` check, lines 789-804) only asserts the section and JSON key are **present**. Nothing compares categories or bytes with what VBR says.

## 1. How the report classifies (what the tests must cover)

Collection, in `Get-VhcJob.ps1`:

| Step | What it does | Lines |
|---|---|---|
| Gate | `$NeedsSweep` from the allowlist, then forced `$true` | 81-109 |
| Sweep | one unscoped `Get-VBRRestorePoint`, grouped by `BackupId` | 126, 171-180 |
| Tier 1 | `GetSourceJob()` then `GetParentJob()`, must be in `$Jobs` | 185-218 |
| Tier 2 | parent backup `JobId` first (#270), else parent name == job name | 271-296 |
| Tier 3 | Backup Copy job `GetLastBackup()` by `BackupId` (#268) | 298-335 |
| Candidate `Unresolved` | no tier placed the group | 336-347 |
| Candidate `Tier2Suppressed` | placed by tier 2 on a job that already has a tier-1 chain | 348-357 |
| Candidate `StaleObject` | restore point `ObjectId` not in `GetObjectsInJob()`, only if at least one point matches (zero-overlap guard) | 494-575 |
| Sweep failure | falls back to per-job path, clears Unresolved/Tier2Suppressed, `SweepRan=false` | 385-411 |

Classification, in `Get-VhcOrphanedSupersededBackups.ps1`:

- Tape backups are skipped (`IsTapeBackup`, 183-191).
- Reason `Unresolved` becomes `Orphaned` only if the newest point of the **parent backup** is older than `ActiveChainWindowDays` (default 35, `VbrConfig.json` `Thresholds.OrphanedActiveChainWindowDays`). Otherwise `Unresolved`. Unusable or future timestamps count as active (253-261).
- Every other Reason becomes `Superseded`, with **no recency or liveness check at all**.
- Size is `GetStorage().Stats.BackupSize` per point, summed per `(BackupId, ObjectId)` (327-339).

C#: the aggregator groups by `(RepositoryId, JobName, CurrentJobId, Category)` (`OrphanedSupersededBackupAggregator.cs:51-54`). The table counts `Orphaned` + `Superseded` as "potentially reclaimable" and shows `Unresolved` separately (`COrphanedSupersededBackupsTable.cs:28-32, 194-204`).

## 2. Risks the tests must target (ranked)

| # | Risk | Where | Why it matters |
|---|---|---|---|
| R1 | `Superseded` has no liveness guard but is summed as "potentially reclaimable". #278 fixed only `Unresolved`-to-`Orphaned`. The #221 framing (live data presented as reclaimable) can still occur via `Superseded`. | `Get-VhcOrphanedSupersededBackups.ps1:253-261`; `COrphanedSupersededBackupsTable.cs:31-32,201-204` | Highest. Needs a scenario where VBR says "current" and VHC says Superseded (S9, S10). |
| R2 | `StaleObject` membership is the raw `GetObjectsInJob()`. Only `Vapp` containers are special-cased. A job with an explicit VM plus a container (folder, host, cluster, tag, agent group) gets `MatchedAny=true`, and every VM that lives only inside the container looks stale while it is being backed up daily. | `Get-VhcJob.ps1:457-467, 513-516, 529-551`; ADR 0024 "left uncovered without live evidence" | Direct false positive on active data. |
| R3 | #278's recency state is built only from `Unresolved` candidates. Sibling chains under the same parent that resolved to a job (the common #268 shape: VM chain unresolved, agent chain resolved) are never consulted, although the PR text says "nothing under its parent backup was written". | `Get-VhcOrphanedSupersededBackups.ps1:139-166` (line 143 `continue`) | The #221 guard is narrower than described. Cheap to unit-test, then confirm live. |
| R4 | `Tier2Suppressed` marks an active chain `Superseded` when tier 1 fails for it but the job has another tier-1 chain. #270 notes plug-in types may not populate parent `JobId`. | `Get-VhcJob.ps1:348-357`, `#270 "Not verified"` | Reachable on non-zbook job types. |
| R5 | `OnDiskGB` currently excludes StaleObject points (so job size looks smaller than the console). PR #300 reverses this. | `Get-VhcJob.ps1:579-585` (dev); PR #300 adds `$OnDiskPoints` | Reconciliation must pick the right formula (section 4). |
| R6 | Forced global sweep: cost unknown at scale, and a sweep failure silently becomes "not evaluated". | `Get-VhcJob.ps1:109, 385-411`; memory note: Jobs collector hit 264 s on a large estate before sweep existed | Perf gate (section 5). |
| R7 | Window of 35 days vs jobs that legitimately write less often (monthly or quarterly Backup Copy, paused job) combined with a discovery gap (#222/#224) gives `Orphaned` for a live chain. | `Get-VhcOrphanedSupersededBackups.ps1:257-258` | Cannot be proven in a week on zbook; needs the aged-chain set (S12) and the v12 lab. |
| R8 | A `GetStorage()` failure drops that point from the size silently (WARNING only). | `Get-VhcOrphanedSupersededBackups.ps1:62-70, 327-339` | Understated GB; check logs for the warning in every run. |
| R9 | HTML shows GB with `N0` rounding, so lab-scale (sub-GB) errors are invisible in the HTML. | `COrphanedSupersededBackupsTable.cs:34-35` | Compare against CSV/JSON bytes, and make lab chains at least a few GB where the HTML is checked. |
| R10 | Zero-overlap guard false negative when a job's whole membership is swapped (accepted in ADR 0024). | `Get-VhcJob.ps1:521-534` | Document, measure, do not block on it. |

## 3. Scenario matrix

Conventions:
- **Window** = `ActiveChainWindowDays`. Lab chains are days old, so with the default 35 nothing can become `Orphaned`. Run every scenario at **W=35 (default)** and **W=0** (edit `VbrConfig.json` in the scratch tree; at 0 a point from an hour ago is `Orphaned`, see Pester line 366). A real aged set (S12) covers the true 35-day case from day 36.
- "Run" = one job execution (tiny sources, 1-3 min each). A chain needs one full + two incrementals minimum, so **3 runs**. A second chain from the same job needs `Start-VBRJob -FullBackup` or an active full (1 run).
- **Truth** = the oracle in section 4 (SDK facts) cross-checked with the staging ledger (what we did on purpose).
- "No row" means the `(BackupId, ObjectId)` must not appear in `_orphanedSupersededBackups.csv`.
- Zbook column: Y = stageable, H = needs Hyper-V or another hypervisor [A], A = needs an agent source (zbook can back itself up with a file-level agent job [A]), N = not on zbook.

| ID | Scenario | Stage (short) | Truth from VBR | Expected VHC | Zbook | Targets |
|---|---|---|---|---|---|---|
| S0 | Healthy baseline: 2 jobs, 2-3 objects each | Create, run 3x | Job owns backup; all objects current; newest today | **No rows.** `OnDiskGB` = file bytes. Meta `SweepRan=True`. | Y | false positives, sizing |
| S1 | Deleted job, backups retained, newest point recent (the #221 shape) | S0 job B, then `Remove-VBRJob`; backup stays as orphaned | `JobId` names no job; newest < W | W=35: **Unresolved**; W=0: **Orphaned**. Bytes counted as reclaimable only in the Orphaned case. | Y | #221, R7 |
| S2 | Deleted job, newest point older than 35 days | Seed on day 0, wait to day 36 (S12) | same, newest > 35 d | **Orphaned**, size = file bytes | Y (slow) | true Orphaned |
| S3 | Unresolvable job, but **actively written** (the literal #221 symptom) | Needs a writer VHC cannot see. v12 lab (tier C job gap), or fault-injection: run `Get-VBRConfig.ps1` standalone with a `Get-VBRJob` wrapper that hides job X while X keeps running | Job X alive, newest today | **Unresolved, never Orphaned**, at both W values | Y (inject) / vbr-12 | #221 core, R3 |
| S4 | Parent with mixed children: one chain unresolved (old, stopped), sibling chain resolved and active | Backup Copy of 2 sources, hide or drop one; or Pester-only for the resolver gap | Parent has a point from today | Unresolved (parent is live). Fails today if R3 is real. | Y/H | R3 |
| S5 | `Superseded` via StaleObject: VM removed from job | S0 job with 3 VMs, run, `Remove-VBRJobObject` VM3, run 2 more | VM3 not in `Get-VBRJobObject`; chain stops | VM3 chain: **Superseded**, `CurrentJobId` = the job. Other VMs: no row. Zero-overlap guard not triggered. | H (or A with 3 file sets) | StaleObject |
| S5b | Same, with "Remove deleted VMs data after N days" set | Job option `RetainDays`; remove VM | VBR will purge at removal + N days | **Superseded** (label only). Record the nuance: VBR cleans this itself. Ben decides whether it counts as reclaimable. | H | semantics |
| S6 | Superseded via Tier2Suppressed | Job with two chains where tier 1 resolves one and the other only by name/parent Id (reproduce with the #268 shape) | Both chains belong to the job | **Superseded** (Tier2Suppressed) **only if** VBR also says the chain is not current. If VBR says current, this is a false positive (R4). | Y/H | R4 |
| S7 | Active-chain window edge | Seed S1 backup, then edit `ActiveChainWindowDays` to 0, 1, 2 around the known age | newest age known | Flips Unresolved to Orphaned exactly at the edge; future-dated stays Unresolved | Y | window math |
| S8 | Renamed Backup Copy (#268/#270) | Backup Copy job from S0 job to a second repo, run 3x, **rename the copy job**, run 1x more | Copy job alive, last backup lists the chain; backup keeps the old name | **No row** (tier 2 by parent `JobId`, tier 3 by `GetLastBackup`). Job `OnDiskGB` = copy repo bytes. Mutation check: on a pre-#271 build this must produce `Unresolved`/`Orphaned`. | Y/H | #268/#270 |
| S9 | VBR says current, VHC flags it (container membership) | Hyper-V job with VM1 explicit + the host or a folder as a container holding VM2 and VM3; run 3x | VM2/VM3 written today | **No row.** Any Superseded for VM2/VM3 = R2 confirmed. | H [A] | R2 |
| S10 | VBR says current, VHC flags it (agent group or policy) | Agent policy job (protection group or per-computer) with machines added/removed | all current machines written today | No row for current machines. Removed machine = Superseded. | A | R2 |
| S11 | VM moved between jobs | VM X in J1 (3 runs), remove from J1, add to J2, run J1 and J2 | X's old chain stale in J1; new chain active in J2 | J1: X old chain **Superseded**. J2: no row. Same `ObjectId` in two jobs: each chain attributed once, no double count. | H | (BackupId, ObjectId) grain, ADR 0021 duplicates |
| S12 | Aged set (time bomb) | Day 0: create S1/S2-style deleted-job backups and a paused job; leave alone | at day 36 newest > 35 d | Day 36: **Orphaned** at default W. Paused job still alive: **no row**. | Y | real window |
| S13 | Repo retarget, prior chain | Change J's target repo, run 2x | New backup in new repo; old backup still shows under J (inactive chain) | **Probe first.** Old chain probably resolves by tier 1 to J (same `JobId`), is not stale (same objects), so it is **not listed** and sits inside `OnDiskGB`. Record actual, then decide: documented gap or a new "Inactive chain" category. | Y | prior-chain gap |
| S14 | Backup Copy with agent-sourced chain (worker job) | Copy job whose source includes an agent backup | Worker job `SimpleBackupCopyParentWorker` owns the agent chain | No row. Parent `OriginalSize` includes worker; on-disk rolled up in the report (not CSV). No double counting. | A | #268 worker |
| S15 | Per-VM chains off vs on | Two repos, same job type | `BackupId` shared vs one per VM | Same classification either way; row count differs, bytes equal | Y/H | grain |
| S16 | vApp (VMware Cloud Director) | n/a | container point ApproxSize = sum of children | container excluded, no Superseded for children | **N** (usdemo or Cloud Director lab) | zero-overlap guard |
| S17 | Tape backup excluded | n/a | `IsTapeBackup` | no row | **N** (needs tape or VTL) | skip |
| S18 | Sweep failure / "not evaluated" | Fault-inject `Get-VBRRestorePoint` throwing | n/a | `SweepRan=False`, Unresolved/Tier2Suppressed cleared, banner shown, StaleObject rows still listed | Y (inject) | R6, banner |
| S19 | Undated or placeholder timestamps | Pester only (cannot create in VBR) | n/a | Unresolved, not dropped | Pester | fail-safe |

Cross-cutting assertions applied to every scenario:
- **A1 (hard):** a `(BackupId, ObjectId)` whose newest point is within W of collection is never `Orphaned`.
- **A2 (hard):** a chain written by a live job's current run is never `Orphaned` and never `Superseded`.
- **A3:** every restore point lands in exactly one bucket (conservation, section 4).
- **A4:** the HTML, JSON and CSV agree on row counts, categories and bytes.
- **A5:** scrubbed output leaks no real name or host.

### Staging recipes (run by Adam; these change the lab)

Create two folder repos on a data disk: `D:\VhcLab\RepoA`, `D:\VhcLab\RepoB` (RepoB is the Backup Copy target). Keep sources tiny. Syntax below is an outline; confirm cmdlet parameters on 13.1 with `Get-Help`.

- **Sources.** Preferred: 3 small Hyper-V VMs of 1-2 GB each [A] (Alpine or Windows Server Core). Fallback: a managed-by-server file-level agent job on zbook itself over three small folders (100-300 MB each). Both give per-object chains in seconds.
- **Run count.** `Start-VBRJob -Job $j` three times per scenario, then `-FullBackup` once if a second chain is needed. Allow ~10 minutes per scenario once sources exist. Total for S0-S11: roughly one working day of clicking, mostly waiting.
- **Delete a job (S1/S2/S12).** Console: Home > Jobs > right-click > Delete. The backup remains under Disk (Orphaned). PowerShell: `Remove-VBRJob -Job $j -Confirm:$false`. Never choose "Delete from disk".
- **Remove a VM (S5).** Console: Edit job > Virtual Machines > Remove. PowerShell: `Get-VBRJobObject -Job $j -Name VM3 | Remove-VBRJobObject`.
- **Move a VM (S11).** Remove from J1, `Add-VBRJobObject` to J2, run both.
- **Rename Backup Copy (S8).** Console: right-click the copy job > Edit > Name. The backup keeps its original name; that mismatch is the test.
- **Retarget (S13).** Edit job > Storage > repository. Accept the prompt that offers to keep or map the old chain.
- **Container job (S9).** Add the Hyper-V host (or a vSphere folder or tag) as one job object, plus one VM explicitly.
- **Fault injection (S3, S18).** Run `Get-VBRConfig.ps1 -VBRServer localhost -VBRVersion 13` from a pwsh session that first defines a proxy `function Get-VBRJob { ... | Where-Object Name -ne 'X' }` (or one that throws for `Get-VBRRestorePoint`). The script and module are plain PowerShell, so a function in the caller's scope shadows the cmdlet. Confirm this works with the first run; if the C# host is needed, mark S3/S18 as v12-lab-only.

## 4. Ground-truth oracle and automation

### 4.1 Oracle script (`Get-VhcOrphanedOracle.ps1`, new, under `.claude/skills/verify-vhc/scripts/`)

Rule: **do not call the code under test**. No `GetSourceJob`, `GetParentJob`, tiered matching, or `Get-VhcJob`. Use different SDK routes, and cross-check them with a hand-written staging ledger.

Inputs: `-Window 35`, `-Ledger ledger.json` (per scenario: backup name, expected category per object, expected active flag), `-RepoRoot`-style repo paths from `Get-VBRBackupRepository`.

Outline (read-only; connects with Windows auth like VHC does on the VBR host):

```powershell
# 1. inventory (all read-only)
$now      = [datetime]::UtcNow
$jobs     = @(Get-VBRJob) + @(Get-VBRComputerBackupJob) + @(Get-VBRTapeJob -ErrorAction SilentlyContinue)
$jobById  = @{}; $jobs | ForEach-Object { $jobById[$_.Id.ToString()] = $_ }
$backups  = Get-VBRBackup                        # plus child backups via $b.GetChildren() where supported
# 2. per backup: owner by JobId, then by Backup Copy last backup (job.FindLastBackup()), never by name alone
# 3. membership: Get-VBRJobObject -Job $j  (different route from GetObjectsInJob) -> ObjectId set
#    containers (host, folder, tag): expand with Find-VBRHvEntity / Find-VBRViEntity; if not expandable,
#    mark the job 'membership-unknown' so the oracle emits NO expectation for it instead of guessing
# 4. per backup, per restore point (Get-VBRRestorePoint -Backup $b: scoped, NOT the global sweep):
#       Newest, Type, Stats.BackupSize, ObjectId, BackupId, GetStorage().FilePath
# 5. file truth: for local repos, Get-ChildItem *.vbk,*.vib,*.vrb under the backup folder; sum Length;
#    exclude *.vbm (metadata). Compare to sum(Stats.BackupSize).
# 6. expected class per (BackupId, ObjectId):
#       owner dead  : newest within W  -> Unresolved ; older -> Orphaned
#       owner alive : ObjectId in membership AND backup is the job's current backup -> Active (no row)
#                     ObjectId not in membership                                     -> Superseded
#                     backup is a prior (non-last) backup of the job                 -> PriorChain (own bucket, see S13)
#    A chain written within W by a live job is Active regardless of anything else (A2).
# 7. emit oracle.json: rows at the same grain as the CSV, plus per-job file bytes, per-backup file bytes
```

Mapping oracle to VHC: `Active` expects no row; `Unresolved`/`Orphaned`/`Superseded` map one to one; `PriorChain` has no VHC equivalent today, so it is reported as a **known gap**, not a failure, until Ben decides.

### 4.2 Automated comparison

Add a new check rather than overloading existing ones:

- `Test-VhcLiveRun.ps1 -Check OrphanedAccuracy -OracleJson <path> -CsvRoot <raw csv dir> -ReportPath <html> -ReportDir <json dir> [-WindowDays N] [-OnDiskIncludesStale]`. Add `'OrphanedAccuracy'` to the `ValidateSet` (line 27). Leave it **out of the default plan** so CI runs are unchanged (the script's rule that a missing input is a FAILURE still holds: the check fails if `-OracleJson` is missing when requested).
- Wire it into `winhost` as `--live --orphaned`:
  1. `vhc-verify.sh winhost` passes a new `-Orphaned` switch to `Invoke-WinHostVerify.ps1`.
  2. `Invoke-LiveStep` (lines 214-275) already sets `VHC_EXPERIMENTS=1` and has raw CSVs in `$out\Original\VBR`. Before cleanup, it runs the oracle, then `-Check OrphanedAccuracy`.
  3. For the HTML name match, do the accuracy run **unscrubbed** (`/scrub:false`) into the scratch folder; keep the existing scrubbed run for the leak check. Only a GUID-keyed diff table is copied back to `.verify-vhc/evidence/`, never names. The scratch folder is deleted as usual.
  4. Optional `--window N` copies a patched `VbrConfig.json` into the scratch tree for the W=0 pass.
- What the check asserts:
  - CSV vs oracle, joined on `(BackupId, ObjectId)`: category, `FullCount`, `IncrementalCount`, `TotalSizeBytes` (tolerance in section 6), newest timestamp.
  - A1 and A2 as hard failures; false negatives against `Active`-complement rows reported as gaps unless the ledger marks them required.
  - JSON `OrphanedSupersededBackups` equals the CSV rolled up by `(RepositoryId, JobName, CurrentJobId, Category)`; `OrphanedBackupsSweepEvaluated` equals meta `SweepRan`.
  - HTML: category badge counts and the "N still being written (~Y GB), not counted" and "~X GB potentially reclaimable" strings equal the oracle sums **after `N0` rounding** (R9). If a lab chain is under 1 GB the HTML sizes are not asserted, only the counts.
  - Log: no `Could not read backup size` warnings (R8) and the sweep line is present.
- Output: a table per scenario (pass, fail, known-gap), saved as `orphaned-accuracy.md` in the evidence folder.

### 4.3 Cheap tests that need no lab (do these first)

- **Pester, R3:** two Unresolved candidates under parent P plus a resolved sibling with a fresh point; assert the Unresolved ones are not `Orphaned`. This should fail today if R3 is real.
- **Pester, R1:** a `Tier2Suppressed` and a `StaleObject` candidate with a point one hour old; document the intended label, then test it.
- **Pester, R2:** `GetObjectsInJob()` mock returning one VM plus a container-typed entry; assert the VMs under the container are not stale.
- **xUnit:** a `Superseded` row whose newest point is minutes old is not summed as reclaimable (or is rendered with a warning), whichever Ben decides.
- Property test: random category and age mix; `Orphaned` never appears with newest within W.

## 5. Sizing reconciliation

Per job `J` (use the oracle's file bytes as truth, local repo only):

- Define `F(J)` = bytes of `.vbk/.vib/.vrb` in all of J's backup folders; `S(J)` = Σ `Stats.BackupSize` of all J's restore points.
- Check 1: `S(J)` vs `F(J)`. Tolerance 1% (metadata and rounding). On ReFS or XFS with block clone, `F` is logical size and still matches `Stats.BackupSize`; physical disk use is lower and is not what the report claims. Note the repo type in the evidence.
- Check 2, **current dev** (before the in-flight fix): `F(J) = OnDiskGB(J) + StaleObject GB(J) + Tier2Suppressed GB(J)`. The console size for a job that lost a VM will exceed `OnDiskGB`; that gap is the R5 bug.
- Check 3, **after the fix**: `F(J) = OnDiskGB(J) + Tier2Suppressed GB(J)`, with StaleObject GB reported as "of which" (already inside `OnDiskGB`). `OriginalSize` must **exclude** removed objects: compare with Σ latest `ApproxSize` per **current** object. The test fails if the report ever adds `OnDiskGB` and StaleObject GB together.
- Check 4, **conservation (A3)** for the whole server: Σ over the global `Get-VBRRestorePoint` of `BackupSize` = Σ `OnDiskGB` (all jobs) + Tier2Suppressed + Unresolved + Orphaned + tape (excluded) + unattributed (vApp container). Tolerance 0.5%. A point counted twice or in no bucket shows up here.
- Check 5, Backup Copy (S8, S14): parent `OnDiskGB` + worker `OnDiskGB` (rolled up in C#, so assert on the HTML or JSON job table) = copy repo bytes.
- Check 6: the report's row `TotalSizeBytes` per `(BackupId, ObjectId)` vs file bytes of that object's chain. Per-VM chains make this exact; with shared chains compare at `BackupId` level.
- Aggregator arithmetic is already unit-tested; the live check proves the inputs.

## 6. Performance

What to measure on zbook and on each CI lab:

1. The existing log line `Restore point sweep: Nms elapsed, P restore points, J jobs, natural-gate=…, effective-gate=…` (`Get-VhcJob.ps1:383`) and the preceding `BackupId grouping: P restore points reduced to G groups` line. Both end up in the collector log, so CI runs give free data points.
2. Isolated timings in a pwsh session on the VBR host (read-only):
   - `Measure-Command { Get-VBRRestorePoint }` (the fetch, scales with P).
   - `Measure-Command { $groupRepresentatives | % { try { $_.GetSourceJob() } catch {} } }` (ADR 0022 measured ~9 ms/call).
   - tier 2 cost for unresolved groups (`GetBackup().GetParentOrThis()`), and tier 3 (`GetLastBackup()` + scoped `Get-VBRRestorePoint -Backup` per Backup Copy job).
   - Peak `WorkingSet64` of the pwsh process (the sweep holds every restore point object).
3. A/B on zbook: run VHC once as shipped (forced sweep) and once with line 109 commented out in the scratch tree (natural gate). On an all-allowlist zbook the delta is exactly what the override costs.

Model for extrapolation: `T ≈ a + b·P + c·G + d·U + e·C`, where P = restore points, G = BackupId groups, U = unresolved groups, C = Backup Copy jobs. Fit it with data points from zbook, vbr-2, vbr-13-sql and vbr-12 (collector logs from CI, no extra runs needed), then project to 10k, 50k and 500k points. Because grouping reduced `GetSourceJob()` calls from P to G, expect fetch time and memory to dominate at large P. Report: projected seconds and GB RAM for 50k and 500k points, and the share of the whole Jobs collector.

Acceptance (proposed, Ben to confirm): the sweep adds no more than 60 s per 10k restore points projected, memory stays under 2 GB at 50k points, and the Jobs collector stays well inside the collection timeout (the pre-3.0.1.110 5-minute timeout history is in memory). If it fails, the fallback is not to revert silently: restrict the sweep to environments with more than one backup chain per job, or cache (the override comment's own options).

## 7. zbook feasibility

Known: HP ZBook, Windows, VBR 13.1 on PostgreSQL, .NET 10 SDK, SSH, driven by `vhc-verify.sh winhost --live` (profile `vbr-13-pg`).

Assumptions to confirm with Appendix A [A]: free disk of at least 100 GB on a non-system volume; Hyper-V enabled (otherwise agent-only); existing jobs and repos are lab-safe to leave alone (the plan creates new `VhcLab-*` jobs and `D:\VhcLab` repos only).

| Can stage | Cannot stage on zbook |
|---|---|
| S0, S1, S3 (injected), S5-S8, S11, S13, S15, S18, S19 with Hyper-V or file-level agent sources | S16 vApp / Cloud Director |
| S10, S14 with a managed agent job on zbook itself | S17 tape; plug-in platforms (Proxmox, Nutanix, oVirt, Morpheus) for R4 |
| S2/S12 true aged Orphaned, after 36 days of waiting | Large-estate performance; v12 behaviours (PS 5.1, tier C discovery); SQL Server config DB; remote collection |
| Perf baseline at tiny scale (tens of points) | Object-storage or block-clone repo behaviour |

Needs the CI labs or other hosts:
- **vbr-12 (PostgreSQL, PS 5.1):** the only place the #222 job-discovery gap exists, so the realistic "unresolvable but active" chain (S3) and R7 live here. Check its collector log for tier B/C merges.
- **vbr-2 (usdemo, remote):** widest job-type mix [A]; Cloud Director (S16), Backup Copy, plug-in types (R4), real restore point volume for the perf fit. Read-only runs only.
- **vbr-13-sql (MsSql):** same scenarios as zbook on the other database engine; plus the A/B perf point.

How to use the CI labs without staging: add `-Check OrphanedAccuracy` to the `ci-cd.yaml` live jobs in a report-only mode (it prints diffs but does not fail) so every CI run is one more oracle comparison on real data. No lab changes needed. Make it blocking only after it has been stable for several runs.

## 8. Un-gate criteria

All must hold, evidence attached to a tracking issue:

| # | Criterion | Pass |
|---|---|---|
| U1 | False `Orphaned` on any active chain (A1/A2), all scenarios, W=35 and W=0 | **0**, in 3 consecutive runs on zbook plus each CI lab |
| U2 | False `Superseded` on a chain VBR says is current (S9, S10, S6) | 0, or R2/R4 fixed or explicitly documented and suppressed in the UI |
| U3 | Category agreement on staged rows | 100%; known gaps (e.g. PriorChain, S13) listed and accepted by Ben |
| U4 | Row `TotalSizeBytes` vs file bytes | within 1% (or 64 MB, whichever is larger) |
| U5 | Per-job reconciliation (Check 2/3) and conservation (Check 4) | within 1% and 0.5% |
| U6 | No missing rows for `Orphaned`/`Superseded` truth rows | 0 unexplained |
| U7 | HTML/JSON/CSV agreement and scrub leak check | all equal; no leak |
| U8 | Perf (section 6) | within agreed budget; forced-sweep override resolved by ADR |
| U9 | New Pester/xUnit tests for R1-R3 exist and fail against the pre-fix code | yes (RED to GREEN shown) |
| U10 | Aged-chain check (S12) at day 36 | true `Orphaned`, size exact |
| U11 | **Rename (Adam, 2026-10-20):** the section comes back as "Orphaned Backups & Decommissioned Workloads". Every row label must be literally true for every row it is applied to. The agreed labels are **Orphaned** (job deleted, nothing under its backup written within the window), **Decommissioned** (removed from its job and not in any other job), **Moved** (now protected by a different job; this is the old chain), **Previous chain** (an older chain of a live job), and **Still being written** (any point inside the window; never counted as reclaimable). "Moved" needs collection to know every job's current objects. The JSON key stays stable for consumers; only display text changes, including fr/ja/zh. | Title, nav, row labels and help text reviewed against S5, S6, S11 evidence |

Adam's bar for un-gating is **no known bugs and accurate reporting**: every U-criterion passes, and any R1-R4 finding is fixed, not just documented.

Sign-off: Adam executes and owns the evidence. **Ben signs off** the criteria, the ADR superseding 0022/0025, and the merge (repo rule: a human approves every merge). Suggested order:
1. Pester/xUnit for R1-R3, fix what fails (no lab).
2. Land the in-flight OnDiskGB fix.
3. Oracle script plus `OrphanedAccuracy` check, with `winhost --live --orphaned`.
4. Stage S0-S11 on zbook; run at W=35 and W=0; fix or document findings.
5. Start S12 (day 0) early; evaluate at day 36.
6. Report-only check on CI labs; use their logs for the perf fit.
7. Perf decision and ADR (Ben).
8. Review the evidence against U1-U10; Ben approves.
9. PR removing the gate (`CHtmlBodyHelper.OrphanedSupersededBackupsTable`, `CHtmlCompiler.BuildSidebar`, `docs/contributing.md` "Currently gated", verify-vhc `env -u VHC_EXPERIMENTS` assertions, ChangeLog). Tests in `OrphanedSupersededExperimentsGateTests` get inverted.

## 9. Effort and first three steps

Estimate (working time, one person with an assistant): Pester/xUnit and fixes 1-2 days; oracle + check + winhost wiring 2 days; staging and runs on zbook 1-1.5 days; CI lab report-only wiring and perf fit 1 day; fixes from findings 1-3 days (R1/R2 are the likely large ones); sign-off and gate PR 0.5 day. **About 7-10 working days**, plus a **36-calendar-day wait** for S12 that starts the day the seed is created. The gate can come off earlier than day 36 only if Ben accepts W=0 emulation for the aged case; S12 stays on the post-ungate watch list.

First three steps for today:
1. **Inventory zbook (15 min).** `export VHC_WINHOST=zbook` then run Appendix A over SSH. It is read-only. This turns every [A] above into a fact and decides Hyper-V vs agent sources.
2. **Baseline run with the raw CSV kept (20 min).** `VHC_WINHOST_KEEP=1 .claude/skills/verify-vhc/scripts/vhc-verify.sh winhost --live --no-tests`. Keep the scratch folder to read the raw `_orphanedSupersededBackups.csv`, the sweep timing line, and the meta `SweepRan`. Delete the scratch folder by hand afterwards (it holds unscrubbed data).
3. **Start the clock and the cheap tests.** Create `D:\VhcLab` repos and the S1/S2/S12 seed (two tiny jobs, 3 runs each, then delete one job and pause the other). That begins the 36-day wait. Meanwhile write the three Pester tests (R3, R1, R2) from section 4.3; each is under an hour and R3 may already be a confirmed bug.

## Appendix A. Read-only zbook inventory

Run as one SSH call. Creates, changes, starts and deletes nothing. It does not read the REST credentials file.

```powershell
ssh -o BatchMode=yes -o ConnectTimeout=15 "$VHC_WINHOST" @'
Add-PSSnapin VeeamPSSnapin -ErrorAction SilentlyContinue; Import-Module Veeam.Backup.PowerShell -ErrorAction SilentlyContinue
$ErrorActionPreference = 'Continue'
"--- host";   Get-CimInstance Win32_OperatingSystem | Select Caption, Version, FreePhysicalMemory
"--- cpu/ram"; Get-CimInstance Win32_ComputerSystem | Select Model, NumberOfLogicalProcessors, TotalPhysicalMemory
"--- disks";  Get-Volume | ? DriveLetter | Select DriveLetter, FileSystem, @{n='FreeGB';e={[int]($_.SizeRemaining/1GB)}}, @{n='SizeGB';e={[int]($_.Size/1GB)}}
"--- hyper-v"; (Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue).State
Get-VM -ErrorAction SilentlyContinue | Select Name, State, MemoryAssigned, @{n='DiskGB';e={[int](($_.HardDrives|%{(Get-VHD $_.Path -ErrorAction SilentlyContinue).Size})|measure -Sum).Sum/1GB}}
"--- vbr";    Get-VBRServerSession | Select Server; (Get-Module Veeam.Backup.PowerShell).Version
"--- repos";  Get-VBRBackupRepository | Select Name, Type, Path, @{n='FreeGB';e={[int]($_.GetContainer().CachedFreeSpace.InGigabytes)}}
"--- jobs";   Get-VBRJob | Select Name, JobType, TypeToString, IsScheduleEnabled
"--- agent jobs"; Get-VBRComputerBackupJob | Select Name, Mode, BackupType
"--- backups"; Get-VBRBackup | Select Name, JobType, TypeToString, @{n='Points';e={@(Get-VBRRestorePoint -Backup $_).Count}}
"--- totals";  $all = @(Get-VBRRestorePoint); "restore points: $($all.Count)"; "objects: $(($all.ObjectId | sort -Unique).Count)"
"--- sweep cost"; (Measure-Command { $null = Get-VBRRestorePoint }).TotalMilliseconds
"--- config db"; (Get-VBRInstalledLicense -ErrorAction SilentlyContinue | Select Edition)
'@
```

Notes: member names such as `GetContainer().CachedFreeSpace` vary by version, so treat failures as "unknown", not as findings. `Measure-Command { Get-VBRRestorePoint }` is read-only but loads every point; fine at lab scale.

## Appendix B. Sources read

Issue #221; PRs #198, #278, #282 (and #271 for the #270 fix); issues #268 and #270; ADRs 0021, 0022, 0024, 0025; `Get-VhcJob.ps1`, `Get-VhcOrphanedSupersededBackups.ps1`, `OrphanedSupersededBackupAggregator.cs`, `COrphanedSupersededBackupsTable.cs`; the Pester and xUnit test lists for each; `Test-VhcLiveRun.ps1`, `Invoke-WinHostVerify.ps1`, `features/windows-host.md`, `live-csv-expectations.json`.
