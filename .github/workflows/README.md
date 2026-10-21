# GitHub Workflows

This directory contains GitHub Actions workflows for the Veeam Health Check project.

## Manual Release Workflow

**File:** `manual-release.yml`

A manually-triggered workflow for creating releases with full control over the process.

### Usage

1. Go to **Actions** → **Manual Release**
2. Click **Run workflow**
3. Fill in the options:

| Input | Required | Description |
|-------|----------|-------------|
| `pr_number` | No | PR number to extract changelog from. Leave empty for no changelog. |
| `version_override` | No | Override the version (four parts, e.g., `3.2.0.5`; must be above the last GA version). Leave empty to compute it from commits. |
| `prerelease` | No | Mark as pre-release. The tag gets an `-rc` suffix, so it never becomes a Base Tag. |
| `skip_virustotal` | No | Check to skip VirusTotal scanning |
| `dry_run` | No | Check to build without creating release (preview mode) |

### Features

- **Automatic changelog extraction** from PR body when PR number provided
- **VirusTotal scanning** when `VT_API_KEY` variable is configured
- **SHA256 checksum** computed and included in release notes
- **Dry run mode** to preview release without publishing
- **Idempotent** - can update existing releases

### Example: Creating a Release After Merging PR

```bash
# After merging PR #65 to VeeamHub
# Go to Actions → Manual Release → Run workflow
# Enter: pr_number = 65
# Click: Run workflow
```

### Setting Up VirusTotal

1. Get API key from [VirusTotal](https://www.virustotal.com/gui/my-apikey)
2. Go to repository **Settings** → **Secrets and variables** → **Actions**
3. Add variable: `VT_API_KEY` = your API key

---

## CI/CD Pipeline

**File:** `ci-cd.yaml`

Automatic pipeline that runs on every push to master/dev branches.

### Jobs

| Job | Runs On | Description |
|-----|---------|-------------|
| `build-and-test` | windows-latest | Build, test, coverage, create artifact |
| `integration-test-vbr` | self-hosted | Test against VBR v13 (optional) |
| `integration-test-vbr-12` | self-hosted | Test against VBR v12 (optional) |
| `integration-test-vbr-13-sql` | self-hosted | Test against VBR v13+SQL (optional) |
| `virustotal-scan` | windows-latest | Scan artifact with VirusTotal |
| `create-release` | windows-latest | Create GitHub release |

### Notes

- **Integration tests are optional** - they run when self-hosted runners are available but don't block releases
- **VirusTotal scan** only depends on `build-and-test`, not integration tests
- **Releases** are created automatically when VirusTotal scan passes (or is skipped)

---

## Beta Release Workflow

**File:** `beta-release.yml`

Nightly (`0 6 * * *` UTC, about 02:00 Eastern) or manual (**Actions** → **Beta Release**). Replaces the old per-push dev prerelease: `ci-cd.yaml` no longer publishes releases for `dev`.

- **Promotion, not rebuild.** It picks the newest `ci-cd.yaml` push run on `dev` that fully passed and ships that run's `veeam-healthcheck-zip` artifact, so what was tested is what ships. Artifacts expire after 7 days; if the newest green build is older than that the run fails with a clear message.
- **Gate.** The run must have concluded `success` and its `virustotal-scan` job (including the `Evaluate ZIP scan result` step) must have succeeded, which is the same condition as `zip_clean == true`.
- **No change, no release.** If the green build's commit is already the latest beta, the run exits with a notice. Manual runs can set `force` to cut anyway (fails if that tag exists).
- **Tag and title.** `3.1.0.250` becomes tag `v3.1.0-beta.250`, titled `[Beta] Veeam Health Check v3.1.0-beta.250`, marked prerelease, targeting the built commit. Notes list commits since the highest GA, legacy `-dev` or `-beta` tag, plus the zip SHA256. `-beta.N` tags never become a Base Tag.
- **Retention.** After a cut, only the newest 10 `-beta.` releases are kept; older ones are deleted with their tags. GA, `-rc` and legacy `-dev` releases are never touched.
- **Environments.** Scheduled runs use `beta` (no approval); manual runs use `beta-manual`, which waits for the required reviewer. Both are limited to `dev`.
- **Tokens.** Tags under `v*` are protected by a tag ruleset, so releases are created with a GitHub App token from `RELEASE_APP_ID` / `RELEASE_APP_PRIVATE_KEY`. If `RELEASE_APP_ID` is empty the run logs a warning and falls back to `GITHUB_TOKEN`.

---

## Other Workflows

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `codeql.yml` | Push/PR | Security scanning with CodeQL |
| `Semgrep.yml` | Push/PR | Additional security scanning |
| `virus-scan.yml` | Push/PR | Quick virus scan of source files |
| `dependency-scan.yml` | Push/PR/Schedule | Check for vulnerable dependencies |
| `pr-release-prep.yml` | PR | Build artifact for PR review |
| `pr-comment.yml` | PR | Post build info as PR comment |
| `sbom-generation.yml` | Push | Generate software bill of materials |
| `stale.yml` | Schedule | Mark stale issues/PRs |
| `actionlint.yml` | Push/PR touching workflows | Lint workflows (blocking); see [Linting](#linting) |
| `psscriptanalyzer.yml` | PR touching PowerShell | PSScriptAnalyzer backlog (advisory); see [Linting](#linting) |

---

## Linting

### actionlint (`actionlint.yml`)

Runs [actionlint](https://github.com/rhysd/actionlint) (pinned version, checksum-verified) on
`ubuntu-latest` for pushes and PRs into `master`/`dev` that touch `.github/workflows/**` or
`.github/actionlint.yaml`.

- **Blocking:** syntax, expression, `needs:`, action-input and runner-label errors fail the check, as
  annotations on the offending line.
- **Advisory:** shellcheck findings in `run:` blocks are annotated and counted in the job summary but
  never fail it. Most of them are PowerShell steps on the Windows lab runners, which actionlint parses as
  bash because their labels don't say `windows`. Adding `shell: pwsh` to those steps would clear them.
- **New self-hosted runner label?** Add it to `.github/actionlint.yaml`, or every job using it fails
  with an unknown-label error.
- Locally: `actionlint -shellcheck= -pyflakes=` from the repo root (the blocking pass), or plain
  `actionlint` to see shellcheck too.

### PSScriptAnalyzer (`psscriptanalyzer.yml`)

Advisory: never fails the PR. Runs a pinned
[PSScriptAnalyzer](https://github.com/PowerShell/PSScriptAnalyzer) on `windows-latest` over every
tracked `*.ps1`, `*.psm1` and `*.psd1` when a PR into `master`/`dev` touches one, or on manual dispatch.

- Findings in files the PR changes are annotated (GitHub shows about 10 per type per step).
- The job summary counts all findings in the repo by severity and by rule, with the top files; the full
  list is the `psscriptanalyzer-results` CSV artifact.
- Rules are configured in `PSScriptAnalyzerSettings.psd1` at the repo root. Each excluded rule has a
  one-line reason there.
- Locally: `Invoke-ScriptAnalyzer -Path <file> -Settings ./PSScriptAnalyzerSettings.psd1`.

---

## Required Secrets/Variables

| Name | Type | Required For | Description |
|------|------|--------------|-------------|
| `VIRUSTOTAL_API_KEY` | Secret | VirusTotal scanning | API key for VirusTotal |
| `VT_API_KEY` | Variable | Manual release VT scan | Same as above, for manual workflow |
| `VBR_HOST` | Secret | Integration tests | VBR server hostname |
| `VBR_USER` | Secret | Integration tests | VBR username |
| `VBR_PASSWORD` | Secret | Integration tests | VBR password |
| `RELEASE_APP_ID` | Environment secret (`beta`, `beta-manual`) | Beta release | GitHub App that may create/delete `v*` tags |
| `RELEASE_APP_PRIVATE_KEY` | Environment secret (`beta`, `beta-manual`) | Beta release | Private key for that app |

## Self-Hosted Runners

Integration tests require self-hosted runners with these labels:
- `CONSOLE_HOST` - VBR v13
- `VBR12_HOST` - VBR v12
- `VBR13SQL_HOST` - VBR v13 with SQL

If runners aren't configured, integration tests will be skipped and releases will proceed normally.

## Versioning

Release versions are computed from commits by `.github/scripts/Get-VhcVersion.ps1` (see
[ADR 0031](../../docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md) and
[ADR 0032](../../docs/adr/0032-base-tag-is-highest-ga-tag.md)). `ci-cd.yaml`,
`manual-release.yml`, `pr-release-prep.yml` and `sbom-generation.yml` all call it. Two exceptions:
`pr-release-prep.yml` falls back to the csproj version when a fork's branch lacks the script, and
`sbom-generation.yml` uses the release tag's version on `release` events.

- `Major.Minor.Patch` comes from the Conventional Commits since the last GA tag
  (`!:` / `BREAKING CHANGE:` = major, `feat:` = minor, anything else = patch), raised to the csproj
  `Major.Minor` floor, or set by a `Release-As: X.Y.Z` commit footer. With no non-merge commits since
  the last GA tag the bump is `none`: the version is that tag's `Major.Minor.Patch` (raised to the floor).
- `Release-As:` and `BREAKING CHANGE:` footers are read from non-merge commits only (`git log --no-merges`),
  so a footer in a merge commit message is ignored. The highest `Release-As` wins and overrides the
  commit-derived bump. A malformed one (non-numeric, out of range, non-ASCII digits) fails the compute
  step with a `Malformed Release-As footer` error. An out-of-range stray tag is ignored when choosing the
  last GA tag.
- `Revision` is a run number: `ci-cd.yaml`, `pr-release-prep.yml` and `version-preview.yml` use their own
  `github.run_number`; `manual-release.yml` and the non-release path of `sbom-generation.yml` use the
  latest `ci-cd.yaml` run number.
- Any job that calls the script needs `actions/checkout` with `fetch-depth: 0` so tags and history exist.
- Release tags are created on the commit that was built (`target_commitish`), not the default branch tip.

### Version Preview (`version-preview.yml`)

Runs on PRs into `master` or `dev` and on manual dispatch. It prints the version the commit would get,
without building anything or touching the lab runners. Check the job summary. Only `Major.Minor.Patch`
is meaningful: the Revision is this workflow's own run number. On dispatch the preview uses the `dev`
channel, which only affects `notes_from_tag`.

### Commit Lint (`commit-lint.yml`)

Advisory: annotates PR titles and commits that are not Conventional Commits (an untyped commit counts
as a patch), use an unknown commit type, or carry a malformed `Release-As:` footer. It never fails the PR. To enforce it, add `-Strict` to the script call and mark the check
required in branch protection.

### Pester tests

`.github/scripts/VhcVersion.Tests.ps1` runs in `pester-tests.yml`. Locally:
`pwsh -NoProfile -Command "Invoke-Pester -Path .github/scripts/VhcVersion.Tests.ps1 -Output Detailed"`.
