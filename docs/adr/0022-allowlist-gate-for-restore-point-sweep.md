# ADR 0022: Gate the Restore-Point Sweep Behind an Allowlist of Proven-Safe Job Types, Not a Flag or a Denylist

* **Status:** Accepted
* **Date:** 2026-08-21
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design, empirical validation)

## Context and Problem Statement

[ADR 0021](0021-tiered-restore-point-sweep-for-job-sizing.md) replaces
per-job `GetLastBackup()` sizing with a global restore-point sweep for
correctness. The sweep has a real, measured cost that scales with
restore-point volume, not just the one-time fetch: `GetSourceJob()` runs at
~9ms/call (measured: 280 calls, 2.6s, on-prem lab). In a large,
otherwise-simple all-VMware/Hyper-V environment — where nothing is
`Type=Snapshot`-skipped and every restore point pays that call — 50,000
restore points would cost minutes, not seconds. Running the sweep
unconditionally would tax exactly the large, conventional environments this
design has no reason to slow down.

## Considered Options

### Option A — Opt-in flag (environment variable / CLI parameter / GUI toggle)

Ship the sweep disabled by default; let users enable it.

**Rejected.** This ships the 0/0 bug unfixed in every report by default —
the entire reason for ADR 0021 exists. Nobody who doesn't already know
about the bug would think to enable a flag to fix it. It also conflicts
with this codebase's own stated convention (`CLAUDE.md`): "Don't use
feature flags or backwards-compatibility shims when you can just change the
code."

### Option B — Denylist of known-broken job types

Run the sweep only if `$Jobs` contains a type already known to be broken
under today's method (HPE Morpheus, Nutanix AHV, oVirt KVM, etc.).

**Rejected.** Requires enumerating every broken type in advance, and this
design's own validation work found two — Proxmox Backup and Backup Copy —
that weren't part of the original problem statement and would not have
been on any list written before this session started. A denylist fails
toward silent wrongness: an environment with an unrecognized broken type
keeps using today's (possibly wrong) numbers indefinitely, with nothing to
prompt anyone to add it to the list.

### Option C — Allowlist of proven-safe job types (chosen)

## Decision

Before running the sweep, check whether every job's `.TypeToString` is a
member of a small allowlist of types already **proven** safe under today's
method (real, non-zero, exactly-matching data across the validation labs —
see below). If so, skip the sweep entirely: every job uses today's
`GetLastBackup()` + scoped `Get-VBRRestorePoint` method, unchanged, zero
added cost. If `$Jobs` contains anything not on that list — including a job
type not yet seen in testing — run the sweep once and route *every* job
through it, since tier 1 alone already reproduces today's numbers exactly
for the allowlisted types and the sweep's dominant cost is paid regardless
of how many jobs use the result.

Replica job types (`VMware Replication`, `Hyper-V Replication`) are outside
this decision entirely — per ADR 0021, they always use the per-job path
regardless of whether the sweep runs for other jobs.

**The allowlist is evidence-tiered, not simply "everything that looked
fine":** only types with real, non-zero restore-point data matching old
exactly qualify — `VMware Backup`, `Hyper-V Backup`, `Windows Agent
Backup`, `Windows Agent Policy`, `Linux Agent Backup`, `Cloud Director
Backup`. Types that were only ever observed as `0/0` under both methods in
testing (`File Backup`, `Object Storage Backup`, `Entra ID Log Backup`,
`Microsoft Azure virtual network`) do **not** qualify yet — two methods
agreeing on zero for a job that never ran is not evidence either method
handles that type's real data correctly. These get the sweep (a
performance cost, not a correctness risk) until proven otherwise.

## Rationale

- **An allowlist fails toward correctness; a denylist fails toward silent
  wrongness.** Given a choice between the two failure directions, wrong
  performance characteristics are recoverable (a future release can add
  the type to the allowlist once proven); silently wrong data in a
  health-check report is the exact problem this whole body of work exists
  to fix.
- **This is empirically justified, not theoretical.** Proxmox and Backup
  Copy were both discovered as broken *during this session's own testing*,
  after the original problem statement was written. A denylist authored
  before this session would have missed both.
- **`TypeToString` values must be used, never console display names.**
  The two can differ (a job the VBR console displays as "Microsoft Azure
  virtual network" is a different string than what `.TypeToString` may
  return) — a wrong string in the allowlist would either silently defeat
  the gate (treating a broken type as safe) or silently keep a broken type
  on the fast path, defeating ADR 0021 for that type.

## Consequences

### Positive
- Environments using only proven-safe job types get zero behavior and
  zero performance change from ADR 0021.
- Any environment with an unrecognized or not-yet-allowlisted job type
  defaults to the slower-but-correct path automatically — no code change
  required when VBR adds a new platform integration.

### Negative
- Large environments with even one job of an un-allowlisted type pay the
  full sweep cost for every job, including ones that would have been fine
  on the old path. The allowlist is deliberately narrow at launch (only
  types with real matching evidence), so this will affect more
  environments initially than a more permissive but less-proven list
  would.
- The exact allowlist string constants are not fixed by this ADR —
  finalizing and testing them against `TypeToString`, not display names, is
  implementation work.

## Validation

Cost model: on-prem lab (5,752 restore points, mostly `Type=Snapshot`)
measured 17.56s sweep / 7.59x today's method. The same `GetSourceJob()`
per-call cost (~9ms), applied to a large all-VMware environment with none
of the `Type=Snapshot` skip's amortization, would scale linearly with
restore-point count — the scenario this gate exists to protect. Full detail
in
[`docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md`](../superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md).
