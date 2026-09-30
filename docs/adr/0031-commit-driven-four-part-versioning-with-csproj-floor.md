# ADR 0031: Release Versions Are Computed From Commits, Four-Part, With the csproj as a Floor

* **Status:** Proposed
* **Date:** 2026-10-01
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Adam Congdon (@adamcongdon) — agreed in principle on #244, has not reviewed the individual decisions; Claude Code (design)
* **Relates to:** #244 (proposal and decisions),
  [ADR 0032](0032-base-tag-is-highest-ga-tag.md) (how the base tag is chosen),
  #245 and #246 (follow-ups deliberately kept out of this change)

## Context and Problem Statement

CI builds the release version as `Major.Minor.1.<run_number>`, taking
only `Major.Minor` from the csproj. The patch segment is hard-coded to `1`,
so the version says nothing about what changed, and a human has to remember
to hand-edit the csproj `Major.Minor` when a release deserves a minor or
major bump. The repo already writes Conventional Commits (of the 50
non-merge commits since `v3.0.1.193`: 39 `fix`, 3 `feat`), and the
release-notes step already categorises by prefix, so the information to
compute the bump exists and is unused.

## Considered Options

### Option A — Three-part SemVer tags (`v3.1.0`, `v3.1.0-dev.4`)

**Rejected.** Cleaner, but it changes tag and ZIP naming for every consumer
of the tag format, and breaks the assumption in
`CClientFunctions.VbrVersionSupportCheck` that segment 3 exists.

### Option B — A tool that owns the whole release flow (GitVersion, release-please, semantic-release)

**Not adopted.** None of them were evaluated against the repo's existing
four-part tags (`v3.0.1.193`), and they would replace more of the release
flow than this change needs. Revisit if the custom script becomes a
maintenance burden.

### Option C — A small shared script; four-part version; csproj `Major.Minor` as a floor (chosen)

## Decision

The release version is `Major.Minor.Patch.Revision`, computed by one shared
script called by every workflow that needs a version (`ci-cd.yaml`,
`manual-release.yml`, `pr-release-prep.yml`, `sbom-generation.yml`).

- **Revision** stays the CI `run_number`. It carries no meaning beyond
  "later builds are higher", and it is what keeps two builds with the same
  `Major.Minor.Patch` distinct.
- **`Major.Minor.Patch`** is computed from the commits since the Base Tag
  ([ADR 0032](0032-base-tag-is-highest-ga-tag.md)): a `!:` type or
  `BREAKING CHANGE:` footer is a major, a `feat` is a minor, anything else
  (including commits with no Conventional Commits type) is a patch.
- **The csproj `Major.Minor` is a floor.** The result is
  `max(commit-derived, Major.Minor.0)`. It can only raise the version.
- **`Release-As: X.Y.Z` in a commit footer overrides both.** The script
  fails if it is below the floor or not above the last GA version. Several
  footers resolve to the highest.
- **Dev Prereleases** (`-dev`) compute from the last **GA** tag, not the last
  dev tag, so dev and GA agree on `Major.Minor.Patch` for the same commits.

The first computed GA is `3.1.0`: base `v3.0.1.193`, three `feat` commits
since.

## Rationale

- The floor gives a reviewable, revertable way to force a bump in the file
  that already held the version, where a footer is easy to lose in a squash;
  `Release-As` covers the case where a specific version is wanted anyway.
- Keeping four parts avoids touching tag and ZIP naming or the version
  parsing in the tool.
- Dev and GA sharing `Major.Minor.Patch` means a dev build previews the
  number the GA will get.

## Consequences

- **`dev -> master` must stay a merge commit.** A squash merge collapses
  the range into one commit and hides every `feat` and footer in it. The same
  holds one level down: a PR squash-merged into `dev` contributes only its
  title, so the title needs a Conventional Commits prefix. Documented, not
  enforced; commit-lint (advisory at first) is included to help.
- **One lineage only.** Supporting a v3 line and a v4 line in parallel is not
  handled. A release from the lower line would compute against the higher
  line's Base Tag. Revisit when that is needed.
- **Hotfixes** branch from `master`, are PR'd into `master`, and are
  cherry-picked to `dev` with `-x`. The hotfix gets its own patch bump; the
  same fix may appear in two versions' release notes.
- **`manual-release.yml` keeps its `version_override` input** as an escape
  hatch, validated to be four-part and above the last GA version. Its default
  revision is the latest `ci-cd.yaml` run number (`run_number` is per workflow
  and would restart), its pre-releases are tagged `-rc` so they never count as
  a Base Tag, and it refuses to replace an existing release unless
  `version_override` is given.
- **The local auto-increment is untouched.** `increment_version.ps1/.sh` and
  the csproj `Exec` hooks still rewrite the csproj build segment on every
  local build; CI ignores it. Replacing it is #246.
- **`VbrVersionSupportCheck` is unaffected but fragile** (it reads only the
  revision segment). Tracked in #245; it should land before local builds
  move to `3.1.0.x`.
- Commits that reach a release without a Conventional Commits type count as
  patches. Commit-lint is the mitigation, not a guarantee.
