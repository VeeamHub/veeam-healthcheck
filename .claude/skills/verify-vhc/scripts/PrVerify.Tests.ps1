#Requires -Version 7
# Pester 5 tests for the PR verifier: the map-completeness test ("the map can't rot silently") and the verdict logic.
# Run: Invoke-Pester -Path .claude/skills/verify-vhc/scripts/PrVerify.Tests.ps1 -CI
# Offline: the gh calls are replaced by a scripted invoker; nothing here touches the network.

BeforeAll {
    $script:Here = $PSScriptRoot
    $script:Root = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
    Import-Module (Join-Path $PSScriptRoot 'PrVerify.psm1') -Force -DisableNameChecking
    Import-Module (Join-Path $PSScriptRoot 'PrVerifyRun.psm1') -Force -DisableNameChecking
    $script:MapPath = Join-Path $script:Root '.claude/skills/verify-vhc/pr-surfaces.json'
    $script:Map = Read-PrSurfaceMap -Path $script:MapPath
    $script:Tracked = @(git -C $script:Root ls-files --cached)
    $env:PRVERIFY_NO_GIT = '1'   # make Get-FileAtRef go through the (scripted) contents API so results do not depend on local objects

    # ---- scripted gh -------------------------------------------------------------------------------------------
    function script:New-Scenario {
        param([string]$Title = 'ci: change the live jobs', [string]$Body = '', [object[]]$Files, [object[]]$Runs = @(), [object[]]$CheckRuns = @(), [object[]]$Statuses = @(), [string]$HeadSha = ('a' * 40), [string]$Branch = 'ci/x', [string]$State = 'open', [bool]$Fork = $false, [string]$CommitMsg = 'ci: change the live jobs')
        @{
            Pr = @{ number = 900; title = $Title; body = $Body; state = $State; merged = $false; draft = $false; html_url = 'https://github.com/o/r/pull/900'; user = @{ login = 'adam' }
                base = @{ ref = 'dev'; sha = ('b' * 40); repo = @{ full_name = 'o/r' } }
                head = @{ ref = $Branch; sha = $HeadSha; repo = @{ full_name = $(if ($Fork) { 'fork/r' } else { 'o/r' }) } } }
            Files = $Files; Runs = $Runs; CheckRuns = $CheckRuns; Statuses = $Statuses; Commits = @(@{ commit = @{ message = $CommitMsg } })
            Calls = [System.Collections.Generic.List[string]]::new(); Comments = [System.Collections.Generic.List[object]]::new(); BranchTip = $HeadSha
        }
    }
    function script:New-Invoker {
        param($S)
        $root = $script:Root
        {
            param($a)
            $j = $a -join ' '
            $S.Calls.Add($j)
            $page = if ($j -match '&page=(\d+)') { [int]$Matches[1] } else { 1 }
            $json = { param($o) $o | ConvertTo-Json -Depth 20 -AsArray:($o -is [array]) }
            if ($j -match '^api repos/o/r/pulls/900$') { return (& $json $S.Pr) }
            if ($j -match '/pulls/900/files') { return $(if ($page -eq 1) { ConvertTo-Json @($S.Files) -Depth 20 } else { '[]' }) }
            if ($j -match '/pulls/900/commits') { return $(if ($page -eq 1) { ConvertTo-Json @($S.Commits) -Depth 20 } else { '[]' }) }
            if ($j -match 'actions/runs\?head_sha=') { return (@{ workflow_runs = $(if ($page -eq 1) { @($S.Runs) } else { @() }) } | ConvertTo-Json -Depth 20) }
            if ($j -match '/check-runs') { return (@{ check_runs = $(if ($page -eq 1) { @($S.CheckRuns) } else { @() }) } | ConvertTo-Json -Depth 20) }
            if ($j -match '/commits/[0-9a-f]+/statuses') { return $(if ($page -eq 1) { ConvertTo-Json @($S.Statuses) -Depth 5 -AsArray } else { '[]' }) }
            if ($j -match 'git/ref/heads/') { return (@{ object = @{ sha = $S.BranchTip } } | ConvertTo-Json) }
            if ($j -match 'git/trees/') { return (@{ tree = @(git -C $root ls-files | ForEach-Object { @{ type = 'blob'; path = $_ } }) } | ConvertTo-Json -Depth 5) }
            if ($j -match '^api repos/o/r/contents/(?<p>[^?]+)\?ref=') {
                $f = Join-Path $root ([uri]::UnescapeDataString($Matches.p))
                if (Test-Path -LiteralPath $f) { return (Get-Content -LiteralPath $f -Raw) }
                throw 'HTTP 404'
            }
            if ($j -match 'issues/900/comments' -and $j -notmatch 'PATCH|POST') { return (ConvertTo-Json @($S.Comments) -Depth 5 -AsArray) }
            if ($j -match 'PATCH.*issues/comments/(\d+)') { $S.Comments | Where-Object { $_.id -eq [int]$Matches[1] } | ForEach-Object { $_.body = ($a | Where-Object { $_ -like 'body=*' }) -replace '^body=', '' }; return (@{ html_url = 'https://x/c' } | ConvertTo-Json) }
            if ($j -match 'POST.*issues/900/comments') { $S.Comments.Add(@{ id = 1000 + $S.Comments.Count; body = (($a | Where-Object { $_ -like 'body=*' }) -replace '^body=', ''); html_url = 'https://x/c' }); return (@{ html_url = 'https://x/c' } | ConvertTo-Json) }
            if ($j -match '^workflow run') { return '' }
            throw "unscripted gh call: $j"
        }.GetNewClosure()
    }
    function script:Invoke-Scenario {
        param($S, [switch]$Comment, [switch]$Dispatch)
        $out = Join-Path ([IO.Path]::GetTempPath()) ("prv-" + [guid]::NewGuid().ToString('N'))
        $p = @{ PrRef = '900'; Repo = 'o/r'; RepoRoot = $script:Root; OutDir = $out; GhInvoker = (New-Invoker $S) }
        if ($Comment) { $p.Comment = $true }
        if ($Dispatch) { $p.Dispatch = $true }
        Invoke-PrVerification @p
    }

    # A ci-cd.yaml edit that lands inside the three live jobs, using the real file's line ranges.
    $script:CiCdText = Get-Content -LiteralPath (Join-Path $script:Root '.github/workflows/ci-cd.yaml') -Raw
    $script:CiCdJobs = @(Get-WorkflowJobs -Text $script:CiCdText)
    function script:New-CiCdFile {
        param([string[]]$JobIds)
        $patch = ''
        foreach ($id in $JobIds) {
            $j = $script:CiCdJobs | Where-Object Id -eq $id
            $n = $j.Start + 3
            $patch += "@@ -$n,1 +$n,1 @@`n-old line`n+new line`n"
        }
        @{ filename = '.github/workflows/ci-cd.yaml'; status = 'modified'; patch = $patch }
    }
    function script:New-Run { param([int]$Id, [string]$Path, [string]$Event = 'pull_request', [string]$Sha = ('a' * 40), [int]$Suite = $Id, [string]$Created = '2026-10-19T10:00:00Z') @{ id = $Id; path = ".github/workflows/$Path"; event = $Event; status = 'completed'; head_sha = $Sha; check_suite_id = $Suite; created_at = $Created; html_url = "https://github.com/o/r/actions/runs/$Id" } }
    function script:New-CheckRun { param([int]$Id, [string]$Name, [int]$Suite, [string]$Status = 'completed', $Conclusion = 'success') @{ id = $Id; name = $Name; status = $Status; conclusion = $Conclusion; check_suite = @{ id = $Suite }; html_url = "https://github.com/o/r/runs/$Id" } }
    function script:New-Status { param([int]$Id, [string]$Description, [string]$State = 'success', [string]$Context = 'verify-vhc/windows-host', [string]$Created = '2026-10-19T12:00:00Z') @{ id = $Id; state = $State; context = $Context; description = $Description; created_at = $Created; target_url = '' } }
    $script:LabJobs = 'integration-test-vbr-2', 'integration-test-vbr-13-sql', 'integration-test-vbr-12'
}

Describe 'surface map: cannot rot silently' {
    It 'is consistent with the tracked files and the workflows' {
        $problems = @(Test-PrSurfaceMap -Map $script:Map -RepoRoot $script:Root -TrackedFiles $script:Tracked)
        $problems | Should -BeNullOrEmpty -Because ($problems -join "`n")
    }

    It 'every tracked file lands on at least one surface' {
        $r = Resolve-FileSurfaces -Map $script:Map -Files $script:Tracked
        $r.Unmapped | Should -BeNullOrEmpty -Because ("unmapped: " + ($r.Unmapped -join ', '))
    }

    It 'detects a tracked file that matches no glob' {
        $p = Test-PrSurfaceMap -Map $script:Map -RepoRoot $script:Root -TrackedFiles ($script:Tracked + 'brand/new/unmapped.bin')
        $p | Should -Contain 'tracked file matches no surface glob: brand/new/unmapped.bin'
    }

    It 'detects a job id that does not exist in the workflow' {
        $text = (Get-Content -LiteralPath $script:MapPath -Raw) -replace '"job": "verify-vhc"', '"job": "no-such-job"'
        $m = $text | ConvertFrom-Json -Depth 32
        (Test-PrSurfaceMap -Map $m -RepoRoot $script:Root -TrackedFiles $script:Tracked) -join "`n" | Should -Match "no-such-job"
    }

    It 'detects a workflow file that does not exist' {
        $text = (Get-Content -LiteralPath $script:MapPath -Raw) -replace '"workflow": "pester-tests.yml"', '"workflow": "ghost.yml"'
        $m = $text | ConvertFrom-Json -Depth 32
        (Test-PrSurfaceMap -Map $m -RepoRoot $script:Root -TrackedFiles $script:Tracked) -join "`n" | Should -Match 'ghost.yml'
    }

    It 'detects a job added to a workflow but missing from the registry' {
        $m = (Get-Content -LiteralPath $script:MapPath -Raw) | ConvertFrom-Json -Depth 32
        $m.workflows.'ci-cd.yaml'.jobs.PSObject.Properties.Remove('report-render-smoke')
        (Test-PrSurfaceMap -Map $m -RepoRoot $script:Root -TrackedFiles $script:Tracked) -join "`n" | Should -Match "job 'report-render-smoke' is not in the registry"
    }

    It 'detects a dead glob' {
        $m = (Get-Content -LiteralPath $script:MapPath -Raw) | ConvertFrom-Json -Depth 32
        $m.surfaces.docs.paths += 'no/such/dir/**'
        (Test-PrSurfaceMap -Map $m -RepoRoot $script:Root -TrackedFiles $script:Tracked) -join "`n" | Should -Match 'no/such/dir'
    }

    It 'registry workflows match .github/workflows exactly' {
        $disk = Get-ChildItem (Join-Path $script:Root '.github/workflows') -File | Where-Object Name -match '\.ya?ml$' | ForEach-Object Name | Sort-Object
        $reg = $script:Map.workflows.PSObject.Properties.Name | Sort-Object
        $reg | Should -Be $disk
    }
}

Describe 'globs' {
    It '<glob> vs <path> = <expected>' -ForEach @(
        @{ glob = '**/*.md'; path = 'README.md'; expected = $true }
        @{ glob = '**/*.md'; path = 'a/b/c.md'; expected = $true }
        @{ glob = 'docs/**'; path = 'docs/a/b.png'; expected = $true }
        @{ glob = 'docs/**'; path = 'docs'; expected = $false }
        @{ glob = '.github/workflows/*.yml'; path = '.github/workflows/x.yml'; expected = $true }
        @{ glob = '.github/workflows/*.yml'; path = '.github/workflows/sub/x.yml'; expected = $false }
        @{ glob = 'vHC/HC_Reporting/Common/*.cs'; path = 'vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs'; expected = $false }
        @{ glob = 'vHC/**/*.csproj'; path = 'vHC/VhcGuiTests/VhcGuiTests.csproj'; expected = $true }
        @{ glob = '**/*.example.*'; path = 'a/labs-config.example.json'; expected = $true }
        @{ glob = 'a?c'; path = 'abc'; expected = $true }
    ) {
        Test-GlobMatch -Path $path -Globs @($glob) | Should -Be $expected
    }
}

Describe 'workflow parsing' {
    It 'finds ci-cd.yaml jobs with ranges and ignores commented-out jobs' {
        $ids = $script:CiCdJobs | ForEach-Object Id
        $ids | Should -Contain 'integration-test-vbr-12'
        $ids | Should -Contain 'report-render-smoke'
        ($script:CiCdJobs | Where-Object Id -eq 'integration-test-vbr-12').End | Should -BeGreaterThan ($script:CiCdJobs | Where-Object Id -eq 'integration-test-vbr-12').Start
    }
    It 'uses a job name: over its id' {
        $t = Get-Content -LiteralPath (Join-Path $script:Root '.github/workflows/pester-tests.yml') -Raw
        (Get-WorkflowJobs -Text $t | Select-Object -First 1).Name | Should -Be 'Pester — all test files'
    }
    It 'maps patch lines to the jobs they touch, and top-level edits to every job' {
        $lab = $script:CiCdJobs | Where-Object Id -eq 'integration-test-vbr-2'
        $lines = Get-PatchTouchedLines -Patch "@@ -$($lab.Start + 2),1 +$($lab.Start + 2),1 @@`n-a`n+b`n"
        @(Get-TouchedJobIds -Jobs $script:CiCdJobs -TouchedLines $lines) | Should -Be @('integration-test-vbr-2')
        $top = Get-PatchTouchedLines -Patch "@@ -1,1 +1,1 @@`n-a`n+b`n"
        @(Get-TouchedJobIds -Jobs $script:CiCdJobs -TouchedLines $top).Count | Should -Be $script:CiCdJobs.Count
        @(Get-TouchedJobIds -Jobs $script:CiCdJobs -TouchedLines $null).Count | Should -Be $script:CiCdJobs.Count
    }
    It 'gives a new job its own header comment, not the job above it' {
        $t = "on: push`njobs:`n  a:`n    runs-on: x`n    # a's own trailing comment`n`n  # header for b`n  #`n  # more about b`n  b:`n    runs-on: y`n`n  c:`n    runs-on: z`n"
        $jobs = @(Get-WorkflowJobs -Text $t)
        ($jobs | Where-Object Id -eq 'a').End | Should -Be 6
        ($jobs | Where-Object Id -eq 'b').Start | Should -Be 7
        ($jobs | Where-Object Id -eq 'b').End | Should -Be 12
        @(Get-TouchedJobIds -Jobs $jobs -TouchedLines @(7, 8, 9, 10, 11, 12)) | Should -Be @('b')
    }
}

Describe 'check status from real data' {
    BeforeAll {
        $script:R1 = New-Run -Id 1 -Path 'crossplatform-tests.yml'
        $script:R2 = New-Run -Id 2 -Path 'ci-cd.yaml' -Event workflow_dispatch
    }
    It 'absent job is none, never passed' {
        (Get-JobStatus -Runs @() -CheckRuns @() -Workflow 'ci-cd.yaml' -DisplayName 'integration-test-vbr-2').State | Should -Be 'none'
    }
    It 'workflow that ran without the job is none' {
        (Get-JobStatus -Runs @($script:R1) -CheckRuns @() -Workflow 'crossplatform-tests.yml' -DisplayName 'verify-vhc').State | Should -Be 'none'
    }
    It 'skipped is not passed' {
        $cr = New-CheckRun -Id 10 -Name 'verify-vhc' -Suite 1 -Conclusion 'skipped'
        (Get-JobStatus -Runs @($script:R1) -CheckRuns @($cr) -Workflow 'crossplatform-tests.yml' -DisplayName 'verify-vhc').State | Should -Be 'skipped'
    }
    It 'in progress is pending' {
        $cr = New-CheckRun -Id 10 -Name 'verify-vhc' -Suite 1 -Status 'in_progress' -Conclusion $null
        (Get-JobStatus -Runs @($script:R1) -CheckRuns @($cr) -Workflow 'crossplatform-tests.yml' -DisplayName 'verify-vhc').State | Should -Be 'pending'
    }
    It 'matrix legs aggregate: one failure fails' {
        $a = New-CheckRun -Id 10 -Name 'test-crossplatform (ubuntu-latest)' -Suite 1
        $b = New-CheckRun -Id 11 -Name 'test-crossplatform (macos-latest)' -Suite 1 -Conclusion 'failure'
        (Get-JobStatus -Runs @($script:R1) -CheckRuns @($a, $b) -Workflow 'crossplatform-tests.yml' -DisplayName 'test-crossplatform').State | Should -Be 'failed'
    }
    It 'a same-named job in another workflow does not count' {
        $cr = New-CheckRun -Id 10 -Name 'integration-test-vbr-2' -Suite 1
        (Get-JobStatus -Runs @($script:R1, $script:R2) -CheckRuns @($cr) -Workflow 'ci-cd.yaml' -DisplayName 'integration-test-vbr-2').State | Should -Be 'none'
    }
    It 'a workflow_dispatch run counts' {
        $cr = New-CheckRun -Id 10 -Name 'integration-test-vbr-2' -Suite 2
        $s = Get-JobStatus -Runs @($script:R1, $script:R2) -CheckRuns @($cr) -Workflow 'ci-cd.yaml' -DisplayName 'integration-test-vbr-2'
        $s.State | Should -Be 'passed'
        $s.Event | Should -Be 'workflow_dispatch'
    }
    It 'the latest run wins over an older one' {
        $old = New-Run -Id 5 -Path 'ci-cd.yaml' -Event workflow_dispatch -Created '2026-10-19T08:00:00Z'
        $new = New-Run -Id 6 -Path 'ci-cd.yaml' -Event workflow_dispatch -Created '2026-10-19T09:00:00Z'
        $c1 = New-CheckRun -Id 20 -Name 'build-and-test' -Suite 5 -Conclusion 'failure'
        $c2 = New-CheckRun -Id 21 -Name 'build-and-test' -Suite 6
        (Get-JobStatus -Runs @($old, $new) -CheckRuns @($c1, $c2) -Workflow 'ci-cd.yaml' -DisplayName 'build-and-test').State | Should -Be 'passed'
    }
}

Describe 'verdict precedence' {
    It '<surfaces> unmapped=<un> otherFailed=<of> hygieneFailed=<hf> otherPending=<op> => <expected>' -ForEach @(
        @{ surfaces = @('VERIFIED'); un = $false; of = $false; hf = $false; op = $false; expected = 'VERIFIED' }
        @{ surfaces = @('VERIFIED', 'NEEDS-RUN'); un = $false; of = $false; hf = $false; op = $false; expected = 'NEEDS-RUN' }
        @{ surfaces = @('VERIFIED', 'NEEDS-HUMAN'); un = $false; of = $false; hf = $false; op = $false; expected = 'NEEDS-HUMAN' }
        @{ surfaces = @('NEEDS-RUN', 'NEEDS-HUMAN'); un = $false; of = $false; hf = $false; op = $false; expected = 'NEEDS-RUN' }
        @{ surfaces = @('VERIFIED'); un = $true; of = $false; hf = $false; op = $false; expected = 'NEEDS-HUMAN' }
        @{ surfaces = @('VERIFIED', 'FAILED'); un = $true; of = $false; hf = $false; op = $false; expected = 'FAILED' }
        @{ surfaces = @('VERIFIED'); un = $false; of = $true; hf = $false; op = $false; expected = 'FAILED' }
        @{ surfaces = @('VERIFIED'); un = $false; of = $false; hf = $true; op = $false; expected = 'FAILED' }
        @{ surfaces = @('VERIFIED'); un = $false; of = $false; hf = $false; op = $true; expected = 'NEEDS-RUN' }
    ) {
        Get-OverallVerdict -SurfaceVerdicts $surfaces -HasUnmapped $un -OtherFailed $of -HygieneFailed $hf -OtherPending $op | Should -Be $expected
    }
}

Describe 'dispatch safety gates' {
    BeforeAll {
        $script:Sha = 'c' * 40
        function script:Gate { param([string]$Workflow = 'ci-cd.yaml', [bool]$Fork = $false, [string]$Branch = 'ci/x', [string]$Tip = $script:Sha, $Runs = @())
            Test-DispatchAllowed -Map $script:Map -Workflow $Workflow -IsFork $Fork -HeadBranch $Branch -HeadSha $script:Sha -BranchTipSha $Tip -ExistingRuns $Runs }
    }
    It 'allows a same-repo feature branch with no existing run' { (Gate).Allowed | Should -BeTrue }
    It 'refuses a fork head' { (Gate -Fork $true).Allowed | Should -BeFalse }
    It 'refuses master and main' {
        (Gate -Branch 'master').Allowed | Should -BeFalse
        (Gate -Branch 'main').Allowed | Should -BeFalse
    }
    It 'refuses manual-release, beta-release and any other publish workflow' {
        foreach ($w in 'manual-release.yml', 'beta-release.yml', 'docs-deploy.yml', 'sbom-generation.yml', 'pester-tests.yml', 'unknown.yml') { (Gate -Workflow $w).Allowed | Should -BeFalse -Because $w }
    }
    It 'refuses when the branch tip is not the PR head SHA' { (Gate -Tip ('d' * 40)).Allowed | Should -BeFalse }
    It 'allows at most one run per head SHA' {
        $r = @(@{ id = 7; path = '.github/workflows/ci-cd.yaml'; head_sha = $script:Sha; event = 'workflow_dispatch' })
        (Gate -Runs $r).Allowed | Should -BeFalse
        $other = @(@{ id = 8; path = '.github/workflows/ci-cd.yaml'; head_sha = ('e' * 40); event = 'push' })
        (Gate -Runs $other).Allowed | Should -BeTrue
    }
}

Describe 'hygiene' {
    BeforeAll {
        function script:Hyg { param([string]$Title, [object[]]$Files, [string]$Body = '', [string[]]$Messages = @())
            $pr = [pscustomobject]@{ number = 900; title = $Title; body = $Body }
            $commits = @($Messages | ForEach-Object { [pscustomobject]@{ commit = [pscustomobject]@{ message = $_ } } })
            Get-Hygiene -Map $script:Map -Pr $pr -Files @($Files | ForEach-Object { [pscustomobject]$_ }) -Commits $commits -HeadSha ('a' * 40) -BaseSha ('b' * 40) -WorkflowDir (Join-Path $script:Root '.github/workflows') -ConventionalModule (Join-Path $script:Root '.github/scripts/VhcVersion.psm1') }
        function script:Level { param($H, [string]$Id) ($H.Items | Where-Object Id -eq $Id).Level }
    }
    It 'fails feat on a tooling-only diff' {
        $h = Hyg -Title 'feat(ci): new thing' -Files @(@{ filename = '.github/workflows/x.yml'; status = 'modified' })
        Level $h 'version-bump-type' | Should -Be 'fail'
    }
    It 'fails a breaking marker on a tooling-only diff' {
        $h = Hyg -Title 'ci!: drop it' -Files @(@{ filename = 'docs/a.md'; status = 'modified' })
        Level $h 'version-bump-type' | Should -Be 'fail'
    }
    It 'allows feat when product code changed' {
        $h = Hyg -Title 'feat(report): add a thing' -Files @(@{ filename = 'vHC/HC_Reporting/Functions/Reporting/Html/X.cs'; status = 'modified' })
        Level $h 'version-bump-type' | Should -Be 'info'
    }
    It 'flags (#N) shorthand without a closing keyword' {
        $h = Hyg -Title 'fix(nas): fix Get-NasInfo for v13 (#112)' -Files @(@{ filename = 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-NasInfo.ps1'; status = 'modified' })
        Level $h 'fixes-keyword' | Should -Be 'warn'
    }
    It 'accepts Fixes #N' {
        $h = Hyg -Title 'fix(nas): fix it' -Body 'Fixes #112' -Files @(@{ filename = 'docs/a.md'; status = 'modified' })
        Level $h 'fixes-keyword' | Should -Be 'pass'
    }
    It 'warns when a fix touches product code and ChangeLog.md has no new [Unreleased] line' {
        $h = Hyg -Title 'fix(report): a user-visible fix' -Files @(@{ filename = 'vHC/HC_Reporting/Functions/Reporting/Html/X.cs'; status = 'modified' })
        Level $h 'changelog' | Should -Be 'warn'
    }
    It 'exempts refactor/test/ci/docs from the changelog rule' {
        $h = Hyg -Title 'refactor(report): tidy' -Files @(@{ filename = 'vHC/HC_Reporting/Functions/Reporting/Html/X.cs'; status = 'modified' })
        Level $h 'changelog' | Should -Be 'pass'
    }
    It 'fails a hand-bumped csproj version' {
        $h = Hyg -Title 'chore: bump' -Files @(@{ filename = 'vHC/HC_Reporting/VeeamHealthCheck.csproj'; status = 'modified'; patch = "@@ -1,1 +1,1 @@`n-<Version>3.1.0.1</Version>`n+<Version>3.1.0.2</Version>" })
        Level $h 'csproj-version' | Should -Be 'fail'
    }
    It 'does not fail a PackageReference Version attribute' {
        $h = Hyg -Title 'ci: coverlet' -Files @(@{ filename = 'vHC/VhcGuiTests/VhcGuiTests.csproj'; status = 'modified'; patch = "@@ -1,1 +1,1 @@`n+<PackageReference Include=`"coverlet`" Version=`"6.0.0`" />" })
        Level $h 'csproj-version' | Should -Be 'pass'
    }
    It 'fails an added action that is not pinned to a SHA (this repo pins everything)' {
        $patch = "@@ -1,1 +1,2 @@`n+      - uses: actions/checkout@v4`n+      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4"
        $h = Hyg -Title 'ci: x' -Files @(@{ filename = '.github/workflows/x.yml'; status = 'modified'; patch = $patch })
        Level $h 'actions-pinned' | Should -Be 'fail'
    }
    It 'flags a hand-edited generated file' {
        $h = Hyg -Title 'docs: x' -Files @(@{ filename = 'docs/changelog.md'; status = 'modified' })
        Level $h 'generated-file' | Should -Be 'warn'
    }
}

Describe 'markdown and changelog sanity' {
    It 'finds a broken relative link, an unclosed fence and a conflict marker' {
        $tree = @('docs/a.md', 'README.md')
        $p = Get-MarkdownProblems -Text "[ok](a.md) [bad](missing.md) [web](https://x.y)`n``````bash`nfoo" -Path 'docs/b.md' -TreePaths $tree
        $p | Should -Contain "broken relative link 'missing.md'"
        $p | Should -Contain 'unclosed code fence'
        (Get-MarkdownProblems -Text "<<<<<<< HEAD`nx" -Path 'a.md' -TreePaths $tree) | Should -Contain 'merge-conflict marker'
    }
    It 'ignores links inside code fences and resolves ../' {
        $tree = @('README.md', 'docs/x.md')
        Get-MarkdownProblems -Text "``````md`n[nope](nope.md)`n``````n[up](../README.md)" -Path 'docs/x.md' -TreePaths $tree | Should -BeNullOrEmpty
    }
    It 'flags a version section with no link definition' {
        $cl = "## [Unreleased]`n`n## [1.2.3] - 2026-01-01`n`n### Added`n`n- x`n`n[Unreleased]: https://x/compare/v1.2.3...dev`n"
        Get-ChangelogProblems -Text $cl | Should -Contain 'no link definition for [1.2.3]'
    }
    It 'accepts the repo ChangeLog.md structure apart from known gaps' {
        $p = @(Get-ChangelogProblems -Text (Get-Content -LiteralPath (Join-Path $script:Root 'ChangeLog.md') -Raw))
        $p | Where-Object { $_ -notmatch 'link definition' } | Should -BeNullOrEmpty
    }
}

Describe 'end to end on scripted PRs (offline)' {
    Context 'a PR that changes the three live VBR jobs (the PR #295 failure mode)' {
        It 'NEEDS-RUN when only the path-filtered / PR checks ran: green but the live jobs never ran' {
            $prRun = New-Run -Id 100 -Path 'codeql.yml' -Suite 100
            $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs) -Runs @($prRun) -CheckRuns @(New-CheckRun -Id 1 -Name 'Analyze (csharp)' -Suite 100)
            $r = Invoke-Scenario $s
            $r.Verdict | Should -Be 'NEEDS-RUN'
            $r.ExitCode | Should -Be 2
            $labChecks = @($r.Report.surfaces | Where-Object id -eq 'ci-workflows' | ForEach-Object { $_.checks })
            @($labChecks | Where-Object status -eq 'not-run').Count | Should -Be 3
            $r.Markdown | Should -Match '--dispatch'
            $r.Markdown | Should -Match 'integration-test-vbr-12'
            (Test-Path (Join-Path $r.EvidenceDir 'report.md')) | Should -BeTrue
            (Test-Path (Join-Path $r.EvidenceDir 'report.json')) | Should -BeTrue
        }
        It 'only the touched jobs are required (build-and-test is not)' {
            $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs)
            $r = Invoke-Scenario $s
            (@($r.Report.surfaces[0].checks).id -join ' ') | Should -Not -Match 'build-and-test'
        }
        It 'VERIFIED when a workflow_dispatch run on the exact head SHA passed the touched jobs' {
            $run = New-Run -Id 200 -Path 'ci-cd.yaml' -Event workflow_dispatch -Suite 200
            $crs = $script:LabJobs | ForEach-Object -Begin { $i = 0 } -Process { New-CheckRun -Id (300 + $i++) -Name $_ -Suite 200 }
            $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs) -Runs @($run) -CheckRuns @($crs)
            $r = Invoke-Scenario $s
            $r.Verdict | Should -Be 'VERIFIED'
            $r.ExitCode | Should -Be 0
        }
        It 'NEEDS-RUN while the dispatched run is still in progress' {
            $run = New-Run -Id 200 -Path 'ci-cd.yaml' -Event workflow_dispatch -Suite 200
            $crs = @(New-CheckRun -Id 301 -Name 'integration-test-vbr-2' -Suite 200) +
                @(New-CheckRun -Id 302 -Name 'integration-test-vbr-13-sql' -Suite 200 -Status in_progress -Conclusion $null) +
                @(New-CheckRun -Id 303 -Name 'integration-test-vbr-12' -Suite 200)
            $r = Invoke-Scenario (New-Scenario -Files @(New-CiCdFile $script:LabJobs) -Runs @($run) -CheckRuns $crs)
            $r.Verdict | Should -Be 'NEEDS-RUN'
            $r.Markdown | Should -Match 'PENDING'
        }
        It 'FAILED when one live job failed' {
            $run = New-Run -Id 200 -Path 'ci-cd.yaml' -Event workflow_dispatch -Suite 200
            $crs = @(New-CheckRun -Id 301 -Name 'integration-test-vbr-2' -Suite 200 -Conclusion failure) +
                @(New-CheckRun -Id 302 -Name 'integration-test-vbr-13-sql' -Suite 200) + @(New-CheckRun -Id 303 -Name 'integration-test-vbr-12' -Suite 200)
            (Invoke-Scenario (New-Scenario -Files @(New-CiCdFile $script:LabJobs) -Runs @($run) -CheckRuns $crs)).Verdict | Should -Be 'FAILED'
        }
        It 'a skipped live job is not exercised' {
            $run = New-Run -Id 200 -Path 'ci-cd.yaml' -Event workflow_dispatch -Suite 200
            $crs = @(New-CheckRun -Id 301 -Name 'integration-test-vbr-2' -Suite 200 -Conclusion skipped) +
                @(New-CheckRun -Id 302 -Name 'integration-test-vbr-13-sql' -Suite 200) + @(New-CheckRun -Id 303 -Name 'integration-test-vbr-12' -Suite 200)
            (Invoke-Scenario (New-Scenario -Files @(New-CiCdFile $script:LabJobs) -Runs @($run) -CheckRuns $crs)).Verdict | Should -Be 'NEEDS-RUN'
        }
        It 'never calls dispatch or posts a comment without the flags' {
            $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs)
            Invoke-Scenario $s | Out-Null
            @($s.Calls | Where-Object { $_ -match '^workflow run|POST|PATCH' }).Count | Should -Be 0
        }
        It '--dispatch on a fork head dispatches nothing' {
            $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs) -Fork $true
            $r = Invoke-Scenario $s -Dispatch
            @($s.Calls | Where-Object { $_ -match '^workflow run' }).Count | Should -Be 0
            $r.Report.notes -join ' ' | Should -Match 'fork'
        }
        It '--dispatch refuses when the branch moved past the PR head' {
            $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs)
            $s.BranchTip = 'f' * 40
            Invoke-Scenario $s -Dispatch | Out-Null
            @($s.Calls | Where-Object { $_ -match '^workflow run' }).Count | Should -Be 0
        }
    }

    Context 'docs-only and changelog-only PRs' {
        It 'a changelog-only PR is VERIFIED by structure sanity, no product build needed' {
            $s = New-Scenario -Title 'docs(changelog): add a link' -CommitMsg 'docs(changelog): add a link' -Files @(@{ filename = 'ChangeLog.md'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' })
            $r = Invoke-Scenario $s
            $r.Report.surfaces.id | Should -Be @('changelog')
            $r.Verdict | Should -Be 'VERIFIED'
        }
        It 'an unmapped file is NEEDS-HUMAN' {
            $s = New-Scenario -Title 'chore: add a thing' -CommitMsg 'chore: add a thing' -Files @(@{ filename = 'brand/new/thing.bin'; status = 'added' })
            $r = Invoke-Scenario $s
            $r.Verdict | Should -Be 'NEEDS-HUMAN'
            $r.Report.unmapped | Should -Contain 'brand/new/thing.bin'
        }
        It 'a feat-typed title on a tooling-only PR is FAILED by hygiene even when every check is green' {
            $s = New-Scenario -Title 'feat(ci): new thing' -CommitMsg 'feat(ci): new thing' -Files @(@{ filename = 'ChangeLog.md'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' })
            (Invoke-Scenario $s).Verdict | Should -Be 'FAILED'
        }
    }

    Context 'a product PR whose path-filtered workflow is absent' {
        It 'reports the crossplatform verify-vhc job as not exercised, never passed' {
            $s = New-Scenario -Title 'fix(report): x' -CommitMsg 'fix(report): x' -Files @(@{ filename = 'vHC/HC_Reporting/css.css'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' })
            $r = Invoke-Scenario $s
            $r.Verdict | Should -Be 'NEEDS-RUN'
            ($r.Report.surfaces | Where-Object id -eq 'report-pipeline').verdict | Should -Be 'NEEDS-RUN'
            $r.Markdown | Should -Match 'pr .* --run'
        }
    }

    Context 'sticky comment' {
        It 'creates one marker comment, then updates it instead of adding a second' {
            $s = New-Scenario -Title 'docs(changelog): x' -CommitMsg 'docs(changelog): x' -Files @(@{ filename = 'ChangeLog.md'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' })
            Invoke-Scenario $s -Comment | Out-Null
            $s.Comments.Count | Should -Be 1
            $s.Comments[0].body | Should -Match ([regex]::Escape((Get-PrVerifyMarker)))
            Invoke-Scenario $s -Comment | Out-Null
            $s.Comments.Count | Should -Be 1
            @($s.Calls | Where-Object { $_ -match 'PATCH' }).Count | Should -BeGreaterThan 0
        }
        It 'does not comment on a fork PR' {
            $s = New-Scenario -Fork $true -Title 'docs: x' -CommitMsg 'docs: x' -Files @(@{ filename = 'ChangeLog.md'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' })
            $r = Invoke-Scenario $s -Comment
            $s.Comments.Count | Should -Be 0
            $r.CommentNote | Should -Match 'fork'
        }
    }
}

Describe 'windows-host commit status (vhc-verify.sh winhost --status)' {
    BeforeAll {
        $script:Green = 'build ok; xunit 1180 pass 0 fail 15 skip; live ok 8 checks; gui 3 pass 0 fail 0 skip'
        # A GUI test project change maps to the gui and build-tests surfaces; the headless/cross-platform CI job is green.
        function script:New-GuiScenario {
            param([object[]]$Statuses = @())
            $run = New-Run -Id 500 -Path 'crossplatform-tests.yml' -Suite 500
            $crs = @(New-CheckRun -Id 501 -Name 'test-crossplatform (ubuntu-latest)' -Suite 500)
            $files = @(@{ filename = 'vHC/VhcGuiE2ETests/VhcAppSession.cs'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' + "`n-a`n+b`n" })
            New-Scenario -Title 'test(gui-e2e): fix a picker id' -CommitMsg 'test(gui-e2e): fix a picker id' -Files $files -Runs @($run) -CheckRuns $crs -Statuses $Statuses
        }
    }
    Context 'Get-StatusStepResult' {
        It 'passes a step that the success status names with no failures' {
            $st = @(New-Status -Id 1 -Description $script:Green)
            foreach ($step in 'build', 'xunit', 'live', 'gui') { (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step $step).State | Should -Be 'passed' -Because $step }
        }
        It 'does not count a step the description does not name' {
            $st = @(New-Status -Id 1 -Description 'build ok; xunit 10 pass 0 fail 0 skip')
            (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step 'gui').State | Should -Be 'none'
            (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step 'live').State | Should -Be 'none'
        }
        It 'fails a step with failures, and a failure state never vouches for a step' {
            $st = @(New-Status -Id 1 -State failure -Description 'build ok; xunit 10 pass 2 fail 0 skip; gui 3 pass 0 fail 0 skip')
            (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step 'xunit').State | Should -Be 'failed'
            (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step 'gui').State | Should -Be 'failed'
        }
        It 'does not count a GUI suite that skipped tests' {
            $st = @(New-Status -Id 1 -Description 'build ok; xunit 10 pass 0 fail 0 skip; gui 2 pass 0 fail 1 skip')
            (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step 'gui').State | Should -Be 'none'
        }
        It 'only the newest status of the context counts' {
            $old = New-Status -Id 1 -Description $script:Green -Created '2026-10-19T10:00:00Z'
            $new = New-Status -Id 2 -State failure -Description 'build ok; xunit 10 pass 1 fail 0 skip' -Created '2026-10-19T11:00:00Z'
            (Get-StatusStepResult -Statuses @($old, $new) -Context 'verify-vhc/windows-host' -Step 'xunit').State | Should -Be 'failed'
        }
        It 'ignores other contexts' {
            $st = @(New-Status -Id 1 -Context 'some/other' -Description $script:Green)
            (Get-StatusStepResult -Statuses $st -Context 'verify-vhc/windows-host' -Step 'gui').State | Should -Be 'none'
        }
    }
    Context 'a PR touching GUI test code' {
        It 'NEEDS-RUN with no status, and the remedy names the winhost command' {
            $r = Invoke-Scenario (New-GuiScenario)
            $r.Verdict | Should -Be 'NEEDS-RUN'
            $r.Markdown | Should -Match 'winhost <ID> --live --gui --status'
        }
        It 'VERIFIED when the status on the head SHA covers xunit and gui' {
            $r = Invoke-Scenario (New-GuiScenario -Statuses @(New-Status -Id 1 -Description $script:Green))
            $r.Verdict | Should -Be 'VERIFIED' -Because $r.Markdown
        }
        It 'NEEDS-RUN when the status ran gui with a skipped test' {
            $r = Invoke-Scenario (New-GuiScenario -Statuses @(New-Status -Id 1 -Description 'build ok; xunit 1180 pass 0 fail 15 skip; gui 2 pass 0 fail 1 skip'))
            $r.Verdict | Should -Be 'NEEDS-RUN'
        }
        It 'FAILED when the status reports failures' {
            $r = Invoke-Scenario (New-GuiScenario -Statuses @(New-Status -Id 1 -State failure -Description 'build ok; xunit 1180 pass 0 fail 15 skip; gui 2 pass 1 fail 0 skip'))
            $r.Verdict | Should -Be 'FAILED'
        }
    }
    Context 'the map' {
        It 'rejects a status entry with an unknown step or no context' {
            $m = (Get-Content -LiteralPath $script:MapPath -Raw) | ConvertFrom-Json -Depth 32
            $m.surfaces.gui.checks[1].status[0].step = 'bogus'
            (Test-PrSurfaceMap -Map $m -RepoRoot $script:Root -TrackedFiles $script:Tracked) -join "`n" | Should -Match "unknown status step 'bogus'"
        }
    }
}

Describe '--run and --dispatch mechanics' {
    It '--run refuses to run local checks when the checkout is not the PR head, and says so' {
        $s = New-Scenario -Title 'fix(report): x' -CommitMsg 'fix(report): x' -Files @(@{ filename = 'vHC/HC_Reporting/css.css'; status = 'modified'; patch = '@@ -1,1 +1,1 @@' })
        $out = Join-Path ([IO.Path]::GetTempPath()) ("prv-" + [guid]::NewGuid().ToString('N'))
        $r = Invoke-PrVerification -PrRef '900' -Repo 'o/r' -RepoRoot $script:Root -OutDir $out -Run -GhInvoker (New-Invoker $s)
        $r.Report.notes -join ' ' | Should -Match 'local checks skipped: checkout is'
        $r.Verdict | Should -Be 'NEEDS-RUN'
    }

    It 'Invoke-LocalChecks runs build first, uses the pr-N evidence ids, and records pass/fail' -Skip:$IsWindows {
        $repo = Join-Path ([IO.Path]::GetTempPath()) ("prv-repo-" + [guid]::NewGuid().ToString('N'))
        $dir = Join-Path $repo '.claude/skills/verify-vhc/scripts'
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Set-Content -LiteralPath (Join-Path $dir 'vhc-verify.sh') -Value "#!/usr/bin/env bash`necho `"stub `$*`"`n[ `"`$1`" = cli-checks ] && exit 1`nexit 0`n"
        git -C $repo init -q
        git -C $repo -c user.email=t@t -c user.name=t add -A
        git -C $repo -c user.email=t@t -c user.name=t commit -q -m init
        $sha = (git -C $repo rev-parse HEAD).Trim()
        $ev = Join-Path $repo 'ev'
        $r = Invoke-LocalChecks -Ids @('import-scrub', 'cli-checks', 'import-report') -PrNumber '77' -HeadSha $sha -EvidenceDir $ev -Root $repo
        $r.results['build'].Ok | Should -BeTrue
        $r.results['import-report'].Ok | Should -BeTrue
        $r.results['cli-checks'].Ok | Should -BeFalse
        (Get-Content (Join-Path $ev 'run-import-scrub.log') -Raw) | Should -Match 'stub import-scrub pr-77-scrub'
        (Get-Content (Join-Path $ev 'run-import-report.log') -Raw) | Should -Match 'stub import-report pr-77'
        Remove-Item -Recurse -Force $repo
    }

    It 'Invoke-LocalChecks refuses a dirty checkout' {
        $repo = Join-Path ([IO.Path]::GetTempPath()) ("prv-repo-" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $repo | Out-Null
        Set-Content -LiteralPath (Join-Path $repo 'a.txt') -Value 'a'
        git -C $repo init -q; git -C $repo -c user.email=t@t -c user.name=t add -A; git -C $repo -c user.email=t@t -c user.name=t commit -q -m init
        $sha = (git -C $repo rev-parse HEAD).Trim()
        Set-Content -LiteralPath (Join-Path $repo 'a.txt') -Value 'changed'
        $r = Invoke-LocalChecks -Ids @('build') -PrNumber '1' -HeadSha $sha -EvidenceDir (Join-Path $repo 'ev') -Root $repo
        $r.results.Count | Should -Be 0
        $r.notes -join ' ' | Should -Match 'modified'
        Remove-Item -Recurse -Force $repo
    }

    It 'Invoke-PrDispatch dispatches exactly once on the head branch and waits for the run' {
        $s = New-Scenario -Files @(New-CiCdFile $script:LabJobs)
        $run = New-Run -Id 555 -Path 'ci-cd.yaml' -Event workflow_dispatch
        $inv = {
            param($a)
            $j = $a -join ' '
            $s.Calls.Add($j)
            if ($j -match '^workflow run') { return '' }
            if ($j -match 'git/ref/heads/') { return (@{ object = @{ sha = $s.BranchTip } } | ConvertTo-Json) }
            if ($j -match 'event=workflow_dispatch') { return (@{ workflow_runs = @($run) } | ConvertTo-Json -Depth 5) }
            if ($j -match 'actions/runs\?head_sha') { return (@{ workflow_runs = @() } | ConvertTo-Json) }
            if ($j -match 'actions/runs/555') { return (@{ status = 'completed'; conclusion = 'success' } | ConvertTo-Json) }
            throw "unscripted: $j"
        }.GetNewClosure()
        Initialize-PrVerify -Repo 'o/r' -RepoRoot $script:Root -GhInvoker $inv
        $pr = $s.Pr | ConvertTo-Json -Depth 8 | ConvertFrom-Json
        $d = Invoke-PrDispatch -Map $script:Map -Repo 'o/r' -Pr $pr -Workflow 'ci-cd.yaml' -TimeoutMinutes 1 -FindPollSeconds 0 -RunPollSeconds 0
        $d.Dispatched | Should -BeTrue
        $d.Reason | Should -Match 'completed: success'
        @($s.Calls | Where-Object { $_ -match '^workflow run' }).Count | Should -Be 1
        ($s.Calls | Where-Object { $_ -match '^workflow run' }) | Should -Match '--ref ci/x'
    }

    It 'Invoke-PrDispatch refuses a second dispatch when a run already exists on the head SHA' {
        $s = New-Scenario -Files @()
        $exists = New-Run -Id 556 -Path 'ci-cd.yaml' -Event workflow_dispatch
        $inv = {
            param($a)
            $j = $a -join ' '
            $s.Calls.Add($j)
            if ($j -match 'git/ref/heads/') { return (@{ object = @{ sha = $s.BranchTip } } | ConvertTo-Json) }
            if ($j -match 'actions/runs\?head_sha') { return (@{ workflow_runs = @($exists) } | ConvertTo-Json -Depth 5) }
            throw "unscripted: $j"
        }.GetNewClosure()
        Initialize-PrVerify -Repo 'o/r' -RepoRoot $script:Root -GhInvoker $inv
        $pr = $s.Pr | ConvertTo-Json -Depth 8 | ConvertFrom-Json
        $d = Invoke-PrDispatch -Map $script:Map -Repo 'o/r' -Pr $pr -Workflow 'ci-cd.yaml' -TimeoutMinutes 1 -FindPollSeconds 0 -RunPollSeconds 0
        $d.Dispatched | Should -BeFalse
        $d.Reason | Should -Match 'at most one'
        @($s.Calls | Where-Object { $_ -match '^workflow run' }).Count | Should -Be 0
    }
}
