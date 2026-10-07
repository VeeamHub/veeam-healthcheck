# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution (plain build works cross-platform; only the
# self-contained single-file Windows publish requires Windows)
dotnet build vHC/HC.sln --configuration Debug

# Build release version
dotnet build vHC/HC.sln --configuration Release

# Restore dependencies
dotnet restore vHC/HC.sln
```

## Test Commands

```bash
# Run all tests (cross-platform; genuinely Windows-only tests are marked
# [WindowsOnlyFact] and skip automatically off Windows)
dotnet test vHC/VhcXTests/VhcXTests.csproj

# Run specific test class
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "FullyQualifiedName~CredentialHelperTests"

# Run tests by category
dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "Category=Integration"

# Run with code coverage
dotnet test vHC/VhcXTests/VhcXTests.csproj --collect:"XPlat Code Coverage"
```

## Architecture Overview

Veeam Health Check is a Windows utility that generates configuration reports for Veeam Backup & Replication (VBR) and Veeam Backup for Microsoft 365 (VB365) installations.

### Three-Phase Pipeline

```
Collection → Processing/Analysis → Report Generation
```

### Key Components

**Entry Point & Flow**
- `vHC/HC_Reporting/Startup/EntryPoint.cs` - Main entry, handles single-file deployment
- `vHC/HC_Reporting/Startup/CArgsParser.cs` - CLI argument parsing, routes to GUI or CLI mode
- `vHC/HC_Reporting/VhcGui.axaml.cs` - Avalonia GUI for interactive use

**Global State**
- `vHC/HC_Reporting/Common/CGlobals.cs` - Central static configuration class holding all execution flags, paths, and shared data

**Data Collection** (`Functions/Collection/`)
- Multi-source collection: PowerShell scripts, SQL queries, registry reads, log parsing, WMI
- PowerShell scripts in `Tools/Scripts/HealthCheck/VBR/` and `Tools/Scripts/HealthCheck/VB365/`
- Outputs CSV files to `C:\temp\vHC\Original\{VBR|VB365}\{servername}\{timestamp}\`
- **`CGlobals.REMOTEEXEC` does not mean scripts run on the remote server.** vHC always launches `pwsh.exe` and the `Veeam.Backup.PowerShell` module on the *local* machine; `REMOTEEXEC` only changes which server the module connects to (`-Server` / `-VBOServerFqdnOrIp`) and whether explicit credentials are required instead of Windows auth. Any preflight check on the local PowerShell/module install (e.g. version checks) must run regardless of `REMOTEEXEC` — it is never "the remote machine's problem."

**Report Generation** (`Functions/Reporting/`)
- `Functions/Reporting/Html/VBR/CHtmlCompiler.cs` - VBR report compiler
- `Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs` - VB365 report compiler
- `Functions/Reporting/CReportModeSelector.cs` - Routes to correct compiler based on detected product

**Data Processing**
- `Functions/Reporting/CsvHandlers/CCsvReader.cs` - CSV reading with CsvHelper
- `Functions/Reporting/CsvHandlers/CCsvParser.cs` - Static methods returning dynamic objects for flexible CSV parsing
- `Functions/Reporting/DataFormers/CDataFormer.cs` - Transforms raw data into typed report objects

### VBR vs VB365 Separation

Product detection happens in `CClientFunctions.ModeCheck()` by scanning running processes:
- `Veeam.Backup.Service` → VBR mode
- `Veeam.Archiver.Service` → VB365 mode

Each product has separate:
- Collection scripts in `Tools/Scripts/HealthCheck/`
- HTML compilers in `Functions/Reporting/Html/`
- Table renderers in `Functions/Reporting/Html/VBR/VbrTables/` and `Functions/Reporting/Html/VB365/`

### Export Formats

- HTML (primary) - with embedded CSS/JavaScript
- PDF - via DinkToPdf
- PowerPoint - via HtmlToOpenXml
- Scrubbed mode - anonymizes IPs, server names, credentials via `CScrubHandler`

## Tech Stack

- **.NET 8.0** targeting Windows 7.0+ (`net8.0-windows7.0`)
- **Avalonia** for GUI
- **PowerShell 7 SDK** for embedded script execution
- **CsvHelper** for CSV processing
- **xUnit + Moq** for testing
- **DocumentFormat.OpenXml + HtmlToOpenXml** for Office exports

## Test Naming Convention

`[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`

Example: `EscapePasswordForPowerShell_SpecialCharacters_ProperlyEscapes`

## Commit / PR Convention: `Fixes #N`

When a commit or PR resolves an open GitHub issue, the commit message OR the PR body **must** include one of GitHub's auto-close keywords followed by the issue number:

- `Fixes #N`
- `Closes #N`
- `Resolves #N`

This is what the release-notes generator scrapes to populate the **🐛 Issues Resolved** section of the GitHub release, and it's what GitHub uses to auto-close the issue when the PR merges to `master`.

**Do not use** `(#N)` shorthand alone — that's GitHub's PR-reference syntax and does not auto-close anything. Past releases (e.g., v3.0.1.169) shipped fixes for #112, #152, #155 that had to be closed manually because the commits used `(#112)` or omitted the link.

Examples:

```
# good — auto-closes #152 on merge to master
fix(outdir): update CGlobals.desiredPath immediately on /outdir argument

Fixes #152

# bad — references the issue but doesn't close it
fix(nas): fix Get-NasInfo.ps1 for VBR v13 (#112)
```

If a PR resolves multiple issues, list them: `Fixes #112, fixes #152, fixes #155`.

## Changelog

User-visible changes get one line under `## [Unreleased]` in `ChangeLog.md` (Keep a Changelog groups: Added, Changed, Deprecated, Removed, Fixed, Security), with the issue linked when there is one. Refactors, tests, CI and docs-only changes don't need an entry. Edit `ChangeLog.md` only; `docs/changelog.md` is regenerated by CI. Details are in `CONTRIBUTING.md`.

## Versioning

Release versions are computed from commits, not hand-edited ([ADR 0031](docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md), [ADR 0032](docs/adr/0032-base-tag-is-highest-ga-tag.md)). Format: `Major.Minor.Patch.Revision`, where Revision is a CI run number (the workflow's own for `ci-cd.yaml`; the latest `ci-cd.yaml` run for manual releases).

| Commit since the last GA tag | Bump |
|---|---|
| `type!:` subject or a `BREAKING CHANGE:` footer | major |
| `feat:` | minor |
| anything else (`fix:`, `chore:`, no type at all) | patch |
| no non-merge commits at all | none: the Base Tag's `Major.Minor.Patch` (raised to the floor), with the new Revision |

- The csproj `Major.Minor` is a **floor**: the result is never lower than `Major.Minor.0`. Raise it to force a bump.
- A `Release-As: X.Y.Z` commit footer overrides the commit-derived bump; the highest one across commits wins. It is rejected if below the floor or not above the last GA version. A malformed footer (non-numeric, out-of-range, non-ASCII digits) fails compute with a `Malformed Release-As footer` error; an out-of-range stray tag is ignored when choosing the Base Tag.
- `Release-As:` and `BREAKING CHANGE:` footers are read from non-merge commits only (`git log --no-merges`); a footer in a merge commit message is ignored.
- **Merge method matters.** `dev -> master` must be a merge commit so the individual commits survive. A PR squash-merged into `dev` becomes one commit: the type and `!` are read from its title (subject), so the title needs a Conventional Commits prefix (`feat:`, `fix:`, ...), while `Release-As:` / `BREAKING CHANGE:` footers are read from the squash commit body.
- **Hotfix:** branch from `master`, PR into `master`, then `git cherry-pick -x` the fix onto `dev`. The fix may appear in two versions' release notes.
- Dry run locally: `pwsh ./.github/scripts/Get-VhcVersion.ps1 -Revision 999 -Channel ga` (needs tags: `git fetch --tags`).
- The `Commit Lint` check on PRs is advisory; it annotates titles and commits that would count as an untyped patch, use an unknown commit type, or carry a malformed `Release-As:` footer.

## Important Notes

- Tests build and run on Windows, macOS, and Linux. `EnableWindowsTargeting=true` in `VeeamHealthCheck.csproj` is what lets the `net8.0-windows7.0` TFM build off-Windows at all, combined with the WPF → Avalonia GUI migration (PR #211) replacing the Windows-only UI toolkit. Genuinely Windows-only tests (DPAPI, registry, etc.) are marked `[WindowsOnlyFact]` and skip individually rather than gating the whole suite.
- A second test project, `VhcXTests.CrossPlatform` (`net10.0`), compiles a hand-picked subset of source files directly rather than referencing `VeeamHealthCheck.csproj`.
- Internal types exposed to `VhcXTests` via `InternalsVisibleTo` in csproj
- Local builds auto-increment the csproj build segment via `increment_version.ps1`; CI ignores it (see Versioning). Revert the csproj after building: `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`
- Suppressed code analysis warnings: CA1305, CA1307, CA1820, CA2242, CA1031, CA1806, CA1822
