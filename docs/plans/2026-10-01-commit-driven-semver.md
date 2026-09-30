# Commit-Driven Semantic Versioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compute every release version (`Major.Minor.Patch.Revision`) from Conventional Commits and the last GA tag in one shared script, used by all four workflows that need a version, and tag releases on the commit that was built.

**Architecture:** A PowerShell module (`VhcVersion.psm1`) holds pure, unit-tested functions plus a thin git layer; a CLI wrapper (`Get-VhcVersion.ps1`) publishes the result as GitHub step outputs. `Base Tag` is the highest reachable suffix-less GA tag, not `git describe`'s nearest tag. The csproj `Major.Minor` is a floor, `Release-As:` footers override. Commit-lint and a no-build version-preview workflow ship with it.

**Tech Stack:** PowerShell 7 (`pwsh`), Pester 5+, git, GitHub Actions (`windows-latest`, `ubuntu-latest`), `actionlint`.

**Decisions and rationale:** [ADR 0031](../adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md), [ADR 0032](../adr/0032-base-tag-is-highest-reachable-ga-tag.md), terms in [`CONTEXT.md`](../../CONTEXT.md), and the settled thread on issue #244. Do not re-litigate them here.

---

## Read first

**Work happens on `feature/commit-driven-semver-244`** (branched from `origin/dev`; it already holds the two ADRs, the glossary commit and this plan). Stay in `/Users/b.thomas/git/github/veeamhub/veeam-healthcheck`. Do not use `EnterWorktree` (RTK hook conflict).

**Conventions for every commit in this plan:**
- One commit per task step that says "Commit"; do not batch.
- Conventional Commits subject; body lines `Refs #244`; end the message with the session's `Co-Authored-By` trailer.
- Never commit an auto-bumped `vHC/HC_Reporting/VeeamHealthCheck.csproj` (this plan never builds the solution, so it should not appear). If `git status` shows it modified: `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`.
- Never push to `dev` or `master` directly.

**Sequencing with other work:** #245 (`VbrVersionSupportCheck` fix) lands before this PR or alongside it. The GUI PR #247 is **held until this PR is merged**, then updated (see the last task).

**Decisions this plan takes that the issue thread did not cover** (review these in the PR):

| # | Decision | Why |
|---|---|---|
| D1 | `manual-release.yml` takes its revision from the latest `ci-cd.yaml` run number (`gh run list`), not its own `github.run_number`. | `run_number` is per workflow and would restart at 1, so manual builds would sort below CI builds of the same `Major.Minor.Patch`. |
| D2 | Manual pre-releases are tagged `vX.Y.Z.R-rc`. | Today they get an unsuffixed tag; the strict GA pattern would then treat a pre-release as a Base Tag. |
| D3 | `manual-release.yml` keeps `version_override`, now validated (four-part, above the last GA version). It also now passes `-p:Version/AssemblyVersion/FileVersion` to `dotnet publish`. | Approved as an escape hatch. The publish flags were missing, so a manual release's exe carried the csproj version while the tag carried something else. |
| D4 | `sbom-generation.yml` uses the release tag's version on `release` events and computes otherwise. | Recomputing on a release event would label the SBOM with a version the release does not have. |
| D5 | `pr-release-prep.yml` falls back to the csproj version when the fork's branch lacks the script. | It checks out the fork's head, which can predate this change. |
| D6 | Dev release notes start at the highest reachable tag of either kind (GA or `-dev`). ADR 0032's sentence is updated to say so. | `git describe`'s "nearest" is unreliable for the reasons in ADR 0032. |
| D7 | `actions: read` is added to the permissions of `manual-release.yml` and the SBOM job. | Both set explicit permissions and call `gh run list`. |
| D8 | New `version-preview.yml` (PR + manual dispatch, no build). | Dispatching `ci-cd.yaml` from a branch starts the three self-hosted VBR lab jobs (`if: github.event_name != 'pull_request'`), so it cannot serve as a dry run. This proves the script on a real Windows runner with the real tags before merge. |

## File structure

| File | Action | Responsibility |
|---|---|---|
| `.github/scripts/VhcVersion.psm1` | create | Pure functions (tag selection, bump, Release-As, floor, resolve, override, commit-lint rules) and the git layer. One module, no I/O to GitHub. |
| `.github/scripts/VhcVersion.Tests.ps1` | create | Pester: pure-function tests plus throwaway-git-repo scenarios (hotfix topology included). |
| `.github/scripts/Get-VhcVersion.ps1` | create | CLI wrapper: parameters, console summary, `GITHUB_OUTPUT` / step summary. |
| `.github/scripts/Invoke-VhcCommitLint.ps1` | create | CLI wrapper for commit-lint annotations. |
| `.github/workflows/commit-lint.yml` | create | Advisory PR check. |
| `.github/workflows/version-preview.yml` | create | No-build version preview on PRs and dispatch. |
| `.github/workflows/pester-tests.yml` | modify | Path filters plus a `pester-vhcversion` job. |
| `.github/workflows/ci-cd.yaml` | modify | Full-history checkout, compute step, notes range, release `target_commitish`. |
| `.github/workflows/manual-release.yml` | modify | Shared script, revision source, `-rc`, publish flags, `--target`. |
| `.github/workflows/pr-release-prep.yml`, `sbom-generation.yml` | modify | Shared script. |
| `CLAUDE.md`, `.github/workflows/README.md`, `CONTEXT.md`, ADR 0031/0032 | modify | Docs. |

---

### Task 1: Write the failing tests

**Files:**
- Create: `.github/scripts/VhcVersion.Tests.ps1`

- [ ] **Step 1: Confirm the branch**

Run: `git branch --show-current && git status --short`
Expected: `feature/commit-driven-semver-244` and no output from `git status`.

- [ ] **Step 2: Create the test file**

```powershell
#Requires -Version 7
# Pester tests for VhcVersion.psm1 (docs/adr/0031, 0032).
# Pure functions are tested directly; version computation is tested against
# throwaway git repositories that mimic the real tag shapes.

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot 'VhcVersion.psm1') -Force

    function New-TestRepo {
        param([string]$Floor = '3.0')
        $path = Join-Path ([IO.Path]::GetTempPath()) ("vhcver-" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $path | Out-Null
        git -C $path init -q -b master | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $path 'vHC/HC_Reporting') -Force | Out-Null
        Set-Content -Path (Join-Path $path 'vHC/HC_Reporting/VeeamHealthCheck.csproj') -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyVersion>$Floor.0.0</AssemblyVersion>
    <FileVersion>$Floor.0.0</FileVersion>
  </PropertyGroup>
</Project>
"@
        $path
    }

    function Add-TestCommit {
        param([string]$Repo, [string]$Subject, [string]$Body = '', [string]$Tag)
        $args = @('-C', $Repo, '-c', 'user.name=t', '-c', 'user.email=t@example.com', '-c', 'commit.gpgsign=false',
            'commit', '-q', '--allow-empty', '-m', $Subject)
        if ($Body) { $args += @('-m', $Body) }
        git @args | Out-Null
        if ($Tag) { git -C $Repo tag $Tag | Out-Null }
    }

    function Merge-TestBranch {
        param([string]$Repo, [string]$Branch)
        git -C $Repo -c user.name=t -c user.email=t@example.com -c commit.gpgsign=false merge --no-ff -q -m "Merge $Branch" $Branch | Out-Null
    }
}

Describe 'Select-VhcHighestTag' {
    It 'ignores -dev tags, the stray beta tag and legacy tags' {
        $tags = 'v3.0.1.193', 'v3.0.1.225-dev', 'v3.0.2-beta.1', 'v2.0.660', '2.0.0.546', 'v1'
        (Select-VhcHighestTag -Tags $tags).Name | Should -Be 'v3.0.1.193'
    }
    It 'orders numerically, not lexically' {
        (Select-VhcHighestTag -Tags 'v3.0.1.99', 'v3.0.1.193', 'v3.0.1.100').Name | Should -Be 'v3.0.1.193'
    }
    It 'includes -dev tags when asked' {
        (Select-VhcHighestTag -Tags 'v3.0.1.193', 'v3.0.1.225-dev' -IncludeDev).Name | Should -Be 'v3.0.1.225-dev'
    }
    It 'returns $null when nothing matches' {
        Select-VhcHighestTag -Tags 'v3.0.2-beta.1' | Should -BeNullOrEmpty
        Select-VhcHighestTag -Tags @() | Should -BeNullOrEmpty
    }
}

Describe 'Get-VhcCommitBump' {
    It 'is none with no commits' {
        Get-VhcCommitBump -Commits @() | Should -Be 'none'
    }
    It 'is patch for fixes and untyped commits' {
        Get-VhcCommitBump -Commits @(
            [pscustomobject]@{ Subject = 'fix(x): a'; Body = '' },
            [pscustomobject]@{ Subject = 'opus code review findings'; Body = '' }) | Should -Be 'patch'
    }
    It 'is minor when any commit is a feat' {
        Get-VhcCommitBump -Commits @(
            [pscustomobject]@{ Subject = 'fix: a'; Body = '' },
            [pscustomobject]@{ Subject = 'feat(gui): b'; Body = '' }) | Should -Be 'minor'
    }
    It 'is major for a bang type' {
        Get-VhcCommitBump -Commits @([pscustomobject]@{ Subject = 'refactor(api)!: drop x'; Body = '' }) | Should -Be 'major'
    }
    It 'is major for a BREAKING CHANGE footer' {
        Get-VhcCommitBump -Commits @([pscustomobject]@{ Subject = 'fix: a'; Body = "details`n`nBREAKING CHANGE: gone" }) | Should -Be 'major'
    }
    It 'does not treat a mid-line BREAKING CHANGE mention as a footer' {
        Get-VhcCommitBump -Commits @([pscustomobject]@{ Subject = 'fix: a'; Body = 'this is not a BREAKING CHANGE: really' }) | Should -Be 'patch'
    }
}

Describe 'Get-VhcReleaseAs' {
    It 'returns $null when no footer exists' {
        Get-VhcReleaseAs -Commits @([pscustomobject]@{ Subject = 'fix: a'; Body = 'text' }) | Should -BeNullOrEmpty
    }
    It 'returns the highest footer across commits' {
        $r = Get-VhcReleaseAs -Commits @(
            [pscustomobject]@{ Subject = 'a'; Body = "x`n`nRelease-As: 3.4.0" },
            [pscustomobject]@{ Subject = 'b'; Body = "Release-As: 4.0.0`r`n" })
        $r | Should -Be ([version]'4.0.0')
    }
    It 'throws on a malformed footer' {
        { Get-VhcReleaseAs -Commits @([pscustomobject]@{ Subject = 'a'; Body = 'Release-As: v4' }) } | Should -Throw '*Malformed Release-As*'
    }
}

Describe 'Resolve-VhcVersion' {
    It 'applies the bump to the base' {
        (Resolve-VhcVersion -Base '3.0.1' -Bump patch -Floor '3.0.0').Version | Should -Be ([version]'3.0.2')
        (Resolve-VhcVersion -Base '3.0.1' -Bump minor -Floor '3.0.0').Version | Should -Be ([version]'3.1.0')
        (Resolve-VhcVersion -Base '3.4.7' -Bump major -Floor '3.0.0').Version | Should -Be ([version]'4.0.0')
    }
    It 'keeps the base when there are no commits' {
        $r = Resolve-VhcVersion -Base '3.0.1' -Bump none -Floor '3.0.0'
        $r.Version | Should -Be ([version]'3.0.1')
        $r.Reason | Should -Be 'none'
    }
    It 'lets the floor raise the result' {
        $r = Resolve-VhcVersion -Base '3.0.1' -Bump patch -Floor '3.1.0'
        $r.Version | Should -Be ([version]'3.1.0')
        $r.Reason | Should -Be 'floor'
    }
    It 'does not let the floor interfere once satisfied' {
        (Resolve-VhcVersion -Base '3.1.0' -Bump patch -Floor '3.1.0').Version | Should -Be ([version]'3.1.1')
    }
    It 'lets Release-As override the commits' {
        $r = Resolve-VhcVersion -Base '3.0.1' -Bump patch -Floor '3.0.0' -ReleaseAs '4.0.0'
        $r.Version | Should -Be ([version]'4.0.0')
        $r.Reason | Should -Be 'release-as'
    }
    It 'rejects Release-As below the floor' {
        { Resolve-VhcVersion -Base '3.0.1' -Bump patch -Floor '3.1.0' -ReleaseAs '3.0.5' } | Should -Throw '*below the csproj floor*'
    }
    It 'rejects Release-As not above the base' {
        { Resolve-VhcVersion -Base '3.1.0' -Bump patch -Floor '3.0.0' -ReleaseAs '3.1.0' } | Should -Throw '*not above the last GA*'
    }
}

Describe 'Assert-VhcVersionOverride' {
    It 'accepts a four-part version above the base' {
        Assert-VhcVersionOverride -Override '3.2.0.5' -BaseTagVersion ([version]'3.1.0.240') | Should -Be ([version]'3.2.0.5')
    }
    It 'rejects a non four-part version' {
        { Assert-VhcVersionOverride -Override '3.2.0' } | Should -Throw '*four numeric parts*'
    }
    It 'rejects a version not above the last GA' {
        { Assert-VhcVersionOverride -Override '3.1.0.240' -BaseTagVersion ([version]'3.1.0.240') } | Should -Throw '*not above*'
    }
    It 'accepts anything well-formed when no GA tag exists' {
        Assert-VhcVersionOverride -Override '1.0.0.1' | Should -Be ([version]'1.0.0.1')
    }
}

Describe 'Get-VhcVersionInfo (synthetic repos)' {
    AfterEach { if ($script:repo -and (Test-Path $script:repo)) { Remove-Item -Recurse -Force $script:repo } }

    It 'uses the floor when no tags exist' {
        $script:repo = New-TestRepo -Floor '3.1'
        Add-TestCommit -Repo $repo -Subject 'fix: first'
        $i = Get-VhcVersionInfo -Revision 7 -RepoPath $repo
        $i.Version | Should -Be '3.1.0.7'
        $i.Reason | Should -Be 'floor'
        $i.BaseTag | Should -Be ''
    }

    It 'computes a minor bump from feat commits since the GA tag' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: old' -Tag 'v3.0.1.193'
        Add-TestCommit -Repo $repo -Subject 'feat: new thing'
        Add-TestCommit -Repo $repo -Subject 'fix: later'
        $i = Get-VhcVersionInfo -Revision 300 -RepoPath $repo
        $i.Version | Should -Be '3.1.0.300'
        $i.BaseTag | Should -Be 'v3.0.1.193'
        $i.CommitCount | Should -Be 2
    }

    It 'computes a patch bump from fixes only' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: old' -Tag 'v3.0.1.193'
        Add-TestCommit -Repo $repo -Subject 'fix: later'
        (Get-VhcVersionInfo -Revision 9 -RepoPath $repo).Version | Should -Be '3.0.2.9'
    }

    It 'ignores -dev and stray tags when choosing the base, and uses the newest -dev tag for dev notes' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.193'
        Add-TestCommit -Repo $repo -Subject 'fix: b' -Tag 'v3.0.2-beta.1'
        Add-TestCommit -Repo $repo -Subject 'fix: c' -Tag 'v3.0.1.225-dev'
        Add-TestCommit -Repo $repo -Subject 'feat: d'
        $ga  = Get-VhcVersionInfo -Revision 1 -RepoPath $repo -Channel ga
        $dev = Get-VhcVersionInfo -Revision 1 -RepoPath $repo -Channel dev
        $ga.BaseTag | Should -Be 'v3.0.1.193'
        $ga.NotesFromTag | Should -Be 'v3.0.1.193'
        $ga.CommitCount | Should -Be 3
        $dev.NotesFromTag | Should -Be 'v3.0.1.225-dev'
        $dev.Version | Should -Be $ga.Version
    }

    It 'honours a Release-As footer' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.193'
        Add-TestCommit -Repo $repo -Subject 'chore: bump' -Body 'Release-As: 4.0.0'
        (Get-VhcVersionInfo -Revision 5 -RepoPath $repo).Version | Should -Be '4.0.0.5'
    }

    It 'applies and validates a version override' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.193'
        Add-TestCommit -Repo $repo -Subject 'feat: b'
        $i = Get-VhcVersionInfo -Revision 5 -RepoPath $repo -VersionOverride '3.5.0.1'
        $i.Version | Should -Be '3.5.0.1'
        $i.Reason | Should -Be 'override'
        { Get-VhcVersionInfo -Revision 5 -RepoPath $repo -VersionOverride '3.0.1.100' } | Should -Throw '*not above*'
    }

    It 'bases on the hotfix tag after dev is merged into master' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'chore: init'
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.225'
        git -C $repo checkout -q -b dev
        Add-TestCommit -Repo $repo -Subject 'feat: new thing' -Tag 'v3.1.0.250-dev'
        git -C $repo checkout -q master
        git -C $repo checkout -q -b hotfix
        Add-TestCommit -Repo $repo -Subject 'fix: critical'
        git -C $repo checkout -q master
        Merge-TestBranch -Repo $repo -Branch hotfix
        git -C $repo tag 'v3.0.2.260'
        Merge-TestBranch -Repo $repo -Branch dev
        $i = Get-VhcVersionInfo -Revision 300 -RepoPath $repo
        $i.BaseTag | Should -Be 'v3.0.2.260'
        $i.CommitCount | Should -Be 1
        $i.Version | Should -Be '3.1.0.300'
    }

    It 'bumps patch past the hotfix when dev only has fixes' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.225'
        git -C $repo checkout -q -b dev
        Add-TestCommit -Repo $repo -Subject 'fix: dev fix'
        git -C $repo checkout -q master
        git -C $repo checkout -q -b hotfix
        Add-TestCommit -Repo $repo -Subject 'fix: critical'
        git -C $repo checkout -q master
        Merge-TestBranch -Repo $repo -Branch hotfix
        git -C $repo tag 'v3.0.2.260'
        Merge-TestBranch -Repo $repo -Branch dev
        (Get-VhcVersionInfo -Revision 300 -RepoPath $repo).Version | Should -Be '3.0.3.300'
    }
}

Describe 'Test-VhcCommitMessage' {
    It 'accepts conventional subjects' {
        Test-VhcCommitMessage -Subject 'feat(gui): add thing' | Should -BeNullOrEmpty
        Test-VhcCommitMessage -Subject 'fix!: drop x' | Should -BeNullOrEmpty
        Test-VhcCommitMessage -Subject 'ci(version): compute from commits' | Should -BeNullOrEmpty
    }
    It 'accepts a git-generated revert subject' {
        Test-VhcCommitMessage -Subject 'Revert "fix(json): x"' | Should -BeNullOrEmpty
    }
    It 'flags an untyped subject' {
        @(Test-VhcCommitMessage -Subject 'opus code review findings').Count | Should -Be 1
    }
    It 'flags a malformed Release-As footer' {
        @(Test-VhcCommitMessage -Subject 'chore: x' -Body 'Release-As: v4').Count | Should -Be 1
    }
    It 'accepts a valid Release-As footer' {
        Test-VhcCommitMessage -Subject 'chore: x' -Body "note`n`nRelease-As: 4.0.0" | Should -BeNullOrEmpty
    }
}
```

- [ ] **Step 3: Run the tests and watch them fail**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path .github/scripts/VhcVersion.Tests.ps1 -Output Normal"`
Expected: the run fails with an error naming `VhcVersion.psm1` (the module does not exist yet), 0 tests passed.

---

### Task 2: Implement the module

**Files:**
- Create: `.github/scripts/VhcVersion.psm1`

- [ ] **Step 1: Create the module**

```powershell
# VhcVersion.psm1
# Commit-driven release version computation for vHC. See docs/adr/0031 and 0032.
#
#   Version = Major.Minor.Patch.Revision
#   Major.Minor.Patch : computed from the Conventional Commits since the Base Tag,
#                       raised to the csproj Major.Minor floor, or set by a
#                       'Release-As: X.Y.Z' commit footer.
#   Revision          : the CI run number, passed in by the caller.
#   Base Tag          : the highest reachable GA tag (vMajor.Minor.Patch.Revision,
#                       no suffix), NOT `git describe`'s nearest tag.

Set-StrictMode -Version Latest

$script:GaTagPattern  = '^v(\d+)\.(\d+)\.(\d+)\.(\d+)$'
$script:AnyTagPattern = '^v(\d+)\.(\d+)\.(\d+)\.(\d+)(-dev)?$'
$script:BumpRank      = @{ none = 0; patch = 1; minor = 2; major = 3 }
$script:CommitTypes   = 'feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert'

function ConvertTo-VhcTagInfo {
    [CmdletBinding()]
    param([string]$Name, [string]$Pattern)

    $m = [regex]::Match($Name, $Pattern)
    if (-not $m.Success) { return $null }
    [pscustomobject]@{
        Name    = $Name
        Version = [version]"$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value).$($m.Groups[4].Value)"
    }
}

function Select-VhcHighestTag {
    # Highest-versioned tag by numeric order. GA tags only unless -IncludeDev.
    [CmdletBinding()]
    param([string[]]$Tags, [switch]$IncludeDev)

    $pattern = if ($IncludeDev) { $script:AnyTagPattern } else { $script:GaTagPattern }
    $found = @($Tags | ForEach-Object { ConvertTo-VhcTagInfo -Name $_ -Pattern $pattern } |
        Where-Object { $null -ne $_ })
    if ($found.Count -eq 0) { return $null }
    $found | Sort-Object -Property Version -Descending | Select-Object -First 1
}

function Get-VhcCommitBump {
    # Highest bump implied by the commits: none | patch | minor | major.
    [CmdletBinding()]
    param([object[]]$Commits)

    $bump = 'none'
    foreach ($c in $Commits) {
        $subject = "$($c.Subject)"
        $body    = "$($c.Body)"
        if ($subject -match '^[A-Za-z]+(\([^)]*\))?!:' -or $body -match '(?m)^BREAKING[ -]CHANGE:') {
            $level = 'major'
        } elseif ($subject -match '^feat(\([^)]*\))?:') {
            $level = 'minor'
        } else {
            $level = 'patch'
        }
        if ($script:BumpRank[$level] -gt $script:BumpRank[$bump]) { $bump = $level }
    }
    $bump
}

function Get-VhcReleaseAs {
    # Highest 'Release-As: X.Y.Z' footer across the commits, or $null. Throws on a malformed footer.
    [CmdletBinding()]
    param([object[]]$Commits)

    $highest = $null
    foreach ($c in $Commits) {
        foreach ($line in ("$($c.Body)" -split "`r?`n")) {
            if ($line -notmatch '^\s*Release-As\s*:') { continue }
            if ($line -notmatch '^Release-As: (\d+)\.(\d+)\.(\d+)\s*$') {
                throw "Malformed Release-As footer '$($line.Trim())' (expected 'Release-As: X.Y.Z')."
            }
            $v = [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
            if ($null -eq $highest -or $v -gt $highest) { $highest = $v }
        }
    }
    $highest
}

function Get-VhcFloor {
    # The csproj Major.Minor as a three-part version Major.Minor.0.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CsprojPath)

    $csproj = [xml](Get-Content -LiteralPath $CsprojPath -Raw)
    $group  = @($csproj.Project.PropertyGroup)[0]
    $parts  = "$($group.AssemblyVersion)".Split('.')
    if ($parts.Count -lt 2) { throw "No AssemblyVersion with Major.Minor found in $CsprojPath." }
    [version]"$($parts[0]).$($parts[1]).0"
}

function Resolve-VhcVersion {
    # Combine base, bump, floor and Release-As into a three-part version plus the reason.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][version]$Base,
        [Parameter(Mandatory)][ValidateSet('none', 'patch', 'minor', 'major')][string]$Bump,
        [Parameter(Mandatory)][version]$Floor,
        [version]$ReleaseAs
    )

    if ($null -ne $ReleaseAs) {
        if ($ReleaseAs -lt $Floor) { throw "Release-As $ReleaseAs is below the csproj floor $Floor." }
        if ($ReleaseAs -le $Base)  { throw "Release-As $ReleaseAs is not above the last GA version $Base." }
        return [pscustomobject]@{ Version = $ReleaseAs; Reason = 'release-as' }
    }

    $derived = switch ($Bump) {
        'major' { [version]::new($Base.Major + 1, 0, 0) }
        'minor' { [version]::new($Base.Major, $Base.Minor + 1, 0) }
        'patch' { [version]::new($Base.Major, $Base.Minor, $Base.Build + 1) }
        'none'  { $Base }
    }
    if ($Floor -gt $derived) { return [pscustomobject]@{ Version = $Floor; Reason = 'floor' } }
    [pscustomobject]@{ Version = $derived; Reason = $Bump }
}

function Assert-VhcVersionOverride {
    # Validate a manual four-part override; it must be above the last GA version when one exists.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Override, [version]$BaseTagVersion)

    if ($Override -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Version override '$Override' must be four numeric parts (Major.Minor.Patch.Revision)."
    }
    $v = [version]$Override
    if ($null -ne $BaseTagVersion -and $v -le $BaseTagVersion) {
        throw "Version override $Override is not above the last GA version $BaseTagVersion."
    }
    $v
}

function Invoke-VhcGit {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepoPath, [Parameter(Mandatory)][string[]]$Arguments)

    $out = & git -C $RepoPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $out" }
    $out
}

function Get-VhcCommitsSince {
    # Non-merge commits reachable from $Ref and not from $From (whole history when $From is empty).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepoPath, [Parameter(Mandatory)][string]$Ref, [string]$From)

    $range = if ($From) { "$From..$Ref" } else { $Ref }
    $raw = (Invoke-VhcGit -RepoPath $RepoPath -Arguments @('log', $range, '--no-merges', '--pretty=format:%H%x1f%s%x1f%b%x1e')) -join "`n"
    $records = $raw -split [char]0x1E
    foreach ($record in $records) {
        if ([string]::IsNullOrWhiteSpace($record)) { continue }
        $f = $record -split [char]0x1F, 3
        [pscustomobject]@{
            Sha     = $f[0].Trim()
            Subject = $f[1].Trim()
            Body    = $(if ($f.Count -gt 2) { $f[2] } else { '' })
        }
    }
}

function Get-VhcVersionInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Revision,
        [string]$Ref = 'HEAD',
        [string]$RepoPath = '.',
        [string]$CsprojPath,
        [ValidateSet('ga', 'dev')][string]$Channel = 'ga',
        [string]$VersionOverride
    )

    if (-not $CsprojPath) { $CsprojPath = Join-Path $RepoPath 'vHC/HC_Reporting/VeeamHealthCheck.csproj' }

    $tags     = @(Invoke-VhcGit -RepoPath $RepoPath -Arguments @('tag', '--merged', $Ref))
    $baseTag  = Select-VhcHighestTag -Tags $tags
    $baseName = if ($baseTag) { $baseTag.Name } else { '' }
    $base3    = if ($baseTag) { [version]::new($baseTag.Version.Major, $baseTag.Version.Minor, $baseTag.Version.Build) } else { [version]'0.0.0' }

    $commits = @(Get-VhcCommitsSince -RepoPath $RepoPath -Ref $Ref -From $baseName)
    $floor   = Get-VhcFloor -CsprojPath $CsprojPath

    if ($VersionOverride) {
        $baseFull = if ($baseTag) { $baseTag.Version } else { $null }
        $version  = (Assert-VhcVersionOverride -Override $VersionOverride -BaseTagVersion $baseFull).ToString()
        $reason   = 'override'
        $bump     = 'n/a'
    } else {
        $bump      = Get-VhcCommitBump -Commits $commits
        $releaseAs = Get-VhcReleaseAs -Commits $commits
        $resolved  = Resolve-VhcVersion -Base $base3 -Bump $bump -Floor $floor -ReleaseAs $releaseAs
        $v         = $resolved.Version
        $version   = "$($v.Major).$($v.Minor).$($v.Build).$Revision"
        $reason    = $resolved.Reason
    }

    $notesFrom = $baseName
    if ($Channel -eq 'dev') {
        $anyTag = Select-VhcHighestTag -Tags $tags -IncludeDev
        $notesFrom = if ($anyTag) { $anyTag.Name } else { '' }
    }

    [pscustomobject]@{
        Version      = $version
        Reason       = $reason
        Bump         = $bump
        BaseTag      = $baseName
        NotesFromTag = $notesFrom
        Floor        = $floor.ToString()
        CommitCount  = $commits.Count
        Channel      = $Channel
    }
}

function Test-VhcCommitMessage {
    # Conventional Commits problems for one message; an empty result means it is fine.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Subject, [string]$Body = '')

    $problems = [System.Collections.Generic.List[string]]::new()
    $isRevert = $Subject -match '^Revert "'
    if (-not $isRevert -and $Subject -notmatch "^($script:CommitTypes)(\([^)]+\))?!?: \S") {
        $problems.Add("Subject '$Subject' is not '<type>(<scope>)!: <description>' (types: $($script:CommitTypes -replace '\|', ', ')).")
    }
    foreach ($line in ("$Body" -split "`r?`n")) {
        if ($line -match '^\s*Release-As\s*:' -and $line -notmatch '^Release-As: \d+\.\d+\.\d+\s*$') {
            $problems.Add("Malformed footer '$($line.Trim())' (expected 'Release-As: X.Y.Z').")
        }
    }
    $problems.ToArray()
}

Export-ModuleMember -Function Select-VhcHighestTag, Get-VhcCommitBump, Get-VhcReleaseAs, Get-VhcFloor, Resolve-VhcVersion, Assert-VhcVersionOverride, Invoke-VhcGit, Get-VhcCommitsSince, Get-VhcVersionInfo, Test-VhcCommitMessage
```

- [ ] **Step 2: Run the tests**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path .github/scripts/VhcVersion.Tests.ps1 -Output Normal"`
Expected: `Tests Passed: 37, Failed: 0, Skipped: 0`.

- [ ] **Step 3: Commit**

```bash
git add .github/scripts/VhcVersion.psm1 .github/scripts/VhcVersion.Tests.ps1
git commit -m "ci(version): add commit-driven version module with tests" -m "Refs #244"
```

---

### Task 3: CLI wrapper and real-tag check

**Files:**
- Create: `.github/scripts/Get-VhcVersion.ps1`

- [ ] **Step 1: Create the wrapper**

```powershell
#Requires -Version 7
# Get-VhcVersion.ps1
# Computes the vHC release version from commits (docs/adr/0031, 0032) and, with
# -GitHubOutput, publishes it to the calling workflow step.
#
# USAGE (workflow step, full history + tags required: actions/checkout fetch-depth: 0):
#   ./.github/scripts/Get-VhcVersion.ps1 -Revision ${{ github.run_number }} -Channel ga -GitHubOutput
#
# Step outputs: version, base_tag, notes_from_tag, reason.
# EXIT CODES: 0 on success; non-zero (terminating error) on a malformed or invalid
# Release-As footer or version override, so a bad version never reaches a release.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$Revision,
    [ValidateSet('ga', 'dev')][string]$Channel = 'ga',
    [string]$Ref = 'HEAD',
    [string]$RepoPath = '.',
    [string]$CsprojPath,
    [string]$VersionOverride,
    [switch]$GitHubOutput
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'VhcVersion.psm1') -Force

$params = @{ Revision = $Revision; Channel = $Channel; Ref = $Ref; RepoPath = $RepoPath }
if ($CsprojPath)      { $params.CsprojPath = $CsprojPath }
if ($VersionOverride) { $params.VersionOverride = $VersionOverride }
$info = Get-VhcVersionInfo @params

Write-Host "Version:        $($info.Version)"
Write-Host "Reason:         $($info.Reason) (bump: $($info.Bump), csproj floor: $($info.Floor))"
Write-Host "Base tag:       $(if ($info.BaseTag) { $info.BaseTag } else { '<none>' }) ($($info.CommitCount) commits since)"
Write-Host "Notes from tag: $(if ($info.NotesFromTag) { $info.NotesFromTag } else { '<none>' }) (channel: $($info.Channel))"

if ($GitHubOutput) {
    if (-not $env:GITHUB_OUTPUT) { throw '-GitHubOutput was given but GITHUB_OUTPUT is not set.' }
    Add-Content -Path $env:GITHUB_OUTPUT -Encoding UTF8 -Value @(
        "version=$($info.Version)",
        "base_tag=$($info.BaseTag)",
        "notes_from_tag=$($info.NotesFromTag)",
        "reason=$($info.Reason)"
    )
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding UTF8 -Value @(
            '## Computed version',
            '',
            "**$($info.Version)** ($($info.Reason); bump: $($info.Bump); csproj floor: $($info.Floor))",
            '',
            "- Base tag: ``$($info.BaseTag)`` ($($info.CommitCount) commits since)",
            "- Release notes start at: ``$($info.NotesFromTag)`` ($($info.Channel) channel)"
        )
    }
}

$info
```

- [ ] **Step 2: Fetch tags, then run it against the real repository**

Run:
```bash
git fetch origin --tags --quiet
pwsh -NoProfile -File ./.github/scripts/Get-VhcVersion.ps1 -Revision 999 -Channel ga -Ref origin/dev
pwsh -NoProfile -File ./.github/scripts/Get-VhcVersion.ps1 -Revision 999 -Channel dev -Ref origin/dev | head -4
```
Expected on the first command (`origin/dev` as of 2026-10-01; later commits can change the counts but not the shape): `Version: 3.1.0.999`, `Reason: minor`, `Base tag: v3.0.1.193 (50 commits since)`, `Notes from tag: v3.0.1.193`. Expected on the second: `Notes from tag` is the newest `-dev` tag (`v3.0.1.225-dev` on 2026-10-01) and the version is the same as the first.

- [ ] **Step 3: Check the failure path exits non-zero**

Run: `pwsh -NoProfile -File ./.github/scripts/Get-VhcVersion.ps1 -Revision 1 -Ref origin/dev -VersionOverride 3.0.1.100; echo "exit=$?"`
Expected: an error containing `is not above the last GA version`, and `exit=1`.

- [ ] **Step 4: Commit**

```bash
git add .github/scripts/Get-VhcVersion.ps1
git commit -m "ci(version): add Get-VhcVersion.ps1 CLI wrapper" -m "Refs #244"
```

---

### Task 4: Run the Pester tests in CI

**Files:**
- Modify: `.github/workflows/pester-tests.yml`

- [ ] **Step 1: Add the path filters**

In `.github/workflows/pester-tests.yml` (`replace_all: true`; 2 occurrences), replace:

```yaml
      - 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-NasInfo.Tests.ps1'
```

with:

```yaml
      - 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-NasInfo.Tests.ps1'
      - '.github/scripts/VhcVersion.psm1'
      - '.github/scripts/VhcVersion.Tests.ps1'
```

- [ ] **Step 2: Append the job**

Append this to the end of `.github/workflows/pester-tests.yml` (the file ends with a newline; the snippet starts with a blank line):

```yaml

  pester-vhcversion:
    name: Pester — VhcVersion
    runs-on: windows-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4

      - name: Ensure Pester v5 is available
        shell: pwsh
        run: |
          $pester = Get-Module -ListAvailable -Name Pester | Where-Object { $_.Version -ge '5.0.0' } | Select-Object -First 1
          if (-not $pester) {
            Write-Host 'Pester v5 not found — installing from PSGallery...'
            Install-Module -Name Pester -MinimumVersion 5.0.0 -Scope CurrentUser -Force -SkipPublisherCheck
          } else {
            Write-Host "Pester $($pester.Version) already available"
          }

      - name: Run Pester tests
        shell: pwsh
        run: |
          Import-Module Pester -MinimumVersion 5.0.0
          $config = New-PesterConfiguration
          $config.Run.Path = '.github/scripts/VhcVersion.Tests.ps1'
          $config.Run.Exit = $true
          $config.Output.Verbosity = 'Detailed'
          Invoke-Pester -Configuration $config
```

- [ ] **Step 3: Lint**

Run: `actionlint -no-color .github/workflows/pester-tests.yml; echo "exit=$?"`
Expected: `exit=0`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/pester-tests.yml
git commit -m "ci(version): run the VhcVersion Pester tests in CI" -m "Refs #244"
```

---

### Task 5: Use the script in `ci-cd.yaml`

**Files:**
- Modify: `.github/workflows/ci-cd.yaml`

Seven edits. Each `old` block below was checked to match exactly once (whitespace-only lines are significant: use the Edit tool with the exact text).

- [ ] **Step 1: Expose `notes_from_tag` and fetch full history**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
      version: ${{ steps.get_version.outputs.version }}
    
    steps:
    - name: Checkout repository
      uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
      
    - name: Setup .NET```

with:

```yaml
      version: ${{ steps.get_version.outputs.version }}
      notes_from_tag: ${{ steps.get_version.outputs.notes_from_tag }}
    
    steps:
    - name: Checkout repository
      uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
      with:
        fetch-depth: 0  # full history and tags: the version is computed from commits since the last GA tag
      
    - name: Setup .NET```

- [ ] **Step 2: Fix the stale comment**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
    # Version is managed in VeeamHealthCheck.csproj and auto-incremented on every build
    # Version increments via increment_version script during build process
```

with:

```yaml
    # The release version is computed from commits by .github/scripts/Get-VhcVersion.ps1
    # (the "Compute version from commits" step below). The csproj Major.Minor is only a floor.
```

- [ ] **Step 3: Replace the version step**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
    - name: Get version from assembly
      id: get_version
      run: |
        $csproj = [xml](Get-Content vHC/HC_Reporting/VeeamHealthCheck.csproj)
        $baseVersion = $csproj.Project.PropertyGroup[0].AssemblyVersion

        # Parse base version (e.g., 3.0.0.501) and replace last segment with run number
        $versionParts = $baseVersion.Split('.')
        # Use format: Major.Minor.Patch.RunNumber (e.g., 3.0.1.45)
        $version = "$($versionParts[0]).$($versionParts[1]).1.${{ github.run_number }}"

        echo "version=$version" >> $env:GITHUB_OUTPUT
        echo "Base version: $baseVersion"
        echo "Release version: $version (using run number ${{ github.run_number }})"
```

with:

```yaml
    - name: Compute version from commits
      id: get_version
      shell: pwsh
      run: |
        # master builds GA releases; every other ref (dev, manual dispatch) builds dev pre-releases.
        $channel = if ('${{ github.ref }}' -eq 'refs/heads/master') { 'ga' } else { 'dev' }
        ./.github/scripts/Get-VhcVersion.ps1 -Revision ${{ github.run_number }} -Channel $channel -GitHubOutput
```

- [ ] **Step 4: GA release notes range**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
        # Find last release tag
        $lastTag = git describe --tags --abbrev=0 2>$null
```

with:

```yaml
        # Start of the notes range: the Base Tag, computed once in build-and-test (ADR 0032)
        $lastTag = "${{ needs.build-and-test.outputs.notes_from_tag }}"
```

- [ ] **Step 5: Dev release notes range**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
        # Find last release tag to scope changelog
        $lastTag = git describe --tags --abbrev=0 2>$null
```

with:

```yaml
        # Start of the notes range: the most recent GA or -dev tag, computed in build-and-test
        $lastTag = "${{ needs.build-and-test.outputs.notes_from_tag }}"
```

- [ ] **Step 6: Tag the commit that was built (GA)**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
        tag_name: v${{ needs.build-and-test.outputs.version }}
        name: Veeam Health Check v${{ needs.build-and-test.outputs.version }}
```

with:

```yaml
        tag_name: v${{ needs.build-and-test.outputs.version }}
        target_commitish: ${{ github.sha }}  # tag the commit that was built, not the default branch tip (ADR 0032)
        name: Veeam Health Check v${{ needs.build-and-test.outputs.version }}
```

- [ ] **Step 7: Tag the commit that was built (dev)**

In `.github/workflows/ci-cd.yaml`, replace:

```yaml
        tag_name: v${{ needs.build-and-test.outputs.version }}-dev
```

with:

```yaml
        tag_name: v${{ needs.build-and-test.outputs.version }}-dev
        target_commitish: ${{ github.sha }}
```

- [ ] **Step 8: Lint against the baseline**

`actionlint` reports findings in this file on `origin/dev` already (a self-hosted runner label and shellcheck noise on PowerShell blocks). The check is that this change adds none. Run the comparison in Task 10, Step 5 once all workflow edits are in, or confirm now that `git diff` shows only the seven edits above.

- [ ] **Step 9: Commit**

```bash
git add .github/workflows/ci-cd.yaml
git commit -m "ci(version): compute the release version from commits in ci-cd.yaml" -m "Also start release notes from the computed tag and tag the built commit (ADR 0032)." -m "Refs #244"
```

---

### Task 6: Use the script in `manual-release.yml`

**Files:**
- Modify: `.github/workflows/manual-release.yml`

- [ ] **Step 1: Permissions (`gh run list` needs `actions: read`)**

In `.github/workflows/manual-release.yml`, replace:

```yaml
permissions:
  contents: write
  pull-requests: read
```

with:

```yaml
permissions:
  actions: read
  contents: write
  pull-requests: read
```

- [ ] **Step 2: Input descriptions**

In `.github/workflows/manual-release.yml`, replace:

```yaml
        description: 'Version override (leave empty to use AssemblyVersion from csproj)'
```

with:

```yaml
        description: 'Version override, four parts e.g. 3.2.0.5 (leave empty to compute from commits)'
```

In `.github/workflows/manual-release.yml`, replace:

```yaml
        description: 'Mark as pre-release (beta/RC from dev branch)'
```

with:

```yaml
        description: 'Mark as pre-release (tagged -rc; build from a non-master branch)'
```

- [ ] **Step 3: Version step**

In `.github/workflows/manual-release.yml`, replace:

```yaml
    - name: Get version
      id: get_version
      shell: pwsh
      env:
        VERSION_OVERRIDE: ${{ inputs.version_override }}
      run: |
        if ("$env:VERSION_OVERRIDE" -ne "") {
          $version = "$env:VERSION_OVERRIDE"
          Write-Host "Using override version: $version"
        } else {
          $csproj = [xml](Get-Content vHC/HC_Reporting/VeeamHealthCheck.csproj)
          $version = $csproj.Project.PropertyGroup[0].AssemblyVersion
          Write-Host "Using AssemblyVersion from csproj: $version"
        }
        "version=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
```

with:

```yaml
    - name: Get version
      id: get_version
      shell: pwsh
      env:
        GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        VERSION_OVERRIDE: ${{ inputs.version_override }}
        IS_PRERELEASE: ${{ inputs.prerelease }}
      run: |
        # github.run_number is per workflow and would restart at 1 here, so take the revision
        # from the ci-cd.yaml counter to keep manual builds ordered with CI builds.
        $revision = gh run list --workflow ci-cd.yaml --limit 1 --json number --jq '.[0].number'
        if (-not $revision) { throw 'Could not read the latest ci-cd.yaml run number.' }

        $params = @{
          Revision     = [int]$revision
          Channel      = $(if ($env:IS_PRERELEASE -eq 'true') { 'dev' } else { 'ga' })
          GitHubOutput = $true
        }
        if ($env:VERSION_OVERRIDE) { $params.VersionOverride = $env:VERSION_OVERRIDE }
        $info = ./.github/scripts/Get-VhcVersion.ps1 @params

        # Manual pre-releases are tagged -rc so they never count as a Base Tag.
        $tag = "v$($info.Version)" + $(if ($env:IS_PRERELEASE -eq 'true') { '-rc' } else { '' })
        "tag=$tag" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
```

- [ ] **Step 4: Stamp the built exe with the computed version**

In `.github/workflows/manual-release.yml`, replace:

```yaml
    - name: Publish single-file executable
      shell: pwsh
      run: |
        dotnet publish vHC/HC_Reporting/VeeamHealthCheck.csproj `
          -c Release `
          -r win-x64 `
          --self-contained true `
          -p:PublishSingleFile=true `
          -p:IncludeNativeLibrariesForSelfExtract=true `
          -p:EnableCompressionInSingleFile=true `
          -o publish/out
```

with:

```yaml
    - name: Publish single-file executable
      shell: pwsh
      run: |
        $version = "${{ steps.get_version.outputs.version }}"
        dotnet publish vHC/HC_Reporting/VeeamHealthCheck.csproj `
          -c Release `
          -r win-x64 `
          --self-contained true `
          -p:PublishSingleFile=true `
          -p:IncludeNativeLibrariesForSelfExtract=true `
          -p:EnableCompressionInSingleFile=true `
          -p:Version=$version `
          -p:AssemblyVersion=$version `
          -p:FileVersion=$version `
          -o publish/out
```

- [ ] **Step 5: Notes range**

In `.github/workflows/manual-release.yml`, replace:

```yaml
          $prevTag = git describe --tags --abbrev=0 HEAD 2>$null
          if ($LASTEXITCODE -eq 0 -and $prevTag) {
```

with:

```yaml
          $prevTag = "${{ steps.get_version.outputs.notes_from_tag }}"
          if ($prevTag) {
```

- [ ] **Step 6: Create the release on the built commit with the computed tag**

In `.github/workflows/manual-release.yml`, replace:

```yaml
        RELEASE_VERSION: ${{ steps.get_version.outputs.version }}
        IS_PRERELEASE: ${{ inputs.prerelease }}
        GH_REPO: ${{ github.repository }}
      run: |
        $version = $env:RELEASE_VERSION
        $zipPath = "publish/VeeamHealthCheck-$version.zip"
        $notesFile = "release_notes.md"

        $existingRelease = gh release view "v$version" 2>&1
        if ($LASTEXITCODE -eq 0) {
          Write-Host "Release v$version already exists. Updating..."
          gh release edit "v$version" --notes-file $notesFile
          gh release upload "v$version" $zipPath --clobber
        } else {
          Write-Host "Creating new release v$version..."
          $prereleaseFlag = if ($env:IS_PRERELEASE -eq "true") { "--prerelease" } else { "" }
          gh release create "v$version" --title "Veeam Health Check v$version" --notes-file $notesFile $zipPath $prereleaseFlag
        }
        Write-Host "Release URL: https://github.com/$env:GH_REPO/releases/tag/v$version"
```

with:

```yaml
        RELEASE_VERSION: ${{ steps.get_version.outputs.version }}
        RELEASE_TAG: ${{ steps.get_version.outputs.tag }}
        IS_PRERELEASE: ${{ inputs.prerelease }}
        GH_REPO: ${{ github.repository }}
      run: |
        $version = $env:RELEASE_VERSION
        $tag = $env:RELEASE_TAG
        $zipPath = "publish/VeeamHealthCheck-$version.zip"
        $notesFile = "release_notes.md"

        $existingRelease = gh release view $tag 2>&1
        if ($LASTEXITCODE -eq 0) {
          Write-Host "Release $tag already exists. Updating..."
          gh release edit $tag --notes-file $notesFile
          gh release upload $tag $zipPath --clobber
        } else {
          Write-Host "Creating new release $tag..."
          $prereleaseFlag = if ($env:IS_PRERELEASE -eq "true") { "--prerelease" } else { "" }
          # --target tags the commit that was built, not the default branch tip (ADR 0032)
          gh release create $tag --target $env:GITHUB_SHA --title "Veeam Health Check v$version" --notes-file $notesFile $zipPath $prereleaseFlag
        }
        Write-Host "Release URL: https://github.com/$env:GH_REPO/releases/tag/$tag"
```

- [ ] **Step 7: Confirm the `gh run list` call works**

Run: `gh run list --workflow ci-cd.yaml --limit 1 --json number --jq '.[0].number'`
Expected: a single integer (225 on 2026-10-01).

- [ ] **Step 8: Commit**

```bash
git add .github/workflows/manual-release.yml
git commit -m "ci(version): compute the manual release version from commits" -m "Validated version_override, -rc tag for pre-releases, exe stamped with the release version, tag on the built commit." -m "Refs #244"
```

---

### Task 7: `pr-release-prep.yml` and `sbom-generation.yml`

**Files:**
- Modify: `.github/workflows/pr-release-prep.yml`
- Modify: `.github/workflows/sbom-generation.yml`

- [ ] **Step 1: `pr-release-prep.yml`** (its checkout already has `fetch-depth: 0`)

In `.github/workflows/pr-release-prep.yml`, replace:

```yaml
    - name: Get version from assembly
      id: get_version
      run: |
        $csproj = [xml](Get-Content vHC/HC_Reporting/VeeamHealthCheck.csproj)
        $version = $csproj.Project.PropertyGroup[0].AssemblyVersion
        echo "version=$version" >> $env:GITHUB_OUTPUT
        echo "Version: $version"
```

with:

```yaml
    - name: Compute version from commits
      id: get_version
      shell: pwsh
      run: |
        # A fork's branch may predate the version script; fall back to the csproj version then.
        if (Test-Path ./.github/scripts/Get-VhcVersion.ps1) {
          ./.github/scripts/Get-VhcVersion.ps1 -Revision ${{ github.run_number }} -Channel dev -GitHubOutput
        } else {
          $csproj = [xml](Get-Content vHC/HC_Reporting/VeeamHealthCheck.csproj)
          $version = $csproj.Project.PropertyGroup[0].AssemblyVersion
          "version=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
          Write-Host "Version (csproj fallback): $version"
        }
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/pr-release-prep.yml
git commit -m "ci(version): compute the PR build version from commits" -m "Refs #244"
```

- [ ] **Step 3: `sbom-generation.yml` permissions and full-history checkout**

In `.github/workflows/sbom-generation.yml`, replace:

```yaml
    permissions:
      contents: write
      
    steps:
      - name: Checkout repository
        uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
```

with:

```yaml
    permissions:
      actions: read
      contents: write
      
    steps:
      - name: Checkout repository
        uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0
```

- [ ] **Step 4: `sbom-generation.yml` version step**

In `.github/workflows/sbom-generation.yml`, replace:

```yaml
      - name: Get version
        id: get_version
        run: |
          $csproj = [xml](Get-Content vHC/HC_Reporting/VeeamHealthCheck.csproj)
          $version = $csproj.Project.PropertyGroup[0].AssemblyVersion
          echo "version=$version" >> $env:GITHUB_OUTPUT
          echo "Version: $version"
```

with:

```yaml
      - name: Get version
        id: get_version
        shell: pwsh
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          EVENT_NAME: ${{ github.event_name }}
          RELEASE_TAG: ${{ github.event.release.tag_name }}
        run: |
          if ($env:EVENT_NAME -eq 'release') {
            # The release already has its version; do not compute a different one.
            $version = $env:RELEASE_TAG.TrimStart('v')
          } else {
            $revision = gh run list --workflow ci-cd.yaml --limit 1 --json number --jq '.[0].number'
            $info = ./.github/scripts/Get-VhcVersion.ps1 -Revision ([int]$revision) -Channel ga
            $version = $info.Version
          }
          "version=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
          Write-Host "Version: $version"
```

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/sbom-generation.yml
git commit -m "ci(version): label the SBOM with the release version" -m "Refs #244"
```

---

### Task 8: Commit lint (advisory)

**Files:**
- Create: `.github/scripts/Invoke-VhcCommitLint.ps1`
- Create: `.github/workflows/commit-lint.yml`

The rules live in `Test-VhcCommitMessage` (already tested in Task 1).

- [ ] **Step 1: Create the script**

```powershell
#Requires -Version 7
# Invoke-VhcCommitLint.ps1
# Reports Conventional Commits problems in a PR title and its commits (docs/adr/0031).
# Commit types drive the release version, so an untyped commit silently counts as a patch.
#
# Advisory by default: problems become ::warning:: annotations and the exit code is 0.
# With -Strict they become ::error:: annotations and the script exits 1.
#
# USAGE (workflow step, full history: actions/checkout fetch-depth: 0):
#   ./.github/scripts/Invoke-VhcCommitLint.ps1 -Title $env:PR_TITLE -BaseSha $env:BASE_SHA -HeadSha $env:HEAD_SHA

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Title,
    [Parameter(Mandatory)][string]$BaseSha,
    [Parameter(Mandatory)][string]$HeadSha,
    [string]$RepoPath = '.',
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'VhcVersion.psm1') -Force

$level    = if ($Strict) { 'error' } else { 'warning' }
$problems = 0

foreach ($p in @(Test-VhcCommitMessage -Subject $Title)) {
    Write-Host "::$level title=PR title::$p"
    $problems++
}
foreach ($c in @(Get-VhcCommitsSince -RepoPath $RepoPath -Ref $HeadSha -From $BaseSha)) {
    foreach ($p in @(Test-VhcCommitMessage -Subject $c.Subject -Body $c.Body)) {
        Write-Host "::$level title=Commit $($c.Sha.Substring(0, 7))::$p"
        $problems++
    }
}

$mode = if ($Strict) { 'strict' } else { 'advisory' }
Write-Host "Commit lint ($mode): $problems problem(s)."
if ($Strict -and $problems -gt 0) { exit 1 }
exit 0
```

- [ ] **Step 2: Create the workflow**

```yaml
name: Commit Lint

# Advisory: reports Conventional Commits problems as annotations and never fails the PR.
# Commit types drive the release version (docs/adr/0031), so an untyped commit silently
# counts as a patch. To enforce, add -Strict to the script call and make this check required
# in branch protection.

on:
  pull_request:
    types: [opened, edited, synchronize, reopened]
    branches: [master, dev]

permissions:
  contents: read

jobs:
  commit-lint:
    name: Conventional Commits (advisory)
    runs-on: ubuntu-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0

      - name: Lint PR title and commits
        shell: pwsh
        env:
          PR_TITLE: ${{ github.event.pull_request.title }}
          BASE_SHA: ${{ github.event.pull_request.base.sha }}
          HEAD_SHA: ${{ github.event.pull_request.head.sha }}
        run: ./.github/scripts/Invoke-VhcCommitLint.ps1 -Title $env:PR_TITLE -BaseSha $env:BASE_SHA -HeadSha $env:HEAD_SHA
```

- [ ] **Step 3: Try it on real history**

Run: `pwsh -NoProfile -File ./.github/scripts/Invoke-VhcCommitLint.ps1 -Title 'opus findings' -BaseSha origin/dev~40 -HeadSha origin/dev; echo "exit=$?"`
Expected: `::warning` lines for the title and for the two untyped commits (`opus code review findings`, `Fable 5 code review findings`), then `Commit lint (advisory): 3 problem(s).` and `exit=0`. The git-generated `Revert "..."` commit is not flagged.

- [ ] **Step 4: Check strict mode fails**

Run: `pwsh -NoProfile -File ./.github/scripts/Invoke-VhcCommitLint.ps1 -Title 'bad title' -BaseSha origin/dev~1 -HeadSha origin/dev -Strict; echo "exit=$?"`
Expected: an `::error title=PR title::` line and `exit=1`.

- [ ] **Step 5: Lint and commit**

```bash
actionlint -no-color .github/workflows/commit-lint.yml
git add .github/scripts/Invoke-VhcCommitLint.ps1 .github/workflows/commit-lint.yml
git commit -m "ci(version): add advisory commit lint for PR titles and commits" -m "Refs #244"
```
Expected: `actionlint` prints nothing.

---

### Task 9: Version preview workflow

**Files:**
- Create: `.github/workflows/version-preview.yml`

- [ ] **Step 1: Create the workflow**

```yaml
name: Version Preview

# Shows the version the commit-driven scheme (docs/adr/0031) would compute for this commit,
# without building or releasing anything. Runs on every PR and on demand.
# The revision shown is this workflow's own run number, so only Major.Minor.Patch is meaningful.

on:
  pull_request:
    branches: [master, dev]
  workflow_dispatch:

permissions:
  contents: read

jobs:
  preview:
    name: Computed version
    runs-on: windows-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0  # full history and tags

      - name: Compute version
        shell: pwsh
        run: ./.github/scripts/Get-VhcVersion.ps1 -Revision ${{ github.run_number }} -Channel ${{ github.base_ref == 'master' && 'ga' || 'dev' }} -GitHubOutput
```

- [ ] **Step 2: Lint and commit**

```bash
actionlint -no-color .github/workflows/version-preview.yml
git add .github/workflows/version-preview.yml
git commit -m "ci(version): add a no-build version preview workflow" -m "Runs on PRs and on demand so the script is proven on a real runner without the lab jobs. Refs #244"
```
Expected: `actionlint` prints nothing.

---

### Task 10: Docs, ADR and glossary touch-ups, then whole-change checks

**Files:**
- Modify: `CLAUDE.md`, `.github/workflows/README.md`, `CONTEXT.md`
- Modify: `docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md`, `docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md`

- [ ] **Step 1: `CLAUDE.md`**

In `CLAUDE.md`, replace:

```markdown
## Important Notes

- Tests require Windows (WPF dependency) - non-Windows builds skip test compilation
```

with:

```markdown
## Versioning

Release versions are computed from commits, not hand-edited ([ADR 0031](docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md), [ADR 0032](docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md)). Format: `Major.Minor.Patch.Revision`, where Revision is the CI run number.

| Commit since the last GA tag | Bump |
|---|---|
| `type!:` subject or a `BREAKING CHANGE:` footer | major |
| `feat:` | minor |
| anything else (`fix:`, `chore:`, no type at all) | patch |

- The csproj `Major.Minor` is a **floor**: the result is never lower than `Major.Minor.0`. Raise it to force a bump.
- A `Release-As: X.Y.Z` commit footer overrides everything. It is rejected if below the floor or not above the last GA version.
- **Merge method matters.** `dev -> master` must be a merge commit so the individual commits survive. A PR squash-merged into `dev` contributes only its title, so the title needs a Conventional Commits prefix (`feat:`, `fix:`, ...).
- **Hotfix:** branch from `master`, PR into `master`, then `git cherry-pick -x` the fix onto `dev`. The fix may appear in two versions' release notes.
- Dry run locally: `pwsh ./.github/scripts/Get-VhcVersion.ps1 -Revision 999 -Channel ga` (needs tags: `git fetch --tags`).
- The `Commit Lint` check on PRs is advisory; it annotates titles and commits that would count as an untyped patch.

## Important Notes

- Tests require Windows (WPF dependency) - non-Windows builds skip test compilation
```

In `CLAUDE.md`, replace:

```markdown
- Version auto-increments on build via `increment_version.ps1`
```

with:

```markdown
- Local builds auto-increment the csproj build segment via `increment_version.ps1`; CI ignores it (see Versioning). Revert the csproj after building: `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`
```

- [ ] **Step 2: Workflows README**

In `.github/workflows/README.md`, replace:

```markdown
| `version_override` | No | Override version (e.g., `3.0.0.500`). Leave empty to use AssemblyVersion from csproj. |
```

with:

```markdown
| `version_override` | No | Override the version (four parts, e.g., `3.2.0.5`; must be above the last GA version). Leave empty to compute it from commits. |
| `prerelease` | No | Mark as pre-release. The tag gets an `-rc` suffix, so it never becomes a Base Tag. |
```

Then append this to the end of `.github/workflows/README.md` (it ends with a newline; the snippet starts with a blank line):

```markdown

## Versioning

Release versions are computed from commits by `.github/scripts/Get-VhcVersion.ps1` (see
[ADR 0031](../../docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md) and
[ADR 0032](../../docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md)). `ci-cd.yaml`,
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
```

- [ ] **Step 3: ADR and glossary wording**

In `docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md`, replace:

```markdown
it is the start of the GA release-notes range. Dev release notes instead
start from the nearest tag of any kind, including `-dev`, since they
describe what changed since the previous build.```

with:

```markdown
it is the start of the GA release-notes range. Dev release notes instead
start from the highest reachable tag of either kind (GA or `-dev`), since
they describe what changed since the previous build.```

In `docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md`, replace:

```markdown
- **The local auto-increment is untouched.**```

with:

```markdown
- **`manual-release.yml` keeps its `version_override` input** as an escape
  hatch, validated to be four-part and above the last GA version. Its default
  revision is the latest `ci-cd.yaml` run number (`run_number` is per workflow
  and would restart), and its pre-releases are tagged `-rc` so they never
  count as a Base Tag.
- **The local auto-increment is untouched.**```

In `CONTEXT.md`, replace:

```markdown
_Avoid_: Beta, RC, nightly (`v3.0.2-beta.1` is a stray tag, not a release
type; `manual-release.yml`'s "beta/RC" input is a separate manual path)
```

with:

```markdown
_Avoid_: Beta, RC, nightly (`v3.0.2-beta.1` is a stray tag, not a release
type; hand-cut prereleases are Manual Prereleases, below)

**Manual Prerelease**:
A prerelease cut by hand through `manual-release.yml`, tagged
`vMajor.Minor.Patch.Revision-rc`. It is never a Base Tag.
_Avoid_: Beta, RC build
```

- [ ] **Step 4: Commit the docs**

```bash
git add CLAUDE.md .github/workflows/README.md CONTEXT.md docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md
git commit -m "docs(version): document the commit-driven versioning workflow" -m "Refs #244"
```

- [ ] **Step 5: Whole-change lint against the baseline**

Run:
```bash
B=$(mktemp -d); mkdir -p "$B/.github/workflows"
FILES=(ci-cd.yaml manual-release.yml pr-release-prep.yml sbom-generation.yml pester-tests.yml)
WF=("${FILES[@]/#/.github/workflows/}")
for f in "${FILES[@]}"; do git show "origin/dev:.github/workflows/$f" > "$B/.github/workflows/$f"; done
norm() { sed -E 's#^[^ ]*/([A-Za-z0-9._-]+\.ya?ml):[0-9]+:[0-9]+:#\1:#; s#SC[0-9]+:[a-z]+:[0-9]+:[0-9]+:#SC:#' | grep -E '^[A-Za-z0-9._-]+\.ya?ml:' | sort | uniq -c | sort -rn; }
(cd "$B" && actionlint -no-color "${WF[@]}" 2>&1 | norm) > "$B/base.txt"
actionlint -no-color "${WF[@]}" 2>&1 | norm > "$B/new.txt"
wc -l "$B/base.txt" "$B/new.txt"
diff "$B/base.txt" "$B/new.txt" && echo "NO NEW actionlint findings"
actionlint -no-color .github/workflows/commit-lint.yml .github/workflows/version-preview.yml && echo "new workflows clean"
```
Expected: `NO NEW actionlint findings` and `new workflows clean`. `base.txt` and `new.txt` should each have about 20 lines (the existing findings); the point is that they are identical.

- [ ] **Step 6: Whole-change test run and cleanliness**

Run:
```bash
pwsh -NoProfile -Command "Invoke-Pester -Path .github/scripts/VhcVersion.Tests.ps1 -Output Normal"
git status --short
git diff origin/dev --stat
```
Expected: `Failed: 0`; `git status` empty; `git diff --stat` lists only the files in the File structure table plus the two ADRs, `CONTEXT.md`, and the files already committed earlier (no `VeeamHealthCheck.csproj`).

---

### Task 11: Open the PR, prove it on real runners, merge order

- [ ] **Step 1: Check for an existing PR and the base branch**

Run: `gh pr list --head feature/commit-driven-semver-244 --state all --json number,state,baseRefName`
Expected: `[]`. The base is `dev` (`gh repo view --json defaultBranchRef --jq .defaultBranchRef.name` prints `dev`).

- [ ] **Step 2: Push with an explicit upstream (the branch was created with `--no-track`)**

Run: `git push -u origin feature/commit-driven-semver-244`
Expected: the branch is created on `origin`; nothing is pushed to `dev` or `master`.

- [ ] **Step 3: Write the PR body to a scratch file, then open the PR**

Body:

```markdown
## Summary

Release versions are now computed from Conventional Commits and the last GA tag by one shared script, instead of `Major.Minor.1.<run_number>`. Design and rationale: [ADR 0031](docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md), [ADR 0032](docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md), and the decisions thread on #244.

- `.github/scripts/VhcVersion.psm1` + `Get-VhcVersion.ps1`: bump from commits, csproj `Major.Minor` floor, `Release-As:` override, Base Tag = highest reachable GA tag.
- Used by `ci-cd.yaml`, `manual-release.yml`, `pr-release-prep.yml`, `sbom-generation.yml`.
- Fixes two existing release bugs: GA notes started from the newest `-dev` tag (empty notes after a dev->master merge) and release tags landed on the default branch tip instead of the built commit.
- Advisory `Commit Lint` and a no-build `Version Preview` workflow.

First computed GA: `3.1.0` (base `v3.0.1.193`, three `feat` commits since).

## Decisions beyond the issue thread

D1-D8 are listed at the top of [the plan](docs/plans/2026-10-01-commit-driven-semver.md). Please look at D1 (manual-release revision from the latest `ci-cd.yaml` run), D2 (manual pre-releases tagged `-rc`) and D3 (`version_override` kept, validated).

## Testing

- 37 Pester tests (`VhcVersion.Tests.ps1`), including throwaway-repo scenarios for the stray `v3.0.2-beta.1` and `-dev` tags and the hotfix topology. They run in `pester-tests.yml`.
- Run against the real tags on 2026-10-01: `3.1.0.999` from `v3.0.1.193` + 50 commits; dev notes start at `v3.0.1.225-dev`.
- `actionlint`: no new findings against `origin/dev` (the existing ones are a self-hosted label and shellcheck noise on PowerShell blocks).
- **Not run before merge:** the changed `ci-cd.yaml` release jobs, `manual-release.yml`, `sbom-generation.yml` and `pr-release-prep.yml` paths. Dispatching `ci-cd.yaml` from a branch would start the self-hosted lab jobs, so the `Version Preview` check on this PR is the pre-merge proof of the script on a Windows runner. The release path is proven by the first push to `dev` after merge (checklist below).

## Rollout / rollback

After merge, on the first `dev` push: the computed version is `3.1.0.<run>`; `v3.1.0.<run>-dev` exists with `targetCommitish` equal to the pushed SHA; its notes start at `v3.0.1.225-dev`. Rollback: revert this PR.

## Things to know

- Needs #245 (the pre-v12 guard compares only the revision segment) fixed first or alongside: local builds at `3.1.0.x` bypass it.
- #246 (commit-hash local builds) and the local `increment_version` hooks are untouched here.
- `dev -> master` must stay a merge commit; a squash-merged PR into `dev` contributes only its title (documented in `CLAUDE.md`).
- The GUI PR #247 is held until this merges.

Fixes #244

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

Save the body to a file outside the repo, then run:
```bash
BODY_FILE=/path/to/that/file.md
gh pr create --base dev --head feature/commit-driven-semver-244 --title "ci(version): adopt commit-driven semantic versioning" --body-file "$BODY_FILE"
```
Expected: a PR URL. Do not merge; confirm the merge method with the user first. (If it is squash-merged, the `ci(version):` title counts as a patch; `dev` already holds `feat` commits, so the next GA is still a minor.)

- [ ] **Step 4: Watch the PR checks**

Run: `gh pr checks <PR number>`
Expected: `Computed version` (Version Preview) passes and its job summary shows `3.1.0.<run>` with base `v3.0.1.193`; `Conventional Commits (advisory)` passes and may annotate untyped commits; the Pester job passes; CodeQL/Semgrep/etc. run as usual. Open the Version Preview job summary and confirm the numbers before asking for review.

- [ ] **Step 5: When the PR is approved, accept the ADRs**

In `docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md` and `docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md`, change `* **Status:** Proposed` to `* **Status:** Accepted`, then:
```bash
git add docs/adr/0031-commit-driven-four-part-versioning-with-csproj-floor.md docs/adr/0032-base-tag-is-highest-reachable-ga-tag.md
git commit -m "docs(adr): accept 0031 and 0032" -m "Refs #244"
git push
```

- [ ] **Step 6: After merge and the first `dev` push, verify the dev release path**

Run (replace `<run>` with the `ci-cd.yaml` run number and `<sha>` with the pushed commit):
```bash
gh run list --workflow ci-cd.yaml --branch dev --limit 1
gh release view "v3.1.0.<run>-dev" --json tagName,targetCommitish,isPrerelease --jq .
git fetch origin --tags && git rev-parse "v3.1.0.<run>-dev^{commit}"
```
Expected: the `build-and-test` summary shows `3.1.0.<run>`, `targetCommitish` is the pushed SHA (a 40-character SHA, not `dev`), `isPrerelease` is `true`, and `git rev-parse` prints `<sha>`. Open the release notes and check they list commits since `v3.0.1.225-dev` only.

- [ ] **Step 7: Verify the GA path on the next `dev -> master` release**

The GA jobs only run on `master`, so this is the first time they execute. After that merge, check that the `create-release` run tags the merge commit and its notes cover the right range:
```bash
gh release view "v<version>" --json tagName,targetCommitish,isPrerelease --jq .
git fetch origin --tags && git rev-parse "v<version>^{commit}" && git rev-parse origin/master
```
Expected: `isPrerelease` is `false`, `targetCommitish` is the merge commit's SHA, the two `rev-parse` lines are equal, and the notes list the commits since the previous GA tag (not an empty list). If the notes are empty or the tag is on `dev`, revert this PR.

- [ ] **Step 8: Update and unblock the GUI PR #247**

After this PR merges: merge `origin/dev` into `feature/gui-redesign-port` (in `.claude/worktrees/gui-redesign-port`), rerun `dotnet test vHC/VhcXTests/VhcXTests.csproj`, `git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`, and edit the PR #247 "Version" section: its csproj `3.1.0.0` floor is already satisfied, the merge computes to the next version from commits, and the `3.1.1` vs `3.1.0` dip no longer applies. #247 is squash-merged, so its title (`feat(gui): ...`) must carry the `feat` prefix. Confirm the merge with the user before merging.
