# ADR 0034: Orphaned & Superseded Backups: Membership and Recency Rules

* **Status:** Proposed (the report is experimental, behind `VHC_EXPERIMENTS`; Ben to accept)
* **Date:** 2026-10-19
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code
* **Amends:** [ADR 0024](0024-superseded-backup-detection-two-mechanisms.md) (stale-ObjectId membership check), and the #278 active-chain window from [ADR 0025](0025-orphaned-detection-bounded-by-sweep-gate.md)'s classifier

## Context and Problem Statement

The bar for this report is no known bugs and no live or possibly-live data presented as reclaimable. Three gaps remained after #278:

- **R2.** The stale-ObjectId guard (ADR 0024) compared restore points against the raw `GetObjectsInJob()` list. Only `Vapp` containers were special-cased. A job with one explicit VM plus a container entry (host, folder, tag, cluster, protection group) matched at least one object, so the zero-overlap guard stayed off and every VM that exists only inside the container was flagged `StaleObject` while being backed up daily.
- **R3.** The #278 recency state was built only from `Unresolved` candidates. A sibling chain under the same parent backup that resolved to a live job and was written today was invisible, so an old `Unresolved` sibling became `Orphaned`. #278 promised "only Orphaned if nothing under its parent backup was written within the active-chain window".
- **R1.** `Superseded` (`StaleObject`, `Tier2Suppressed`) had no recency check at all, yet the C# table counts it as potentially reclaimable.

## Decision

**Membership rule (R2).** The stale partition for a job runs only when every `GetObjectsInJob()` entry is a known leaf workload. Trusted leaf `Object.Type` values (case-insensitive): `Vm`, `VirtualMachine`. Any other value, a missing `Object`/`Type`, or a `GetObjectsInJob()` that throws means membership is not checkable: nothing is excluded from sizing and nothing is cached as `StaleObject` for that job (the existing "uncertain, so do not exclude" rule). This is an allowlist, so an unknown type fails safe. `Vapp` is a container and therefore also disables the partition for a job that has one; its own restore point is still excluded from sizing as before (#193).

Only `Vapp` (Cloud Director) is confirmed live as a container type. The exact `Object.Type` strings for hosts, folders, clusters, tags, protection groups and agent computers are unconfirmed. If real VBR names the leaf type for VMs or agent computers differently, the partition silently stays off for those jobs (false negatives, never false positives), and a lab run settles it (validation plan S5, S9, S10).

**Recency rule (R1, R3).** One window (`ActiveChainWindowDays`, default 35) guards every category:

- `Unresolved` candidates: the parent backup's newest restore point is taken across every chain under that parent that the sweep saw: all candidates of any reason, plus every group a tier placed on a job (`Cache.ResolvedGroups`, recorded by `Get-VhcJob` with no new SDK calls). Inside the window, or any unusable or future timestamp, the chain is `Unresolved`; otherwise `Orphaned`.
- `StaleObject` / `Tier2Suppressed` candidates: judged on the group's own points. Inside the window, or unusable/future, the group is reported as `Unresolved` ("still being written", not counted as reclaimable); outside it stays `Superseded`. Parent-level recency is deliberately not used here: under per-VM chains a removed VM shares its parent with live siblings, so a parent-level check would make every removed-VM chain permanently unreportable.

Parent keys use `GetParentOrThis().Id`, falling back to the group's `BackupId`. The classifier resolves a resolved group's parent only when an `Unresolved` candidate exists and the group has a point inside the window or an unusable timestamp, which bounds the extra `GetBackup().GetParentOrThis()` calls.

## Consequences

- No live or possibly-live data is counted as reclaimable by the paths covered above; the cost is more `Unresolved` rows and fewer `Superseded` rows in marginal cases.
- Jobs with container entries or unrecognised object types get no stale-object detection (accepted false negative, same stance as ADR 0024's zero-overlap case).
- An `Unresolved` row can now carry a live job's `CurrentJobId` (a recent `StaleObject`/`Tier2Suppressed` group). The C# side already excludes the `Unresolved` category from the reclaimable total regardless of `CurrentJobId`. The existing label for `Unresolved` still reads "Job not matched ..." and is not accurate for those rows; relabelling is left to the pending naming decision.
- Unverified against a real VBR: that sibling chains share a `GetParentOrThis().Id` (S4), and the `Object.Type` strings above (S5, S9, S10). Tier2Suppressed liveness (S6) is covered by the same window.
