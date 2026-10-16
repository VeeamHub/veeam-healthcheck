# Contributing

We welcome contributions — bug reports, fixes, new features, and documentation improvements.

## How to Contribute

All contributions to this repository must be signed as described in the [Developer Certificate of Origin](https://github.com/VeeamHub/veeam-healthcheck/blob/master/DCO.md). Your signature certifies that you wrote the contribution or have the right to pass it on as an open source contribution.

### Report a Bug or Request a Feature

[Open a new issue](https://github.com/VeeamHub/veeam-healthcheck/issues/new/choose) — it's that easy.

### Submit a Fix or Feature

1. Fork the repository
2. Create a feature branch from `dev`
3. Write your change with tests where applicable
4. Follow the [test naming convention](architecture/index.md#testing): `[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`
5. Open a pull request against `dev` (`master` is the release branch and only takes release PRs from `dev`)

## Development Setup

```bash
# Restore dependencies
dotnet restore vHC/HC.sln

# Build (debug)
dotnet build vHC/HC.sln --configuration Debug

# Run tests (Windows, macOS, Linux; Windows-only tests skip elsewhere)
dotnet test vHC/VhcXTests/VhcXTests.csproj
```

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/) — `feat:`, `fix:`, `chore:`, `ci:`, `test:`, `docs:`. Feature commits (`feat:`) appear in the auto-generated [Feature Timeline](timeline.md).

The prefix also decides the next release version: `feat:` bumps the minor version, `type!:` or a `BREAKING CHANGE:` footer bumps the major version, and everything else bumps the patch. If your PR may be squash-merged, make the PR title a Conventional Commit, because the title is what gets read. The `Commit Lint` check on PRs only warns; it never blocks a merge.

## Changelog

User-visible changes are recorded in [ChangeLog.md](https://github.com/VeeamHub/veeam-healthcheck/blob/master/ChangeLog.md), which follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). If your PR changes what a user sees or gets (new or changed report content, CLI arguments, behaviour, a fix for a reported problem, a security fix), add one line under `## [Unreleased]` in the matching group (`Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`) and link the issue when there is one. Refactors, tests, CI and docs-only changes do not need an entry.

Edit `ChangeLog.md` only. This site's [Changelog](changelog.md) page is regenerated from it by CI.

## Experimental Features

Work that is not ready for general use ships hidden behind the `VHC_EXPERIMENTS` environment variable. The values `1` and `true` (case-insensitive) turn it on. Anything else, including `0`, `false`, `yes` or leaving it unset, keeps it off. The variable is read from the environment of the process that launches `VeeamHealthCheck.exe`, so set it in the same shell session. In code, check `CFeatureFlags.ExperimentsEnabled`.

Currently gated:

- **Orphaned & Superseded Backups** (VBR report section, its left-nav link, and the `OrphanedSupersededBackups` / `OrphanedBackupsSweepEvaluated` JSON fields). The collector (`Get-VhcOrphanedSupersededBackups`) still runs; only the report output is hidden. Gate sites: `CHtmlBodyHelper.OrphanedSupersededBackupsTable` and `CHtmlCompiler.BuildSidebar`.

When a feature graduates, delete its `CFeatureFlags.ExperimentsEnabled` checks and its entry in this list.

## Live VBR Integration Tests Before Merge

The `CI/CD Pipeline` workflow (`.github/workflows/ci-cd.yaml`) runs the tool against real Veeam Backup & Replication servers on self-hosted runners. It triggers on pushes to `master` and `dev`, so a pull request gets no live run by default. To run it against your branch before merging:

```bash
gh workflow run "CI/CD Pipeline" --ref <your-branch>
gh run list --workflow "CI/CD Pipeline" --branch <your-branch> --limit 1
```

It builds your branch, then runs the three integration jobs. Release publishing is gated on `master` (release) and `dev` (prerelease), so a feature-branch run publishes nothing. Never dispatch it with `--ref master` or `--ref dev` unless you mean to release.

The jobs assert on the output, not only on the exit code: core CSVs exist and are non-empty, the Jobs CSV has rows, the HTML report has its core sections, and on `vbr-2` the JSON, PDF and PPTX exports are checked and a second `/scrub:true` pass (with `VHC_EXPERIMENTS=1`) confirms the real hostname is absent and the Orphaned & Superseded Backups section is present. The shared checks are in `.github/scripts/Test-VhcLiveRun.ps1`.

### Security & Compliance Analyzer label triage

Each live job runs a deterministic (no AI) SCA label triage after the report is built. Console labels for the Security & Compliance Analyzer rules live in `VbrConfig.json` (`SecurityComplianceRuleNames`); when a VBR release adds or renames a rule, the report falls back to a PascalCase label.

- **Drift means evaluated rules only.** The rules a server actually evaluated (`*_SecurityCompliance.csv`, the same rows the report shows) count. An evaluated rule is `missing` (no map entry) or `label-changed` (its VBR console label differs from the map). Enum values the server never evaluates are `dormant`: they appear in the job summary and never open an issue.
- **Exact labels come from a join.** `*_SecurityCompliance.csv` carries the SDK rule `RuleId`; the VBR REST `GET /api/v1/securityAnalyzer/bestPractices` returns the same GUID with the full console label. Each job writes `sca-triage.json` (evaluated rules with verdict, console and map labels, plus dormant enums and the VBR version) as an `sca-triage-<server>` artifact. If REST is unreachable, credentials are missing, or the server's API has no analyzer endpoint (possible on v12), the job falls back to a CSV-only verdict with a notice and never fails.
- **One proposer.** The `sca-drift-proposal` job unions the artifacts. On `dev` and `master` it opens or updates one draft PR from `bot/sca-labels` into `dev` (label `AI-assisted`) that edits `SecurityComplianceRuleNames` with the exact console labels and bumps `SecurityComplianceRulesValidatedForVbrVersion`, and one `sca-drift` issue linking it; a clean run closes both. Everywhere else it is report-only: the proposed `VbrConfig.json` is in the job summary and the `sca-proposal` artifact. If the repo setting that lets Actions create PRs is off, the exact changes go into the issue body instead. A human reviews and merges the PR.
- Code: `.github/scripts/Invoke-SecurityComplianceDriftCheck.ps1` (per server), `Invoke-ScaDriftProposal.ps1` (proposer), `ScaTriage.psm1` (join and patch logic), with Pester tests beside them.

## License

By contributing, you agree that your contributions will be licensed under the project's [MIT License](https://github.com/VeeamHub/veeam-healthcheck/blob/master/LICENSE).
