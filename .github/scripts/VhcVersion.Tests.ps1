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
        $gitArgs = @('-C', $Repo, '-c', 'user.name=t', '-c', 'user.email=t@example.com', '-c', 'commit.gpgsign=false',
            'commit', '-q', '--allow-empty', '-m', $Subject)
        if ($Body) { $gitArgs += @('-m', $Body) }
        git @gitArgs | Out-Null
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
    It 'skips a stray tag whose numbers do not fit a version instead of throwing' {
        (Select-VhcHighestTag -Tags 'v99999999999.0.0.1', 'v3.0.1.193').Name | Should -Be 'v3.0.1.193'
        Select-VhcHighestTag -Tags 'v99999999999.0.0.1' | Should -BeNullOrEmpty
        Select-VhcHighestTag -Tags 'v3.0.1.193', 'v99999999999.0.0.1-dev' -IncludeDev | ForEach-Object Name | Should -Be 'v3.0.1.193'
    }
    It 'does not accept non-ASCII digits in a tag' {
        Select-VhcHighestTag -Tags "v$([char]0x0663).0.1.193" | Should -BeNullOrEmpty
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
    It 'is major for a hyphenated BREAKING-CHANGE footer' {
        Get-VhcCommitBump -Commits @([pscustomobject]@{ Subject = 'fix: a'; Body = "details`n`nBREAKING-CHANGE: x" }) | Should -Be 'major'
    }
    It 'does not treat a lower-case breaking change line as a footer' {
        Get-VhcCommitBump -Commits @([pscustomobject]@{ Subject = 'fix: a'; Body = "details`n`nbreaking change: not a footer" }) | Should -Be 'patch'
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
    It 'throws the malformed error for a footer whose numbers do not fit a version' {
        { Get-VhcReleaseAs -Commits @([pscustomobject]@{ Subject = 'a'; Body = 'Release-As: 99999999999.0.0' }) } | Should -Throw '*Malformed Release-As*'
    }
    It 'does not accept non-ASCII digits in a footer' {
        { Get-VhcReleaseAs -Commits @([pscustomobject]@{ Subject = 'a'; Body = "Release-As: $([char]0x0663).0.0" }) } | Should -Throw '*Malformed Release-As*'
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

    It 'is not broken by a tracked file named like the ref' {
        $script:repo = New-TestRepo
        # No tags, so the log range is the bare ref 'HEAD', which collides with the file below.
        Add-TestCommit -Repo $repo -Subject 'fix: a'
        Set-Content -Path (Join-Path $repo 'HEAD') -Value 'x'
        git -C $repo add HEAD | Out-Null
        Add-TestCommit -Repo $repo -Subject 'feat: b'
        $i = Get-VhcVersionInfo -Revision 3 -RepoPath $repo
        $i.Version | Should -Be '3.0.0.3'
        $i.CommitCount | Should -Be 2
    }

    It 'keeps git stderr out of the parsed output when git succeeds' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.193'
        Add-TestCommit -Repo $repo -Subject 'fix: b'
        $saved = $env:GIT_TRACE
        try {
            $env:GIT_TRACE = '1'   # makes every git command write trace lines to stderr, exit code 0
            $tags    = @(Invoke-VhcGit -RepoPath $repo -Arguments @('tag', '--list'))
            $commits = @(Get-VhcCommitsSince -RepoPath $repo -Ref 'HEAD')
        } finally {
            if ($null -eq $saved) { Remove-Item Env:GIT_TRACE -ErrorAction SilentlyContinue } else { $env:GIT_TRACE = $saved }
        }
        $tags | Should -Be @('v3.0.1.193')
        $commits.Count | Should -Be 2
        $commits[0].Sha | Should -Match '^[0-9a-f]{40}$'
    }

    It 'still throws with git output when git fails' {
        $script:repo = New-TestRepo
        { Invoke-VhcGit -RepoPath $repo -Arguments @('log', 'no-such-ref', '--') } | Should -Throw '*failed*'
    }
}

Describe 'Get-VhcVersionInfo after a GA tagged on master (dev is never merged back)' {
    AfterEach { if ($script:repo -and (Test-Path $script:repo)) { Remove-Item -Recurse -Force $script:repo } }

    It 'lets dev and master agree once the first GA exists only on the master merge commit' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.193'
        git -C $repo checkout -q -b dev
        Add-TestCommit -Repo $repo -Subject 'feat: b'
        git -C $repo checkout -q master
        Merge-TestBranch -Repo $repo -Branch dev
        git -C $repo tag 'v3.1.0.300'
        git -C $repo checkout -q dev
        Add-TestCommit -Repo $repo -Subject 'fix: c'
        $onDev = Get-VhcVersionInfo -Revision 400 -RepoPath $repo -Channel dev
        $onDev.BaseTag | Should -Be 'v3.1.0.300'
        $onDev.CommitCount | Should -Be 1
        $onDev.Version | Should -Be '3.1.1.400'
        git -C $repo checkout -q master
        Merge-TestBranch -Repo $repo -Branch dev
        (Get-VhcVersionInfo -Revision 401 -RepoPath $repo).Version | Should -Be '3.1.1.401'
    }

    It 'does not carry a consumed Release-As footer into the next dev range' {
        $script:repo = New-TestRepo
        Add-TestCommit -Repo $repo -Subject 'fix: a' -Tag 'v3.0.1.193'
        git -C $repo checkout -q -b dev
        Add-TestCommit -Repo $repo -Subject 'chore: bump' -Body 'Release-As: 4.0.0'
        git -C $repo checkout -q master
        Merge-TestBranch -Repo $repo -Branch dev
        git -C $repo tag 'v4.0.0.300'
        git -C $repo checkout -q dev
        Add-TestCommit -Repo $repo -Subject 'fix: later'
        (Get-VhcVersionInfo -Revision 400 -RepoPath $repo -Channel dev).Version | Should -Be '4.0.1.400'
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
