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

---

## Required Secrets/Variables

| Name | Type | Required For | Description |
|------|------|--------------|-------------|
| `VIRUSTOTAL_API_KEY` | Secret | VirusTotal scanning | API key for VirusTotal |
| `VT_API_KEY` | Variable | Manual release VT scan | Same as above, for manual workflow |
| `VBR_HOST` | Secret | Integration tests | VBR server hostname |
| `VBR_USER` | Secret | Integration tests | VBR username |
| `VBR_PASSWORD` | Secret | Integration tests | VBR password |

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
`manual-release.yml`, `pr-release-prep.yml` and `sbom-generation.yml` all call it, so they agree.

- `Major.Minor.Patch` comes from the Conventional Commits since the last GA tag
  (`!:` / `BREAKING CHANGE:` = major, `feat:` = minor, anything else = patch), raised to the csproj
  `Major.Minor` floor, or set by a `Release-As: X.Y.Z` commit footer.
- `Revision` is the `ci-cd.yaml` run number.
- Any job that calls the script needs `actions/checkout` with `fetch-depth: 0` so tags and history exist.
- Release tags are created on the commit that was built (`target_commitish`), not the default branch tip.

### Version Preview (`version-preview.yml`)

Runs on every PR and on demand. It prints the version the commit would get, without building anything
or touching the lab runners. Check the job summary.

### Commit Lint (`commit-lint.yml`)

Advisory: annotates PR titles and commits that are not Conventional Commits (an untyped commit counts
as a patch). It never fails the PR. To enforce it, add `-Strict` to the script call and mark the check
required in branch protection.

### Pester tests

`.github/scripts/VhcVersion.Tests.ps1` runs in `pester-tests.yml`. Locally:
`pwsh -NoProfile -Command "Invoke-Pester -Path .github/scripts/VhcVersion.Tests.ps1 -Output Detailed"`.
