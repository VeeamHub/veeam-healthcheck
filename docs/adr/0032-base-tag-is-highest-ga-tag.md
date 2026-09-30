# ADR 0032: The Base Tag Is the Highest GA Tag, Not `git describe`'s Nearest or Reachable Tag

* **Status:** Proposed
* **Date:** 2026-10-01
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Adam Congdon (@adamcongdon) — agreed in principle on #244; Claude Code (design, and an independent review that found the reachability hole below)
* **Relates to:** [ADR 0031](0031-commit-driven-four-part-versioning-with-csproj-floor.md), #244
* **Amends:** the first draft of this ADR and the decision recorded on #244, which chose the highest *reachable* GA tag

## Context and Problem Statement

[ADR 0031](0031-commit-driven-four-part-versioning-with-csproj-floor.md)
computes the next version, and the release notes, from the commits since
a Base Tag. The release jobs currently find it with a plain
`git describe --tags --abbrev=0`, which returns the **nearest** tag of any
kind. That is already wrong in two ways:

- On `dev` it returns the newest Dev Prerelease tag (`v3.0.1.225-dev`). A
  `dev -> master` merge descends from it, so the next GA's range is
  `v3.0.1.225-dev..HEAD`, which is empty.
- The stray tag `v3.0.2-beta.1` is nearer than the real previous release for
  `v3.0.1.193`. From the tag graph and the published body, that release's
  notes list 1 commit where 6 were due (the run logs have expired, so this is
  inferred).

A hotfix on `master` makes it worse: `master` then carries two tag lines
(hotfix tags, and tags on `dev`-side commits), and "nearest" depends on the
shape of the merge graph.

There is a second constraint. Release tags must point at the commit that was
built, which for a GA is the `dev -> master` merge commit. `softprops/action-gh-release`
is currently given `tag_name` with no `target_commitish`, so tags land on the
default branch (`dev`); `v3.0.1.193` shows `targetCommitish: dev`. Fixing that
puts every new GA tag on a `master` commit, and **`master` is not merged back
into `dev`** (its last merge into `dev` was in April).

## Considered Options

### Option A — `git describe` with `--exclude` and `--match` globs

**Rejected.** `--match` uses `fnmatch`, where `*` also matches `-` and `.`.
`v[0-9]*.[0-9]*.[0-9]*.[0-9]*` matches 72 tags, where only 51 are real
four-part tags: the 20 `-dev` tags and `v3.0.2-beta.1` match too. Excluding
suffixes one by one keeps failing on the next unexpected tag, and it still
returns the nearest tag, not the highest.

### Option B — The highest GA tag reachable from the commit being built

**Rejected** (this was the first draft). It is correct from `master`, but once
GA tags live on `master`'s merge commits, `dev` cannot reach them. After the
first new-scheme GA (`v3.1.0.300`), `dev` would still base on `v3.0.1.193`,
compute `3.1.0.x-dev` for a `fix:`, and ship a dev version that has already
been released while the next GA computes `3.1.1`. A consumed `Release-As:`
footer would also stay in `dev`'s range forever. Hotfix-only tests did not
catch this; a scenario that tags a GA on `master` and then commits on `dev`
does.

### Option C — Merge `master` back into `dev` after every GA

**Rejected.** It would keep "reachable" working, but it is a process change
the repo does not follow today, and one missed merge-back silently
reintroduces the bug.

### Option D — Tag `dev`'s side of the merge instead of the built commit

**Rejected.** It breaks the hotfix flow and means tags no longer mark what
was built, which undoes the `target_commitish` fix.

### Option E — The highest GA tag among all fetched tags (chosen)

## Decision

List all fetched tags (`git tag --list`), keep only those matching
`^v\d+\.\d+\.\d+\.\d+$` (no suffix, so `-dev`, `-beta.N` and the legacy
three-part tags are out), and take the highest by version order. That is the
Base Tag. The commits since it are `git log <base>..<ref> --no-merges`, which
is well defined whether or not the tag is an ancestor of `<ref>`: commits
already released through a merge are ancestors of the tag and drop out. The
Base Tag decides the computed version for GA and dev builds alike, and it is
the start of the GA release-notes range. Dev release notes instead start from
the highest tag of either kind (GA or `-dev`), since they describe what
changed since the previous build.

Each release tag must point at the commit that was built. Both release steps
pass `target_commitish: ${{ github.sha }}`.

## Rationale

- `dev` and `master` compute from the same Base Tag, so a `fix:` on `dev`
  after `3.1.0` ships is `3.1.1`, the same number the next GA will get.
- After a hotfix, the next release takes the hotfix tag as its base, so the
  version is greater than it and released commits are not listed again. A
  `dev` build sees the hotfix tag too; when `dev` only has the cherry-picked
  copy of the fix, that copy is the one commit in its range.
- Revision ordering is not relied on for this: `run_number` makes the full
  four-part version unique and increasing, but the patch number must still
  say whether a release contains new fixes.

## Consequences

- **All tags must be fetched.** Every job that computes a version checks out
  with `fetch-depth: 0` (or runs `git fetch --tags`). With no tags the base is
  `0.0.0` and the csproj floor applies.
- **GA tags must only come from `master`.** A GA-looking tag cut from another
  branch would become the Base Tag for everything. `manual-release.yml` pre-releases
  are tagged `-rc` for this reason, and a manual GA should be dispatched from
  `master`.
- **Parallel lines break the assumption.** With v3 and v4 maintained side by
  side, the highest GA tag would be a v4 tag even when building the v3 line.
  Revisit with the multi-line work named in
  [ADR 0031](0031-commit-driven-four-part-versioning-with-csproj-floor.md).
- After the tag-target change, the first GA from a `dev -> master` merge is
  tagged on the merge commit, not on `dev`'s tip.
- Anyone simplifying the lookup back to `git describe`, or to
  `git tag --merged`, reintroduces the problems above.
