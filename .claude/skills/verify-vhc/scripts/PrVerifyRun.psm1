#Requires -Version 7
# PrVerifyRun.psm1 - orchestration for `vhc-verify.sh pr`: fetch PR + check data, evaluate, optionally run/dispatch/comment.
# Pure decision logic is in PrVerify.psm1.

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PrVerify.psm1') -Global -DisableNameChecking

$script:Root = $null

function Get-PrNumber {
    param([string]$Pr, [string]$Repo)
    if ($Pr -match '^#?(\d+)$') { return $Matches[1] }
    $r = Invoke-GhJson -GhArgs @('pr', 'view', $Pr, '--repo', $Repo, '--json', 'number')
    if (-not $r) { throw "no PR found for '$Pr' in $Repo" }
    [string]$r.number
}

function Get-RunData {
    param([string]$Repo, [string]$HeadSha)
    @{
        Runs      = @(Get-GhList -Endpoint "repos/$Repo/actions/runs?head_sha=$HeadSha" -Key 'workflow_runs')
        CheckRuns = @(Get-GhList -Endpoint "repos/$Repo/commits/$HeadSha/check-runs" -Key 'check_runs')
        Statuses  = @(Get-GhList -Endpoint "repos/$Repo/commits/$HeadSha/statuses")
    }
}

function Wait-ChecksSettled {
    # Polls until every non-self workflow run / check-run on the SHA is completed and the set has been stable
    # for two polls. Excludes our own workflow so it can never wait on itself. -> $true when settled.
    param([string]$Repo, [string]$HeadSha, [int]$Minutes, [string]$SelfWorkflow, [string]$SelfRunId, [int]$PollSeconds = 30, [int]$GraceSeconds = 90)
    $deadline = (Get-Date).AddMinutes($Minutes)
    $started = Get-Date
    $prev = $null; $stable = 0
    while ($true) {
        $d = Get-RunData -Repo $Repo -HeadSha $HeadSha
        $selfSuites = @($d.Runs | Where-Object { ($SelfWorkflow -and $_.path -eq ".github/workflows/$SelfWorkflow") -or ($SelfRunId -and [string]$_.id -eq $SelfRunId) } | ForEach-Object { $_.check_suite_id })
        $runs = @($d.Runs | Where-Object { $selfSuites -notcontains $_.check_suite_id })
        $crs = @($d.CheckRuns | Where-Object { $selfSuites -notcontains $_.check_suite.id })
        $open = @($runs | Where-Object { $_.status -ne 'completed' }).Count + @($crs | Where-Object { $_.status -ne 'completed' }).Count
        $sig = (($runs | ForEach-Object { "$($_.id):$($_.status)" }) + ($crs | ForEach-Object { "$($_.id):$($_.status)" }) | Sort-Object) -join ','
        if ($open -eq 0 -and $sig -eq $prev) { $stable++ } else { $stable = 0 }
        $prev = $sig
        $aged = ((Get-Date) - $started).TotalSeconds -ge $GraceSeconds
        if ($open -eq 0 -and $stable -ge 1 -and $aged) { return $true }
        if ((Get-Date) -ge $deadline) { return $false }
        Write-Host "waiting for checks: $open still running, $($crs.Count) check-runs seen ($([int](($deadline - (Get-Date)).TotalMinutes)) min left)"
        Start-Sleep -Seconds $PollSeconds
    }
}

function Invoke-LocalChecks {
    # Runs the vhc-verify.sh subcommands (and pester) for the given ids. -> @{ results = id -> {Ok; Evidence}; notes = [] }
    param([string[]]$Ids, [string]$PrNumber, [string]$HeadSha, [string]$EvidenceDir, [string]$Root)
    $results = @{}; $notes = @()
    $head = (git -C $Root rev-parse HEAD).Trim()
    if ($head -ne $HeadSha) {
        return @{ results = $results; notes = @("local checks skipped: checkout is $($head.Substring(0,7)), PR head is $($HeadSha.Substring(0,7)). Run from a worktree of the PR branch (gh pr checkout, or git worktree add) so the evidence is about the right code.") }
    }
    $dirty = @(git -C $Root status --porcelain --untracked-files=no)
    if ($dirty.Count) {
        return @{ results = $results; notes = @("local checks skipped: tracked files are modified in this checkout ($($dirty.Count)); commit or stash so the evidence matches the head SHA.") }
    }
    New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
    $vv = Join-Path $Root '.claude/skills/verify-vhc/scripts/vhc-verify.sh'
    $order = @('build', 'import-report', 'import-scrub', 'cli-checks', 'cli-matrix', 'import-layouts', 'render', 'tests', 'pester')
    $todo = @($order | Where-Object { $Ids -contains $_ })
    $needsBuild = @($todo | Where-Object { $_ -ne 'pester' }).Count -gt 0
    if ($needsBuild -and $todo -notcontains 'build') { $todo = @('build') + $todo }
    $buildFailed = $false
    foreach ($id in $todo) {
        if ($id -ne 'pester' -and $id -ne 'build' -and $buildFailed) { $notes += "local '$id' skipped: build failed"; continue }
        $log = Join-Path $EvidenceDir "run-$id.log"
        Write-Host "local: $id ..."
        Push-Location $Root
        try {
            switch ($id) {
                'pester' {
                    $cmd = '$ErrorActionPreference="Stop"; Import-Module Pester -MinimumVersion 5.0.0; $f=@(git ls-files "*.Tests.ps1"); $c=New-PesterConfiguration; $c.Run.Path=[string[]]$f; $c.Run.Exit=$true; $c.Output.Verbosity="Normal"; Invoke-Pester -Configuration $c'
                    pwsh -NoProfile -Command $cmd *> $log
                }
                'import-scrub' { bash $vv import-scrub "pr-$PrNumber-scrub" *> $log }
                'build' { bash $vv build *> $log }
                default { bash $vv $id "pr-$PrNumber" *> $log }
            }
            $rc = $LASTEXITCODE
        } finally { Pop-Location }
        $tail = (Get-Content -LiteralPath $log -Tail 1 -ErrorAction SilentlyContinue)
        $results[$id] = [pscustomobject]@{ Ok = ($rc -eq 0); Evidence = "$(Resolve-Path -Relative -LiteralPath $log -RelativeBasePath $Root) exit $rc; $tail" }
        if ($id -eq 'build' -and $rc -ne 0) { $buildFailed = $true }
    }
    @{ results = $results; notes = $notes }
}

function Invoke-PrDispatch {
    # ci-cd.yaml on the PR head branch, behind the safety gates. -> @{ Dispatched; Reason; RunId }
    param($Map, [string]$Repo, $Pr, [string]$Workflow, [int]$TimeoutMinutes, [int]$FindPollSeconds = 10, [int]$RunPollSeconds = 60)
    $headBranch = $Pr.head.ref; $headSha = $Pr.head.sha
    $isFork = (-not $Pr.head.repo) -or ($Pr.head.repo.full_name -ne $Pr.base.repo.full_name)
    if ($Pr.state -ne 'open') { return @{ Dispatched = $false; Reason = "PR is $($Pr.state); the head branch may be gone" } }
    $tip = $null
    try { $tip = (Invoke-GhJson -GhArgs @('api', "repos/$Repo/git/ref/heads/$headBranch")).object.sha } catch { $tip = $null }
    $existing = @(Get-GhList -Endpoint "repos/$Repo/actions/runs?head_sha=$headSha" -Key 'workflow_runs')
    $gate = Test-DispatchAllowed -Map $Map -Workflow $Workflow -IsFork $isFork -HeadBranch $headBranch -HeadSha $headSha -BranchTipSha ([string]$tip) -ExistingRuns $existing
    if (-not $gate.Allowed) { return @{ Dispatched = $false; Reason = $gate.Reason } }
    Invoke-Gh -GhArgs @('workflow', 'run', $Workflow, '--repo', $Repo, '--ref', $headBranch) | Out-Null
    Write-Host "dispatched $Workflow on $headBranch ($($headSha.Substring(0,7)))"
    $run = $null
    $find = (Get-Date).AddMinutes(4)
    while (-not $run -and (Get-Date) -lt $find) {
        Start-Sleep -Seconds $FindPollSeconds
        $run = @(Get-GhList -Endpoint "repos/$Repo/actions/runs?head_sha=$headSha&event=workflow_dispatch" -Key 'workflow_runs') |
            Where-Object { $_.path -eq ".github/workflows/$Workflow" } | Select-Object -First 1
    }
    if (-not $run) { return @{ Dispatched = $true; Reason = 'dispatched, but the run did not appear within 4 minutes'; RunId = $null } }
    $end = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $end) {
        $r = Invoke-GhJson -GhArgs @('api', "repos/$Repo/actions/runs/$($run.id)")
        if ($r.status -eq 'completed') { return @{ Dispatched = $true; Reason = "run $($run.id) completed: $($r.conclusion)"; RunId = $run.id } }
        Write-Host "waiting for dispatched run $($run.id): $($r.status)"
        Start-Sleep -Seconds $RunPollSeconds
    }
    @{ Dispatched = $true; Reason = "run $($run.id) still running after $TimeoutMinutes min; re-run this command to fold it in"; RunId = $run.id }
}

function Set-PrStickyComment {
    # Creates or updates the single marker comment. Never creates a second one.
    param([string]$Repo, [string]$PrNumber, [string]$Body)
    $marker = Get-PrVerifyMarker
    $text = "$marker`n$Body"
    if ($text.Length -gt 60000) { $text = $text.Substring(0, 60000) + "`n`n_(report truncated; full report is in the job summary / report.md)_" }
    $comments = @(Get-GhList -Endpoint "repos/$Repo/issues/$PrNumber/comments")
    $mine = @($comments | Where-Object { $_.body -and $_.body.Contains($marker) } | Sort-Object id)
    if ($mine.Count -ge 1) {
        $r = Invoke-GhJson -GhArgs @('api', '--method', 'PATCH', "repos/$Repo/issues/comments/$($mine[0].id)", '-f', "body=$text")
        $extra = if ($mine.Count -gt 1) { " ($($mine.Count - 1) other marker comment(s) left untouched: $(($mine | Select-Object -Skip 1 | ForEach-Object { $_.html_url }) -join ', '))" } else { '' }
        return "updated sticky comment $($r.html_url)$extra"
    }
    $r = Invoke-GhJson -GhArgs @('api', '--method', 'POST', "repos/$Repo/issues/$PrNumber/comments", '-f', "body=$text")
    "created sticky comment $($r.html_url)"
}

function Invoke-PrVerification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrRef,
        [string]$Repo,
        [string]$RepoRoot,
        [switch]$Run,
        [switch]$Dispatch,
        [switch]$Comment,
        [int]$WaitMinutes = 0,
        [string]$SelfWorkflow = '',
        [string]$OutDir,
        [int]$DispatchTimeoutMinutes = 120,
        [scriptblock]$GhInvoker
    )
    if (-not $RepoRoot) { $RepoRoot = (git rev-parse --show-toplevel).Trim() }
    $script:Root = $RepoRoot
    Initialize-PrVerify -Repo $Repo -RepoRoot $RepoRoot -GhInvoker $GhInvoker
    if (-not $Repo) {
        if ($env:GITHUB_REPOSITORY) { $Repo = $env:GITHUB_REPOSITORY }
        else {
            Push-Location $RepoRoot
            try { $Repo = (gh repo view --json nameWithOwner -q .nameWithOwner).Trim() } finally { Pop-Location }
        }
        Initialize-PrVerify -Repo $Repo -RepoRoot $RepoRoot -GhInvoker $GhInvoker
    }
    $map = Read-PrSurfaceMap -Path (Join-Path $RepoRoot '.claude/skills/verify-vhc/pr-surfaces.json')
    $prNum = Get-PrNumber -Pr $PrRef -Repo $Repo
    $pr = Invoke-GhJson -GhArgs @('api', "repos/$Repo/pulls/$prNum")
    $headSha = $pr.head.sha; $baseSha = $pr.base.sha
    $isFork = (-not $pr.head.repo) -or ($pr.head.repo.full_name -ne $pr.base.repo.full_name)
    $files = @(Get-GhList -Endpoint "repos/$Repo/pulls/$prNum/files")
    $commits = @(Get-GhList -Endpoint "repos/$Repo/pulls/$prNum/commits")
    $notes = [System.Collections.Generic.List[string]]::new()
    if ($files.Count -ge 3000) { $notes.Add('PR has 3000+ files; the API truncates the file list, so unseen files are not mapped.') }

    $settled = $true
    if ($WaitMinutes -gt 0) {
        $settled = Wait-ChecksSettled -Repo $Repo -HeadSha $headSha -Minutes $WaitMinutes -SelfWorkflow $SelfWorkflow -SelfRunId $env:GITHUB_RUN_ID
        if (-not $settled) { $notes.Add("checks had not settled after $WaitMinutes minutes; anything still running is reported as pending.") }
    }

    $names = @($files | ForEach-Object { $_.filename })
    $resolved = Resolve-FileSurfaces -Map $map -Files $names
    $evidenceDir = if ($OutDir) { $OutDir } else { Join-Path $RepoRoot ".verify-vhc/evidence/pr-$prNum" }
    $wfDir = Join-Path $RepoRoot '.github/workflows'
    $changedWf = @($files | Where-Object { $_.filename -match '^\.github/workflows/[^/]+\.ya?ml$' })

    # Workflow text at the head SHA: display names for every workflow a touched check refers to, line ranges for changed ones.
    $wfJobs = @{}
    $loadWf = {
        param($name)
        if ($wfJobs.ContainsKey($name)) { return }
        $t = Get-FileAtRef -Path ".github/workflows/$name" -Ref $headSha
        $wfJobs[$name] = if ($null -ne $t) { @(Get-WorkflowJobs -Text $t) } else { @() }
    }
    foreach ($sid in $resolved.Surfaces.Keys) {
        foreach ($c in @($map.surfaces.$sid.checks)) { foreach ($e in @(Get-MapProp $c 'ci' @())) { & $loadWf $e.workflow } }
    }
    $wfChange = @{}
    foreach ($f in $changedWf) {
        $name = Split-Path $f.filename -Leaf
        & $loadWf $name
        if ($f.status -eq 'removed') { $wfChange[$name] = @{ Removed = $true; TouchedJobs = @() }; continue }
        $touched = Get-PatchTouchedLines -Patch $f.patch
        $wfChange[$name] = @{ Removed = $false; TouchedJobs = @(Get-TouchedJobIds -Jobs $wfJobs[$name] -TouchedLines $touched) }
    }

    # Builtin static checks (run once, results shared by every surface that lists them).
    $builtin = @{
        'docs-sanity'      = Invoke-DocsSanity -ChangedFiles $files -HeadSha $headSha -BaseSha $baseSha
        'changelog-format' = Invoke-ChangelogFormat -ChangedFiles $files -HeadSha $headSha -BaseSha $baseSha
        'shell-syntax'     = Invoke-ShellSyntax -ChangedFiles $files -HeadSha $headSha
        'json-syntax'      = Invoke-JsonSyntax -ChangedFiles $files -HeadSha $headSha
    }

    $local = @{}
    $localNotes = @()
    $dispatchNote = $null

    $evaluate = {
        $d = Get-RunData -Repo $Repo -HeadSha $headSha
        $selfSuites = @($d.Runs | Where-Object { $SelfWorkflow -and $_.path -eq ".github/workflows/$SelfWorkflow" } | ForEach-Object { $_.check_suite_id })
        $ctx = @{
            Runs = @($d.Runs); Statuses = @($d.Statuses); CheckRuns = @($d.CheckRuns | Where-Object { $selfSuites -notcontains $_.check_suite.id })
            WorkflowName = $wfJobs; WorkflowChange = $wfChange; SelfWorkflow = $SelfWorkflow; IsFork = $isFork
            Builtin = $builtin; Local = $local; Branch = $pr.head.ref; PrNumber = $prNum
            RepoFlag = " --repo $Repo"; Claimed = [System.Collections.Generic.HashSet[string]]::new()
        }
        $surfaces = @()
        foreach ($sid in $resolved.Surfaces.Keys) {
            $s = $map.surfaces.$sid
            $checks = @()
            if ((Get-MapProp $s 'kind') -eq 'workflow') {
                $checks = @(Get-WorkflowSurfaceChecks -Map $map -Ctx $ctx -ChangedWorkflows @($resolved.Surfaces[$sid] | ForEach-Object { $_ }))
            } else {
                foreach ($c in @($s.checks)) { $checks += Get-CheckResult -Map $map -Check $c -Ctx $ctx -Required ([bool](Get-MapProp $c 'required' $true)) }
            }
            $surfaces += [pscustomobject]@{ id = $sid; title = $s.title; files = @($resolved.Surfaces[$sid]); checks = $checks; verdict = (Get-SurfaceVerdict -Checks $checks); residue = @(Get-MapProp $s 'residue' @()) }
        }
        $other = @()
        foreach ($cr in $ctx.CheckRuns) {
            if ($ctx.Claimed.Contains([string]$cr.id)) { continue }
            $st = ConvertTo-CheckState $cr
            $other += [pscustomobject]@{ name = $cr.name; state = $st; url = $cr.html_url }
        }
        @{ Surfaces = $surfaces; Other = $other; Ctx = $ctx }
    }

    $ev = & $evaluate

    if ($Run) {
        $ids = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($sid in $resolved.Surfaces.Keys) {
            foreach ($c in @($map.surfaces.$sid.checks)) {
                $r = @($ev.Surfaces | Where-Object id -eq $sid | ForEach-Object { $_.checks } | Where-Object { $_.Id -eq $c.id -and $_.Status -eq 'not-run' })
                if ($r.Count) { foreach ($l in @(Get-MapProp $c 'local' @())) { [void]$ids.Add($l) } }
            }
        }
        if ($ids.Count) {
            $lr = Invoke-LocalChecks -Ids @($ids) -PrNumber $prNum -HeadSha $headSha -EvidenceDir $evidenceDir -Root $RepoRoot
            foreach ($k in $lr.results.Keys) { $local[$k] = $lr.results[$k] }
            $localNotes = @($lr.notes)
            $ev = & $evaluate
        } else { $localNotes = @('--run: nothing left to run locally for the touched surfaces.') }
    }

    if ($Dispatch) {
        $labPending = @($ev.Surfaces | ForEach-Object { $_.checks } | Where-Object { $_.Lab -and $_.Status -eq 'not-run' })
        if ($labPending.Count -eq 0) { $dispatchNote = '--dispatch: no live-lab check is missing, nothing dispatched.' }
        else {
            $dr = Invoke-PrDispatch -Map $map -Repo $Repo -Pr $pr -Workflow 'ci-cd.yaml' -TimeoutMinutes $DispatchTimeoutMinutes
            $dispatchNote = "--dispatch: $($dr.Reason)"
            if ($dr.Dispatched) { $ev = & $evaluate }
        }
    }

    # Hygiene and verdict
    $hyg = Get-Hygiene -Map $map -Pr $pr -Files $files -Commits $commits -HeadSha $headSha -BaseSha $baseSha -WorkflowDir $wfDir -ConventionalModule (Join-Path $RepoRoot '.github/scripts/VhcVersion.psm1')
    $hygFailed = @($hyg.Items | Where-Object Level -eq 'fail').Count -gt 0
    $otherFailed = @($ev.Other | Where-Object state -eq 'failed').Count -gt 0
    $otherPending = @($ev.Other | Where-Object state -eq 'pending').Count -gt 0
    $verdict = Get-OverallVerdict -SurfaceVerdicts @($ev.Surfaces | ForEach-Object { $_.verdict }) -HasUnmapped ($resolved.Unmapped.Count -gt 0) -OtherFailed $otherFailed -HygieneFailed $hygFailed -OtherPending $otherPending
    if ($names.Count -eq 0) { $verdict = 'NEEDS-HUMAN'; $notes.Add('the PR has no changed files.') }

    # Next actions
    $next = [System.Collections.Generic.List[string]]::new()
    foreach ($s in $ev.Surfaces) {
        foreach ($c in $s.checks) {
            switch ($c.Status) {
                'failed' { $next.Add("FIX: $($s.id) / $($c.Title): $($c.Detail)") }
                'pending' { $next.Add("WAIT: $($s.id) / $($c.Title) is still running$(if ($c.Url) { ": $($c.Url)" })") }
                'not-run' { foreach ($r in @($c.Remedy)) { $next.Add("EXERCISE ($($s.id) / $($c.Title)): $r") } ; if (@($c.Remedy).Count -eq 0) { $next.Add("EXERCISE ($($s.id) / $($c.Title)): no automatic route; see the check detail") } }
                'human' { $next.Add("HUMAN ($($s.id) / $($c.Title)): $($c.Detail)") }
            }
        }
    }
    foreach ($o in $ev.Other) { if ($o.state -eq 'failed') { $next.Add("FIX: check '$($o.name)' failed on this SHA: $($o.url)") } elseif ($o.state -eq 'pending') { $next.Add("WAIT: check '$($o.name)' is still running") } }
    if ($resolved.Unmapped.Count) { $next.Add("MAP: add $($resolved.Unmapped.Count) unmapped file(s) to .claude/skills/verify-vhc/pr-surfaces.json") }
    foreach ($h in $hyg.Items) { if ($h.Level -in 'fail', 'warn') { $next.Add("HYGIENE ($($h.Level)) $($h.Id): $($h.Message)") } }
    $nextU = @($next | Select-Object -Unique)

    $allChecks = @($ev.Surfaces | ForEach-Object { $_.checks } | Where-Object { $_.Required })
    $count = { param($st) @($allChecks | Where-Object { $_.Status -eq $st }).Count }
    $summary = "$($ev.Surfaces.Count) surface(s) touched by $($names.Count) file(s); required checks: $(& $count 'passed') passed, $(& $count 'failed') failed, $(& $count 'pending') pending, $(& $count 'not-run') not exercised, $(& $count 'human') need a human. $(if ($resolved.Unmapped.Count) { "$($resolved.Unmapped.Count) unmapped file(s). " })Hygiene: $(@($hyg.Items | Where-Object Level -eq 'fail').Count) fail, $(@($hyg.Items | Where-Object Level -eq 'warn').Count) warn."
    foreach ($n in @($localNotes) + @($dispatchNote) | Where-Object { $_ }) { $notes.Add($n) }

    $residue = @($ev.Surfaces | ForEach-Object { $_.residue } | Where-Object { $_ } | Select-Object -Unique)
    $exit = switch ($verdict) { 'VERIFIED' { 0 } 'FAILED' { 1 } 'NEEDS-RUN' { 2 } 'NEEDS-HUMAN' { 3 } }

    $report = [ordered]@{
        schemaVersion = 1
        verdict       = $verdict
        exitCode      = $exit
        generatedAt   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [cultureinfo]::InvariantCulture)
        options       = [ordered]@{ run = [bool]$Run; dispatch = [bool]$Dispatch; comment = [bool]$Comment; waitMinutes = $WaitMinutes; checksSettled = $settled }
        pr            = [ordered]@{
            number = [int]$prNum; title = $pr.title; url = $pr.html_url; state = $(if ($pr.merged) { 'merged' } else { $pr.state }); draft = [bool]$pr.draft
            baseBranch = $pr.base.ref; baseSha = $baseSha; headBranch = $pr.head.ref; headSha = $headSha; sameRepo = (-not $isFork); author = $pr.user.login
        }
        summary       = $summary
        nextActions   = $nextU
        surfaces      = @($ev.Surfaces | ForEach-Object {
                [ordered]@{ id = $_.id; title = $_.title; verdict = $_.verdict; files = $_.files; checks = @($_.checks | ForEach-Object { [ordered]@{ id = $_.Id; title = $_.Title; status = $_.Status; required = $_.Required; kind = $_.Kind; detail = $_.Detail; url = $_.Url; remedy = @($_.Remedy) } }) }
            })
        unmapped      = @($resolved.Unmapped)
        hygiene       = @($hyg.Items | ForEach-Object { [ordered]@{ id = $_.Id; level = $_.Level; message = $_.Message } })
        otherChecks   = @($ev.Other | ForEach-Object { [ordered]@{ name = $_.name; state = $_.state; url = $_.url } })
        residue       = $residue
        notes         = @($notes)
        files         = $names
    }
    $obj = $report | ConvertTo-Json -Depth 16 | ConvertFrom-Json -Depth 16
    $md = Format-PrReportMarkdown -R $obj
    if ($notes.Count) { $md += "`n## Notes`n`n" + (($notes | ForEach-Object { "- $_" }) -join "`n") + "`n" }

    New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
    Set-Content -LiteralPath (Join-Path $evidenceDir 'report.md') -Value $md -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $evidenceDir 'report.json') -Value ($report | ConvertTo-Json -Depth 16) -Encoding utf8NoBOM

    $commentNote = $null
    if ($Comment) {
        if ($isFork) { $commentNote = 'comment skipped: fork PR (the token cannot write); report is in the job summary / report.md' }
        else { $commentNote = Set-PrStickyComment -Repo $Repo -PrNumber $prNum -Body $md }
    }
    [pscustomobject]@{ Verdict = $verdict; ExitCode = $exit; Markdown = $md; Report = $obj; EvidenceDir = $evidenceDir; CommentNote = $commentNote }
}

Export-ModuleMember -Function Invoke-PrVerification, Get-PrNumber, Wait-ChecksSettled, Invoke-PrDispatch, Set-PrStickyComment, Invoke-LocalChecks
