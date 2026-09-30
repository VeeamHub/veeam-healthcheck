# ADR 0030: Unconditional Merge for Hierarchical Session Rollup

* **Status:** Accepted
* **Date:** 2026-09-14
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (independent PR review)
* **Amends:** ADR 0019 (the guard this removes was added on top of ADR 0019's
  GUID-based rollup, in the same commit that fixed issue #219)

## Context and Problem Statement

ADR 0019 anchors session rollup on `CSessionGroupKey.Of`: children carry their
parent's GUID in `Info.PolicyTag`, so grouping by GUID merges per-machine
children under the parent automatically. Issue #219 found a gap: some
Backup-Copy per-object child sessions report `PolicyTag == JobId` (a
self-reference meaning "no parent GUID link exists at all"), so the GUID
grouping alone left each of those children as its own row, with a
mangled `"<Parent>\<child>"` display name (VBR's own internal naming
convention for that job-hierarchy construct — not a user-typed name).

The fix landed in `691a14ac`: `CSessionGroupKey.Group()` added a second,
hierarchical pass on top of `Of()`. For any session whose `DisplayName`
contains `\`, it splits on the first `\` and looks the prefix up against the
non-hierarchical identities seen elsewhere in the window. If a match is
found, the child is folded into that parent's group instead of standing
alone.

That same commit also added an `identityHasData` guard: the merge was only
allowed to happen if the matched parent identity itself carried zero
`DataSize`/`BackupSize` across the window. The stated intent (see the removed
comment) was defensive — protect against two *unrelated* jobs happening to
share a name prefix, where a data-bearing "parent" identity is actually its
own real job and should keep its own row rather than absorbing an unrelated
child.

Independent PR review, using a synthetic reconstruction of a real production
Backup Copy job's session shape, found that this guard backfires for the
exact case the hierarchical pass exists to fix.

## Evidence

Reconstructed scenario (synthetic names/GUIDs only), matching a real Backup
Copy job (`"ParentJob"`) observed in production:

- A correctly GUID-linked child session: `PolicyTag` = the parent's real,
  different `JobId`; `PolicyName` = `"ParentJob"` (canonical, no backslash);
  real non-zero `DataSize`/`BackupSize`.
- A self-referencing child session: `PolicyTag == own JobId`; `PolicyName`
  empty, so `DisplayName` falls back to `JobName`, which VBR populates as
  `"ParentJob\ChildVm01 Backup"` (backslash, per VBR's internal naming for
  this construct); also real non-zero `DataSize`/`BackupSize`.

Both sessions are ordinary, healthy children of the same job, and both are
present in the same reporting window — this is not a rare timing accident,
it is the normal steady state for a running Backup Copy job with both kinds
of child session.

Walking `Group()`'s two passes against this input:

- **Pass 1** indexes the GUID-linked child under identity `"ParentJob"`
  (its `DisplayName` has no backslash) and, because it has non-zero data,
  sets `identityHasData["ParentJob"] = true`.
- **Pass 2** processes the self-referencing child: its `DisplayName` prefix
  `"ParentJob"` matches the identity indexed in Pass 1, but
  `identityHasData["ParentJob"]` is `true`, so the guard refuses the merge.
  The child falls through to its own standalone group with the mangled
  `"ParentJob\ChildVm01 Backup"` name.

The result is precisely issue #219's original symptom (a per-machine Backup
Copy child landing outside its parent's row) for exactly the case the
hierarchical pass was written to solve.

## Decision

Remove `identityHasData` entirely. Once a hierarchical child's prefix
matches an already-indexed parent identity in `keyByIdentity` (Pass 1),
`Group()` now **always** merges it into that parent's group — the merge is
no longer conditioned on whether the parent identity has data of its own.

## Rationale

- The guard's protective scenario — two *unrelated* jobs coincidentally
  sharing a name prefix — was never observed or verified in practice. The
  confirmed failure mode (two *related* children of the same job, one
  GUID-linked, one self-referencing, both with data) is the normal case for
  any actively-running Backup Copy job.
- A backslash-containing identity is not an arbitrary user coincidence in
  the first place: VBR rejects `\` in user-entered job names (see the
  `Of`/`Group` doc comments), so every hierarchical identity this code sees
  is either a VBR-populated `PolicyName` for a genuinely GUID-linked child or
  VBR's own internal worker-job naming convention — never something a user
  typed that happened to collide.
- Even if the guard's hypothetical collision did occur, refusing the merge
  does not produce a strictly safer result — it produces an equally wrong
  one (a mislabeled standalone row instead of a mislabeled merged row).
  Always-merge is no worse for the unconfirmed rare case and strictly better
  for the confirmed common one.

## Consequences

### Positive

- Per-machine Backup Copy children with self-referencing `PolicyTag` now
  roll up under their parent even when the parent identity also has a
  GUID-linked sibling with real data in the same window — closing the gap
  that reopened issue #219's symptom.
- `Group()` loses a dictionary and a conditional; the hierarchical merge
  path is now a single unconditional assignment once a prefix match is
  found.

### Negative / Accepted Trade-off

- This reopens, in theory, the false-positive-collision risk the guard was
  originally meant to prevent: two *unrelated* jobs that happen to share a
  name prefix could now merge into one row. This risk has no verified
  occurrence in this codebase's evidence to date, and — per the Rationale
  above — the guard's absence doesn't make the failure mode any worse than
  the guard's presence did for the confirmed case; it merely relocates which
  case is favored. If a genuine prefix collision between unrelated jobs is
  ever observed, it should be fixed with an identity check that doesn't
  penalize the common same-job case (e.g., matching on both name prefix and
  a job-type/family signal), not by reinstating a bare data-presence guard.

## Validation

`CSessionGroupKeyTEST` (new/updated cases):

- A GUID-linked child with data and a self-referencing hierarchical child
  with data, for the same parent identity, in one `Group()` call — asserts
  they land in the same group under the clean parent display name.
- The same scenario extended with a second self-referencing hierarchical
  child — asserts all children merge into the one group.
- The pre-existing "data-bearing parent blocks the merge" test is flipped to
  assert the merge now happens, with the guard's removal explained inline.
- Existing GUID-only and no-parent-in-window (fabricated `"name:"` group)
  cases are unchanged and re-verified by hand (this worktree cannot run the
  Windows-only test project).

## Related

- **ADR 0019** — GUID-based rollup via `Info.PolicyName`/`Info.PolicyTag`.
  Amended by this ADR; the GUID design itself is untouched.
- **Issue #219** — original hierarchical-rollup gap this pass (and its now-
  removed guard) were built to close.
