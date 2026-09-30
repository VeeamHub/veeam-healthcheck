# ADR 0032: The Base Tag Is the Highest Reachable GA Tag, Not `git describe`'s Nearest Tag

* **Status:** Proposed
* **Date:** 2026-10-01
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Adam Congdon (@adamcongdon) — agreed in principle on #244; Claude Code (design)
* **Relates to:** [ADR 0031](0031-commit-driven-four-part-versioning-with-csproj-floor.md), #244

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

## Considered Options

### Option A — `git describe` with `--exclude` and `--match` globs

**Rejected.** `--match` uses `fnmatch`, where `*` also matches `-` and `.`.
`v[0-9]*.[0-9]*.[0-9]*.[0-9]*` matches 72 tags, where only 51 are real
four-part tags: the 20 `-dev` tags and `v3.0.2-beta.1` match too. Excluding
suffixes one by one keeps failing on the next unexpected tag, and it still
returns the nearest tag, not the highest.

### Option B — The highest-versioned GA tag reachable from the commit being built (chosen)

## Decision

List the tags reachable from the commit being built
(`git tag --merged <commit>`), keep only those matching
`^v\d+\.\d+\.\d+\.\d+$` (no suffix, so `-dev`, `-beta.N` and the legacy
three-part tags are out), and take the highest by version order. That is the
Base Tag. It decides the computed version for GA and dev builds alike, and
it is the start of the GA release-notes range. Dev release notes instead
start from the nearest tag of any kind, including `-dev`, since they
describe what changed since the previous build.

For the set of tags to be trustworthy, each release tag must point at the
commit that was built. `softprops/action-gh-release` is currently given
`tag_name` with no `target_commitish`, so tags land on the default branch
(`dev`); `v3.0.1.193` shows `targetCommitish: dev`. Both release steps are
changed to pass `target_commitish: ${{ github.sha }}`.

## Rationale

- Correctness does not depend on the shape of the merge graph. After a
  hotfix, the next release from `dev` takes the hotfix as its base, so the
  next version is greater than it and released commits are not listed again.
- Revision ordering is not relied on for this: `run_number` makes the full
  four-part version unique and increasing, but the patch number must still
  say whether a release contains new fixes beyond the hotfix.

## Consequences

- **Parallel lines break the assumption.** With v3 and v4 maintained side by
  side, "highest reachable" from the v3 line could still see a v4 tag.
  Revisit with the multi-line work named in
  [ADR 0031](0031-commit-driven-four-part-versioning-with-csproj-floor.md).
- After the tag-target change, the first GA from a `dev -> master` merge is
  tagged on the merge commit, not on `dev`'s tip.
- Anyone simplifying the lookup back to `git describe` reintroduces the
  empty-notes and stray-tag problems above.
