# PR verification

Entry point: `.claude/skills/verify-vhc/scripts/vhc-verify.sh pr <number|branch> [--run] [--dispatch] [--comment] [--repo OWNER/REPO] [--wait MINUTES]`

Answers one question: **did this PR's green checks actually exercise the code the PR changed?** It exists because a workflow that never ran shows up as *absent*, not as failed. PR #295 changed the three live-VBR jobs in `ci-cd.yaml`, every PR check was green, and none of them ran those jobs, because `ci-cd.yaml` only triggers on push to master/dev and `workflow_dispatch`.

Implementation: PowerShell 7 (`scripts/Invoke-PrVerify.ps1` -> `PrVerifyRun.psm1` orchestration -> `PrVerify.psm1` pure logic), shelling out to `gh` with whatever auth is ambient. Tests: `scripts/PrVerify.Tests.ps1` (Pester 5, offline).

## What it does

1. Reads the PR (`pulls/N`), its files with patches, and its commits.
2. Maps every changed file to one or more **surfaces** through `pr-surfaces.json`. A file on no surface is reported as unmapped.
3. Reads the real data for the head SHA: `commits/{sha}/check-runs` and `actions/runs?head_sha=`. That includes `workflow_dispatch` runs on the same SHA, so a dispatch of `ci-cd.yaml` on the exact head SHA counts as exercising the live surfaces.
4. For each surface, evaluates its checks. A check is satisfied only by a **completed, successful** job on the head SHA. Absent, skipped, cancelled, or path-filtered-out is *not exercised*, never passing. In progress is *pending*.
5. Runs hygiene checks, lists the residue a human must still do, and writes `.verify-vhc/evidence/pr-<N>/report.md` and `report.json`.

Workflow edits are judged per job: the patch hunks are intersected with the job line ranges of the workflow file **at the head SHA**, so a PR that edits only the three live jobs is required to show those three jobs ran, not the whole workflow. An edit outside every job (triggers, env, permissions) touches all of them.

## Verdicts and exit codes

| Verdict | Exit | Meaning |
|---|---|---|
| VERIFIED | 0 | Every required check of every touched surface ran on the head SHA and passed; no hygiene failure; no unmapped file; no other check failed or still running. |
| FAILED | 1 | A required check failed, or any other check on the SHA failed, or a hygiene check is `fail`. |
| NEEDS-RUN | 2 | Something touched was not exercised (or is still running). The report prints the command that would exercise it. |
| NEEDS-HUMAN | 3 | Only a human can prove it (live VB365, a publish-only job), or a changed file maps to no surface. |
| (tool error) | 70 | The verifier itself broke. |

Precedence: FAILED > NEEDS-RUN > NEEDS-HUMAN > VERIFIED. Run the NEEDS-RUN commands first; what is left is the human part.

## The map: `pr-surfaces.json`

Data, tracked, and checked by a test.

- `surfaces.<id>`: `paths` (globs), optional `exclude`, `checks`, optional `residue`. Glob syntax: `**/` zero or more directories, `**` anything, `*` anything but `/`, `?` one char but `/`. A file may sit on several surfaces; all of them must be satisfied.
- A **check** has an `id`, a `title`, `required` (default true) and one or more of:
  - `ci`: a list of `{workflow, job}` alternatives (any-of). The job must exist in `workflows`.
  - `local`: `vhc-verify.sh` subcommands (all-of) that give the same proof on a laptop: `build import-report import-scrub cli-checks cli-matrix import-layouts render tests pester`. `--run` executes these for checks CI did not cover.
  - `builtin`: a static check done by the verifier itself (`docs-sanity`, `changelog-format`, `shell-syntax`, `json-syntax`). Docs-only and changelog-only PRs are VERIFIED this way: relative links resolve, fences balance, no conflict markers, `[Unreleased]` present, a link definition per version. Only *new* problems against the base count.
  - `human`: text for what only a person can do. Makes the surface NEEDS-HUMAN.
- `workflows.<file>`: every workflow, `dispatch` (has `workflow_dispatch`), `autoDispatch` (the verifier may dispatch it; only `ci-cd.yaml`), and per job a `trigger`: `pr`, `pr-path-filtered`, `pr-same-repo-only`, `pr-fork-only`, `push-or-dispatch`, `dispatch-only`, `publish-only`, `scheduled`, `self`; plus `lab: true` for self-hosted runners that hold lab credentials.
- Surface `ci-workflows` has `kind: workflow`: its checks are derived from the registry for each changed workflow file and job.
- `hygiene`: which paths count as product code (a diff with none is "tooling-only"), which commit types are exempt from the changelog rule, and generated files.

### Extending it

- New tracked file that matches no glob: add the glob to the right surface (or a new one). The Pester test fails until you do, and a PR that adds such a file is reported NEEDS-HUMAN.
- New or renamed workflow job: add it to `workflows`. The test fails if the registry and `.github/workflows/*.yml` disagree in either direction, or if a check names a workflow or job that does not exist.
- New `vhc-verify.sh` subcommand to use as a `local` check: add it to `$script:KnownLocal` in `PrVerify.psm1` and to the run table in `Invoke-LocalChecks` (`PrVerifyRun.psm1`).

Run the map test locally:

```bash
pwsh -NoProfile -c 'Import-Module Pester -MinimumVersion 5.0.0; Invoke-Pester -Path .claude/skills/verify-vhc/scripts/PrVerify.Tests.ps1 -CI'
```

It runs in CI in `crossplatform-tests.yml` job `verify-vhc` (any PR touching `.claude/skills/verify-vhc/**`) and in `pester-tests.yml` (any PR touching a `.ps1`). It asserts: every tracked file matches a glob, every glob matches a tracked file, the registry equals the workflow files and their jobs, every `ci` / `local` / `builtin` reference resolves, plus the verdict logic, the dispatch gates and an offline replay of the PR #295 failure mode.

## Flags

- `--run`: run the local subcommands for checks that CI did not cover. Refuses (and says so in the report) unless the checkout is the PR head SHA and tracked files are clean, so the evidence is about the right code. Evidence lands under `.verify-vhc/evidence/pr-<N>/` (`run-<subcommand>.log`) and the subcommand IDs are `pr-<N>` / `pr-<N>-scrub`, the same convention as `ci`.
- `--dispatch`: dispatches `ci-cd.yaml` on the PR head branch when a live-lab check has no run on the head SHA, waits (up to 120 min), and folds the result in. Gates, all of which must hold: same-repo head (never a fork), head branch is not `master`/`main`, the workflow is marked `autoDispatch` and is not a release/publish/deploy/sbom workflow (`manual-release.yml` and `beta-release.yml` are never dispatchable), the branch tip equals the PR head SHA, no `ci-cd.yaml` run already exists on that SHA (at most one dispatch per head SHA), and the PR is open.
- `--comment`: creates or updates ONE comment identified by the hidden marker `<!-- vhc-pr-verify -->`. If a marker comment exists it is PATCHed; a second one is never created. Skipped for fork PRs.
- `--wait MINUTES`: poll until the other checks on the SHA settle first (used by the workflow).

## Hygiene checks

Each is `pass`, `warn` (advisory), `fail` (turns the verdict to FAILED) or `info`.

| Check | Level when violated | Rule |
|---|---|---|
| `conventional-title` | warn | PR title is a Conventional Commit (uses `.github/scripts/VhcVersion.psm1`). Types decide the release version. |
| `version-bump-type` | **fail** | `feat`, `!` or a `BREAKING CHANGE:` footer on a tooling-only diff would bump the product minor/major (repo CLAUDE.md, Versioning). Retype as `ci`/`test`/`docs`/`chore`. |
| `fixes-keyword` | warn | `(#N)` shorthand or "issue #N" without `Fixes/Closes/Resolves #N` (repo CLAUDE.md, Commit / PR Convention). |
| `changelog` | warn | User-visible product code changed (type not `refactor/test/ci/docs/chore/style/build`) but no line was added under `## [Unreleased]` in `ChangeLog.md`. |
| `generated-file` | warn | `docs/changelog.md` edited by hand; CI regenerates it. |
| `csproj-version` | **fail** | A `<Version>`-class element changed in a csproj. Versions are computed from commits (ADR 0031); the local-build auto-bump must be reverted. |
| `actions-pinned` | **fail** (warn if under 80% of this repo's refs are pinned) | An added `uses:` is not pinned to a 40-character SHA. |

The residue list comes from each touched surface's `residue` (for example "Windows GUI smoke" for GUI changes, PDF content, live collection).

## The PR workflow: `.github/workflows/pr-verify.yml`

Runs on every PR to master/dev, writes the report to the job summary, and posts the sticky comment for same-repo PRs. It polls for up to 45 minutes until every other workflow run and check-run on the head SHA has completed (two identical polls, after a 90 s grace so late-registering workflows appear), excluding its own run by workflow path, so it cannot wait on itself. `concurrency` cancels the older run on a new push. It uses polling rather than `workflow_run` because `workflow_run` runs the default branch's copy with a write token and would let a fork PR trigger it. It never dispatches: `ci-cd.yaml` runs on self-hosted lab runners; a missing live run is reported as NEEDS-RUN with the command. Advisory today (`PR_VERIFY_STRICT: 'false'`); flip it and require the check in branch protection to enforce.

## What it cannot prove

- It proves that the checks mapped to the touched surfaces ran on this exact SHA and passed. It does not read the diff for logic errors.
- It is exactly as good as the map. A surface with weak checks (see the coverage matrix in [README.md](README.md)) gives a weak VERIFIED.
- Live collection on a real VBR/VB365 host, the real Windows GUI window, and PDF output stay with a human unless a live job covers them. VB365 collection scripts are always NEEDS-HUMAN.
- Publish-only jobs (`create-release`, `virustotal-scan`, `manual-release.yml`, `beta-release.yml`, `docs-deploy.yml`, `sbom-generation.yml`) and cron jobs cannot be exercised from a PR; a PR that edits them is NEEDS-HUMAN.
- A job that ran on an older SHA does not count. A PR whose branch moved after a dispatch needs a new run.
- It trusts the GitHub API's check-run conclusions. A job that exits 0 without testing anything (a fail-open step) is still reported as passing; the coverage matrix records those.
- `pr-verify.yml` and the map test only fail a PR on a rotted map when the PR touches the skill or a `.ps1`; elsewhere an unmapped new file shows up as NEEDS-HUMAN in the report instead.
