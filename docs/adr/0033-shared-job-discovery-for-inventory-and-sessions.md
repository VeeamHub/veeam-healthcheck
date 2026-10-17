# ADR 0033: Shared Job Discovery for the Job Inventory and Session Collectors

* **Status:** Accepted
* **Date:** 2026-10-10
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code
* **Amends:** ADR 0018 (which job list the session fast path iterates)

## Context and Problem Statement

`Get-VBRJob` does not return every job. On at least one VBR 12.3.x environment
it omits Nutanix AHV jobs (issue #222). #222 fixed the job inventory
(`Get-VhcJob`, `_Jobs.csv`) with additive discovery fallbacks: tier B rebuilds
jobs from `Get-VBRBackup` + `GetJob()`, tier C enumerates the core job model on
VBR versions below 13.

`Get-VhcBackupSessions` kept its own `Get-VBRJob` call. ADR 0018's fast path
queries sessions one job Id at a time, so a job `Get-VBRJob` never returned was
never queried: it had no session rows at all (issue #224), a total data gap
rather than a mis-rollup.

## Decision

Job discovery moves out of `Get-VhcJob` into a new public cmdlet,
`Get-VhcJobDiscovery`, unchanged apart from recording which step found each
job. `Get-VBRConfig.ps1` runs it once, as its own collector, before sessions,
and passes the result to both `Get-VhcBackupSessions` and `Get-VhcJob` through
a `-JobDiscovery` parameter.

The result separates the parts, because the two consumers need different sets:

| Property | Contents | Used by |
|---|---|---|
| `Jobs` | everything, in the order `Get-VhcJob` always used | `Get-VhcJob` |
| `VbrJobs` | what `Get-VBRJob` returned | `Get-VhcBackupSessions` |
| `DiscoveredJobs` | tier B and tier C additions only | `Get-VhcBackupSessions` |
| `StandaloneAgentJobs` | standalone agent jobs | `Get-VhcJob` (via `Jobs`) only |

`Get-VhcSessionReport` also receives the result: its `VbrJobs` and
`DiscoveredJobs` replace its own `Get-VBRJob` call when it builds the JobId to
current-name map, so a renamed job that `Get-VBRJob` does not return is
labelled with its current name. Backup Copy per-source workers
(`SimpleBackupCopyParentWorker`) and standalone agent jobs are left out of the
map. A worker's Id is the `PolicyTag` of its self-referencing child sessions
(ADR 0030), so mapping it would rewrite those sessions' `PolicyName` and change
the CSV on servers where nothing is hidden. The agent and EP job lookups there
still run.

`Get-VhcBackupSessions` queries `VbrJobs` + `DiscoveredJobs`. Standalone agent
jobs stay out of the session path: it has never queried them, and adding them
would change session output on environments that never had the #224 gap.
Where tiers B and C find nothing, the session job list is exactly what
`Get-VBRJob` returned before.

Tiers B and C do not only find jobs `Get-VBRJob` hides. They also collect
VBR's internal Backup Copy per-source worker jobs (`SimpleBackupCopyParentWorker`,
named `Parent\Child`, see #225), which exist on any Backup Copy setup. ADR 0018's
query for the parent already returns its per-machine child sessions, so
querying the worker as well could return the same sessions twice. The session
fast path (`Get-VhciJobSessions`) therefore keeps one copy of each session by
its `Id`; a session with no `Id` is never dropped.

Both consumers fall back to their previous behaviour when no discovery is
passed (`Get-VhcJob` runs discovery itself; `Get-VhcBackupSessions` calls
`Get-VBRJob`). If the discovery collector fails, `Get-VBRConfig.ps1` passes
`$null` and each does that.

## Consequences

* **Good:** one discovery pass instead of two, and one place for the tier B/C
  logic. Running it in the session collector as well would repeat every
  `Get-VBRBackup` enumeration, `GetJob()` call and core-job fetch.
* **Good:** the `$script:VhcOrphanedSupersededCache` ordering concern in #224
  does not apply. That cache is set by the restore-point sweep, not discovery,
  and is already passed explicitly via `Get-VhcJob`'s output.
* **Neutral:** `_CollectionManifest.csv` gains a `JobDiscovery` row. Discovery
  errors are still attributed to the `Jobs` collector, so a `Get-VBRJob`
  failure fails `Jobs` exactly as before.
* **Neutral:** only the fast path is affected. The slow path ignores the job
  list and fetches every session with one unfiltered cmdlet call, so it never
  had the #224 gap. Both lab collections used the fast path.
* **Neutral:** if `Get-VBRJob` fails inside discovery, the collector still
  returns a result with an empty `VbrJobs`, and the session collector no longer
  retries `Get-VBRJob` itself as it did before. Such failures are usually
  persistent, and the error is still recorded under `Jobs`.
* **Bad:** a job found by tier B/C that is also an agent job but missing from
  `Get-VBRComputerBackupJob` would be queried on the VM/BackupCopy path. Agent
  Ids that both lists contain are subtracted as before.
* **Unverified:** the end-to-end fix has not been confirmed on an environment
  that actually hides jobs from `Get-VBRJob`. It is covered by mocked tests
  through the ADR 0018 seam.
