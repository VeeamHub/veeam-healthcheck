#Requires -Version 7
# PrVerify.psm1 - decide whether a pull request's green checks actually exercised what it changed.
#
# Pure logic lives here so Pester can drive it with synthetic data; Invoke-PrVerify.ps1 is the CLI.
# Design: .claude/skills/verify-vhc/features/pr-verification.md

$ErrorActionPreference = 'Stop'

$script:KnownLocal    = @('build', 'import-report', 'import-scrub', 'cli-checks', 'cli-matrix', 'import-layouts', 'render', 'tests', 'pester')
$script:KnownBuiltin  = @('docs-sanity', 'changelog-format', 'shell-syntax', 'json-syntax')
$script:KnownStatusStep = @('build', 'xunit', 'live', 'gui')
$script:KnownTrigger  = @('pr', 'pr-path-filtered', 'pr-same-repo-only', 'pr-fork-only', 'push-or-dispatch', 'dispatch-only', 'publish-only', 'scheduled', 'self')
$script:ReportMarker  = '<!-- vhc-pr-verify -->'
$script:Repo          = $null
$script:RepoRoot      = $null
$script:GhInvoker     = $null     # tests replace this; gets [string[]] args, returns stdout text
$script:Cache         = @{}

function Get-PrVerifyKnownLocal { $script:KnownLocal }
function Get-PrVerifyKnownBuiltin { $script:KnownBuiltin }
function Get-PrVerifyKnownTrigger { $script:KnownTrigger }
function Get-PrVerifyKnownStatusStep { $script:KnownStatusStep }
function Get-PrVerifyMarker { $script:ReportMarker }

function Initialize-PrVerify {
    param([string]$Repo, [string]$RepoRoot, [scriptblock]$GhInvoker)
    $script:Repo = $Repo
    $script:RepoRoot = $RepoRoot
    $script:GhInvoker = $GhInvoker
    $script:Cache = @{}
}

# ---------------------------------------------------------------------------
# gh plumbing
# ---------------------------------------------------------------------------

function Invoke-Gh {
    param([Parameter(Mandatory)][string[]]$GhArgs)
    if ($script:GhInvoker) { return (& $script:GhInvoker $GhArgs) }
    $all = & gh @GhArgs 2>&1
    $rc = $LASTEXITCODE
    $err = @($all | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { $_.ToString() })
    $out = @($all | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] })
    if ($rc -ne 0) { throw "gh $($GhArgs -join ' ') failed (exit $rc): $($err -join ' ')$($out -join ' ')" }
    $text = ($out -join "`n")
    if ($env:PRVERIFY_RECORD_DIR) {
        New-Item -ItemType Directory -Force -Path $env:PRVERIFY_RECORD_DIR | Out-Null
        Set-Content -LiteralPath (Join-Path $env:PRVERIFY_RECORD_DIR (Get-GhFixtureName $GhArgs)) -Value $text -Encoding utf8NoBOM
    }
    $text
}

function Get-GhFixtureName {
    param([string[]]$GhArgs)
    $bytes = [Text.Encoding]::UTF8.GetBytes(($GhArgs -join "`u{1F}"))
    $hash = [BitConverter]::ToString([Security.Cryptography.SHA1]::HashData($bytes)).Replace('-', '').ToLowerInvariant()
    "$hash.json"
}

function Invoke-GhJson {
    param([Parameter(Mandatory)][string[]]$GhArgs)
    $text = Invoke-Gh -GhArgs $GhArgs
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    $text | ConvertFrom-Json -Depth 64
}

function Get-GhList {
    # Pages through a REST list endpoint. $Key names the array inside an envelope object (check_runs, workflow_runs).
    param([Parameter(Mandatory)][string]$Endpoint, [string]$Key)
    $all = [System.Collections.Generic.List[object]]::new()
    $page = 1
    do {
        $sep = if ($Endpoint.Contains('?')) { '&' } else { '?' }
        $r = Invoke-GhJson -GhArgs @('api', "$Endpoint${sep}per_page=100&page=$page")
        $items = if ($Key) { @($r.$Key) } else { @($r) }
        foreach ($i in $items) { if ($null -ne $i) { $all.Add($i) } }
        $page++
    } while ($items.Count -ge 100 -and $page -le 30)
    $all.ToArray()
}

function ConvertTo-UrlPath([string]$Path) { ($Path -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/' }

function Get-FileAtRef {
    # File text at a commit: local object database first, then the contents API. $null when absent.
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Ref)
    $key = "file|$Ref|$Path"
    if ($script:Cache.ContainsKey($key)) { return $script:Cache[$key] }
    $text = $null
    if ($script:RepoRoot -and -not $env:PRVERIFY_NO_GIT) {
        $t = git -C $script:RepoRoot cat-file -p "${Ref}:${Path}" 2>$null
        if ($LASTEXITCODE -eq 0) { $text = ($t -join "`n") }
    }
    if ($null -eq $text -and $script:Repo) {
        try { $text = Invoke-Gh -GhArgs @('api', "repos/$($script:Repo)/contents/$(ConvertTo-UrlPath $Path)?ref=$Ref", '-H', 'Accept: application/vnd.github.raw') } catch { $text = $null }
    }
    $script:Cache[$key] = $text
    $text
}

function Get-TreePaths {
    param([Parameter(Mandatory)][string]$Ref)
    $key = "tree|$Ref"
    if ($script:Cache.ContainsKey($key)) { return $script:Cache[$key] }
    $paths = $null
    if ($script:RepoRoot -and -not $env:PRVERIFY_NO_GIT) {
        $t = git -C $script:RepoRoot ls-tree -r --name-only $Ref 2>$null
        if ($LASTEXITCODE -eq 0) { $paths = @($t) }
    }
    if ($null -eq $paths -and $script:Repo) {
        try {
            $r = Invoke-GhJson -GhArgs @('api', "repos/$($script:Repo)/git/trees/${Ref}?recursive=1")
            $paths = @($r.tree | Where-Object { $_.type -eq 'blob' } | ForEach-Object { $_.path })
        } catch { $paths = @() }
    }
    $script:Cache[$key] = $paths
    $paths
}

# ---------------------------------------------------------------------------
# Globs and the surface map
# ---------------------------------------------------------------------------

function ConvertTo-GlobRegex {
    param([Parameter(Mandatory)][string]$Glob)
    $sb = [System.Text.StringBuilder]::new('^')
    $i = 0
    while ($i -lt $Glob.Length) {
        $c = $Glob[$i]
        if ($c -eq '*') {
            if ($i + 1 -lt $Glob.Length -and $Glob[$i + 1] -eq '*') {
                if ($i + 2 -lt $Glob.Length -and $Glob[$i + 2] -eq '/') { [void]$sb.Append('(?:.*/)?'); $i += 3; continue }
                [void]$sb.Append('.*'); $i += 2; continue
            }
            [void]$sb.Append('[^/]*'); $i++; continue
        }
        if ($c -eq '?') { [void]$sb.Append('[^/]'); $i++; continue }
        [void]$sb.Append([regex]::Escape([string]$c)); $i++
    }
    [void]$sb.Append('$')
    $sb.ToString()
}

function Test-GlobMatch {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string[]]$Globs)
    foreach ($g in $Globs) { if ($Path -cmatch (ConvertTo-GlobRegex $g)) { return $true } }
    $false
}

function Read-PrSurfaceMap {
    param([Parameter(Mandatory)][string]$Path)
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 32
}

function Get-MapProp {
    param($Object, [string]$Name, $Default = $null)
    if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) { return $Object.$Name }
    $Default
}

function Resolve-FileSurfaces {
    # -> @{ Surfaces = ordered id -> [paths]; Unmapped = [paths] }
    param([Parameter(Mandatory)]$Map, [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Files)
    $by = [ordered]@{}
    $unmapped = [System.Collections.Generic.List[string]]::new()
    foreach ($f in $Files) {
        $hit = $false
        foreach ($p in $Map.surfaces.PSObject.Properties) {
            $s = $p.Value
            if (-not (Test-GlobMatch -Path $f -Globs @($s.paths))) { continue }
            $ex = @(Get-MapProp $s 'exclude' @())
            if ($ex.Count -gt 0 -and (Test-GlobMatch -Path $f -Globs $ex)) { continue }
            if (-not $by.Contains($p.Name)) { $by[$p.Name] = [System.Collections.Generic.List[string]]::new() }
            $by[$p.Name].Add($f)
            $hit = $true
        }
        if (-not $hit) { $unmapped.Add($f) }
    }
    @{ Surfaces = $by; Unmapped = $unmapped.ToArray() }
}

# ---------------------------------------------------------------------------
# Workflow files: jobs, display names, touched jobs
# ---------------------------------------------------------------------------

function Get-WorkflowJobs {
    # Parses the top-level jobs of a workflow's text. -> list of @{ Id; Name; Start; End } (1-based inclusive lines)
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Text)
    $lines = $Text -split "`r?`n"
    $jobsAt = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^jobs:\s*(#.*)?$') { $jobsAt = $i; break } }
    if ($jobsAt -lt 0) { return @() }
    $end = $lines.Count - 1
    for ($i = $jobsAt + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^[A-Za-z_]') { $end = $i - 1; break }
    }
    $starts = @()
    for ($i = $jobsAt + 1; $i -le $end; $i++) {
        if ($lines[$i] -match '^  ([A-Za-z0-9_-]+):\s*(#.*)?$') { $starts += , @($i, $Matches[1]) }
    }
    # A job-level (two-space) comment block directly above a job key belongs to that job, not the one before it;
    # otherwise a new job's header comment counts as an edit to the previous job. A blank line belongs to the
    # next job only when it sits inside such a comment block, so the blank separator between jobs stays put.
    $lead = @()
    for ($k = 0; $k -lt $starts.Count; $k++) {
        $b = $starts[$k][0]
        while ($k -gt 0 -and $b - 1 -gt $starts[$k - 1][0]) {
            $above = $lines[$b - 1]
            if ($above -match '^  #') { $b--; continue }
            if ($above -match '^\s*$' -and $b - 2 -gt $starts[$k - 1][0] -and $lines[$b - 2] -match '^  #' -and $lines[$b] -match '^  #') { $b--; continue }
            break
        }
        $lead += $b
    }
    $jobs = @()
    for ($k = 0; $k -lt $starts.Count; $k++) {
        $s = $lead[$k]
        $e = if ($k + 1 -lt $starts.Count) { $lead[$k + 1] - 1 } else { $end }
        $name = $starts[$k][1]
        $key = $starts[$k][0]
        for ($j = $key + 1; $j -le [Math]::Min($e, $key + 6); $j++) {
            if ($lines[$j] -match '^    name:\s*(.+?)\s*$') {
                $n = $Matches[1] -replace '\s+#.*$', ''
                $name = $n.Trim("'", '"')
                break
            }
        }
        $jobs += [pscustomobject]@{ Id = $starts[$k][1]; Name = $name; Start = $s + 1; End = $e + 1 }
    }
    $jobs
}

function Get-PatchTouchedLines {
    # New-file line numbers an added or removed line sits at, from a unified-diff patch. $null patch => $null (unknown).
    param([AllowNull()][string]$Patch)
    if ([string]::IsNullOrEmpty($Patch)) { return $null }
    $touched = [System.Collections.Generic.HashSet[int]]::new()
    $new = 0
    foreach ($l in ($Patch -split "`r?`n")) {
        if ($l -match '^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@') { $new = [int]$Matches[1]; continue }
        if ($l.StartsWith('+')) { [void]$touched.Add($new); $new++ }
        elseif ($l.StartsWith('-')) { [void]$touched.Add($new) }
        elseif ($l.StartsWith('\')) { }
        else { $new++ }
    }
    , @($touched | Sort-Object)
}

function Get-TouchedJobIds {
    # Which jobs a workflow edit touched. Edits outside every job (triggers, env, permissions) touch all of them.
    param($Jobs, [AllowNull()]$TouchedLines)
    $all = @($Jobs | ForEach-Object { $_.Id })
    if ($null -eq $TouchedLines -or @($TouchedLines).Count -eq 0) { return $all }
    $hit = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($ln in $TouchedLines) {
        $owner = $Jobs | Where-Object { $ln -ge $_.Start -and $ln -le $_.End } | Select-Object -First 1
        if ($owner) { [void]$hit.Add($owner.Id) } else { return $all }
    }
    @($all | Where-Object { $hit.Contains($_) })
}

# ---------------------------------------------------------------------------
# Real check data -> per-job status
# ---------------------------------------------------------------------------

function ConvertTo-CheckState {
    param($CheckRun)
    if ($CheckRun.status -ne 'completed') { return 'pending' }
    switch ($CheckRun.conclusion) {
        { $_ -in 'success', 'neutral' } { 'passed' }
        'skipped' { 'skipped' }
        { $_ -in 'failure', 'timed_out', 'startup_failure', 'stale' } { 'failed' }
        { $_ -in 'cancelled', 'action_required' } { 'cancelled' }
        default { 'cancelled' }
    }
}

function Merge-CheckStates {
    # matrix legs or any-of alternatives -> one state
    param([string[]]$States)
    $s = @($States)
    if ($s.Count -eq 0) { return 'none' }
    if ($s -contains 'failed') { return 'failed' }
    if ($s -contains 'pending') { return 'pending' }
    if ($s -contains 'passed') { return 'passed' }
    if ($s -contains 'cancelled') { return 'cancelled' }
    'skipped'
}

function Get-JobStatus {
    # Latest workflow run (on the head SHA) of $Workflow that has a check-run for $DisplayName.
    param($Runs, $CheckRuns, [string]$Workflow, [string]$DisplayName)
    $path = ".github/workflows/$Workflow"
    $mine = @($Runs | Where-Object { $_.path -eq $path } | Sort-Object { [datetime]$_.created_at } -Descending)
    $rx = '^' + [regex]::Escape($DisplayName) + '( \(.*\))?$'
    foreach ($run in $mine) {
        $crs = @($CheckRuns | Where-Object { $_.check_suite.id -eq $run.check_suite_id -and $_.name -match $rx })
        if ($crs.Count -eq 0) { continue }
        $state = Merge-CheckStates @($crs | ForEach-Object { ConvertTo-CheckState $_ })
        return [pscustomobject]@{
            State = $state; CheckRunIds = @($crs | ForEach-Object { $_.id }); RunId = $run.id; Event = $run.event; Url = $run.html_url
            Detail = ($crs | ForEach-Object { "$($_.name): $(if ($_.status -eq 'completed') { $_.conclusion } else { $_.status })" }) -join '; '
        }
    }
    $any = $mine | Select-Object -First 1
    [pscustomobject]@{
        State = 'none'; CheckRunIds = @(); RunId = $(if ($any) { $any.id } else { $null }); Event = $(if ($any) { $any.event } else { $null }); Url = $(if ($any) { $any.html_url } else { $null })
        Detail = $(if ($any) { "workflow ran ($($any.event)) but produced no '$DisplayName' job" } else { "no run of $Workflow on this SHA" })
    }
}

# ---------------------------------------------------------------------------
# Check evaluation
# ---------------------------------------------------------------------------

function New-CheckResult {
    param([string]$Id, [string]$Title, [string]$Status, [string]$Detail, [string[]]$Remedy = @(), [bool]$Required = $true, [string]$Kind = 'ci', [string]$Url = '', [bool]$Lab = $false)
    [pscustomobject]@{ Id = $Id; Title = $Title; Status = $Status; Detail = $Detail; Remedy = @($Remedy); Required = $Required; Kind = $Kind; Url = $Url; Lab = $Lab }
}

function Get-TriggerText {
    param([string]$Trigger)
    switch ($Trigger) {
        'pr' { 'runs on every PR' }
        'pr-path-filtered' { 'PR-triggered only when the diff touches its path filters' }
        'pr-same-repo-only' { 'PR-triggered, same-repo PRs only' }
        'pr-fork-only' { 'PR-triggered, fork PRs only' }
        'push-or-dispatch' { 'does not run on PRs; push to master/dev or workflow_dispatch only' }
        'dispatch-only' { 'workflow_dispatch only' }
        'publish-only' { 'publish path only' }
        'scheduled' { 'cron only' }
        'self' { 'this verification workflow' }
        default { $Trigger }
    }
}

function Get-CiRemedy {
    # What would exercise a CI job that is absent / skipped.
    param($Map, [string]$Workflow, [string]$Job, [string]$Trigger, [bool]$Lab, [string]$Branch, [string]$Pr, [string]$RepoFlag, [string[]]$Local)
    $wf = Get-MapProp $Map.workflows $Workflow
    $r = @()
    if ($Lab) { $r += "$($script:VerifyCmd) pr $Pr --dispatch    # dispatches ci-cd.yaml on '$Branch' (self-hosted lab; one dispatch per head SHA)" }
    elseif ($wf -and (Get-MapProp $wf 'dispatch' $false) -and $Trigger -notin 'publish-only', 'scheduled') {
        $r += "gh workflow run $Workflow --ref $Branch$RepoFlag"
    }
    if (@($Local).Count -gt 0) { $r += "$($script:VerifyCmd) pr $Pr --run    # runs: $(@($Local) -join ', ')" }
    if ($r.Count -eq 0 -and $Trigger -in 'pr', 'pr-path-filtered', 'pr-same-repo-only', 'pr-fork-only') {
        $r += "push a commit that matches $Workflow's triggers, or re-run the workflow from the Actions tab"
    }
    $r
}
$script:VerifyCmd = '.claude/skills/verify-vhc/scripts/vhc-verify.sh'

function Resolve-CiEntries {
    # any-of CI alternatives -> combined status object
    param($Map, $Entries, $Ctx)
    $alts = @()
    foreach ($e in @($Entries)) {
        $wf = Get-MapProp $Map.workflows $e.workflow
        $jobMeta = if ($wf) { Get-MapProp $wf.jobs $e.job } else { $null }
        $trigger = if ($jobMeta) { $jobMeta.trigger } else { 'unknown' }
        $lab = [bool](Get-MapProp $jobMeta 'lab' $false)
        $disp = $e.job
        $wfText = $Ctx.WorkflowName[$e.workflow]
        if ($wfText) { $j = $wfText | Where-Object { $_.Id -eq $e.job } | Select-Object -First 1; if ($j) { $disp = $j.Name } }
        if ($trigger -eq 'self' -and $Ctx.SelfWorkflow -eq $e.workflow) {
            $st = [pscustomobject]@{ State = 'passed'; RunId = $null; Event = 'this run'; Url = ''; Detail = 'this verification run' }
        } elseif ($trigger -eq 'pr-fork-only' -and -not $Ctx.IsFork) {
            $st = [pscustomobject]@{ State = 'skipped'; RunId = $null; Event = $null; Url = ''; Detail = 'fork-only job; this PR is same-repo' }
        } elseif ($trigger -eq 'pr-same-repo-only' -and $Ctx.IsFork) {
            $st = [pscustomobject]@{ State = 'skipped'; RunId = $null; Event = $null; Url = ''; Detail = 'same-repo-only job; this PR is from a fork' }
        } else {
            $st = Get-JobStatus -Runs $Ctx.Runs -CheckRuns $Ctx.CheckRuns -Workflow $e.workflow -DisplayName $disp
            foreach ($cid in @($st.CheckRunIds)) { [void]$Ctx.Claimed.Add([string]$cid) }
        }
        $alts += [pscustomobject]@{ Workflow = $e.workflow; Job = $e.job; Display = $disp; Trigger = $trigger; Lab = $lab; Status = $st }
    }
    $state = Merge-CheckStates @($alts | ForEach-Object { $_.Status.State -replace '^none$', 'skipped' })
    # a job nobody ever started is 'not-run', distinct from a job that was started and skipped
    $nonePresent = (@($alts | Where-Object { $_.Status.State -eq 'none' }).Count -gt 0)
    if ($state -eq 'skipped' -and $nonePresent) { $state = 'none' }
    [pscustomobject]@{ State = $state; Alts = $alts }
}

function Get-StatusStepResult {
    # A commit status posted by `vhc-verify.sh winhost --status` on the head SHA -> did it exercise $Step?
    # The description is a fixed counts-only grammar (see vhc-verify.sh wh_status_description):
    #   build ok | build FAIL; xunit P pass F fail S skip; live ok N checks | live FAIL K of N checks; gui P pass F fail S skip
    # Only the newest status of the context counts, so a later failing run overrides an earlier green one. A step that is
    # not named in the description was not run and is reported 'none', never passed.
    param($Statuses, [string]$Context, [string]$Step)
    $latest = @($Statuses | Where-Object { $_.context -eq $Context } | Sort-Object { [datetime]$_.created_at }, { [long]$_.id } -Descending) | Select-Object -First 1
    if (-not $latest) { return [pscustomobject]@{ State = 'none'; Detail = "no '$Context' status on this SHA"; Url = '' } }
    $url = [string]$latest.target_url
    $segment = @(([string]$latest.description) -split ';\s*' | Where-Object { $_ -match "^$([regex]::Escape($Step))\b" }) | Select-Object -First 1
    if (-not $segment) { return [pscustomobject]@{ State = 'none'; Detail = "status '$Context' ($($latest.state)) does not cover '$Step': $($latest.description)"; Url = $url } }
    $rest = ($segment -replace "^$([regex]::Escape($Step))\s*", '')
    $state =
        if ($rest -cmatch '\bFAIL\b') { 'failed' }
        elseif ($rest -match '^(\d+) pass (\d+) fail(?: (\d+) skip)?') {
            # a GUI suite with skipped tests did not run everything (the full-Run test needs --live), so it does not count
            if ([int]$Matches[2] -gt 0) { 'failed' } elseif ([int]$Matches[1] -gt 0 -and -not ($Step -eq 'gui' -and $Matches[3] -and [int]$Matches[3] -gt 0)) { 'passed' } else { 'none' }
        }
        elseif ($rest -match '^ok\b') { 'passed' }
        else { 'none' }
    # a success state can only vouch for a passing step, and a failure state never for one
    if ($state -eq 'passed' -and $latest.state -ne 'success') { $state = 'failed' }
    [pscustomobject]@{ State = $state; Detail = "status '$Context' is $($latest.state): $($latest.description)"; Url = $url }
}

function Get-CheckResult {
    param($Map, $Check, $Ctx, [bool]$Required = $true)
    $id = $Check.id; $title = $Check.title
    $localIds = @(Get-MapProp $Check 'local' @())
    $human = Get-MapProp $Check 'human'
    $builtin = Get-MapProp $Check 'builtin'

    if ($human) { return New-CheckResult -Id $id -Title $title -Status 'human' -Detail $human -Required $Required -Kind 'human' }

    if ($builtin) {
        $b = $Ctx.Builtin[$builtin]
        if ($null -eq $b) { return New-CheckResult -Id $id -Title $title -Status 'not-run' -Detail "builtin '$builtin' did not run" -Required $Required -Kind 'builtin' }
        $st = if ($b.Ok) { 'passed' } else { 'failed' }
        return New-CheckResult -Id $id -Title $title -Status $st -Detail $b.Detail -Required $Required -Kind 'builtin'
    }

    $ciStatus = 'none'; $ciDetail = ''; $url = ''; $lab = $false; $remedy = @()
    $ciEntries = @(Get-MapProp $Check 'ci' @())
    if ($ciEntries.Count -gt 0) {
        $c = Resolve-CiEntries -Map $Map -Entries $ciEntries -Ctx $Ctx
        $ciStatus = $c.State
        $lab = [bool](@($c.Alts | Where-Object { $_.Lab }).Count -gt 0)
        $bits = foreach ($a in $c.Alts) { "$($a.Workflow)#$($a.Job) [$(Get-TriggerText $a.Trigger)]: $($a.Status.State) ($($a.Status.Detail))" }
        $ciDetail = $bits -join ' | '
        $url = (@($c.Alts | ForEach-Object { $_.Status.Url } | Where-Object { $_ }) | Select-Object -First 1)
        if ($ciStatus -in 'none', 'skipped', 'cancelled', 'pending') {
            $first = $c.Alts | Select-Object -First 1
            $remedy = Get-CiRemedy -Map $Map -Workflow $first.Workflow -Job $first.Job -Trigger $first.Trigger -Lab $lab -Branch $Ctx.Branch -Pr $Ctx.PrNumber -RepoFlag $Ctx.RepoFlag -Local $localIds
        }
    }

    $statusEntries = @(Get-MapProp $Check 'status' @())
    $statusState = 'none'; $statusDetail = ''
    if ($statusEntries.Count -gt 0) {
        $sr = @($statusEntries | ForEach-Object { Get-StatusStepResult -Statuses @($Ctx.Statuses) -Context $_.context -Step $_.step })
        $statusState = Merge-CheckStates @($sr | ForEach-Object { $_.State -replace '^none$', 'skipped' })
        if ($statusState -eq 'skipped') { $statusState = 'none' }
        $statusDetail = ($sr | ForEach-Object { $_.Detail }) -join ' | '
        if (-not $url) { $url = (@($sr | ForEach-Object { $_.Url } | Where-Object { $_ }) | Select-Object -First 1) }
    }

    $localStatus = 'none'; $localDetail = ''
    if ($localIds.Count -gt 0 -and $Ctx.Local.Count -gt 0) {
        $res = @($localIds | ForEach-Object { $Ctx.Local[$_] })
        if (@($res | Where-Object { $_ -ne $null }).Count -gt 0) {
            $bad = @($localIds | Where-Object { $Ctx.Local[$_] -and -not $Ctx.Local[$_].Ok })
            $missing = @($localIds | Where-Object { -not $Ctx.Local[$_] })
            if ($bad.Count -gt 0) { $localStatus = 'failed' }
            elseif ($missing.Count -eq 0) { $localStatus = 'passed' }
            $localDetail = "local: " + (($localIds | ForEach-Object { if ($Ctx.Local[$_]) { "$_=$(if ($Ctx.Local[$_].Ok) { 'ok' } else { 'FAILED' }) ($($Ctx.Local[$_].Evidence))" } else { "$_=not run" } }) -join ', ')
        }
    }

    $status =
        if ($ciStatus -eq 'failed' -or $localStatus -eq 'failed' -or $statusState -eq 'failed') { 'failed' }
        elseif ($ciStatus -eq 'passed' -or $localStatus -eq 'passed' -or $statusState -eq 'passed') { 'passed' }
        elseif ($ciStatus -eq 'pending') { 'pending' }
        elseif ($ciStatus -eq 'cancelled') { 'not-run' }
        else { 'not-run' }
    $detail = (@($ciDetail, $statusDetail, $localDetail) | Where-Object { $_ }) -join ' || '
    if ($statusEntries.Count -gt 0 -and $status -eq 'not-run') {
        $steps = (@($statusEntries | ForEach-Object { $_.step }) | Select-Object -Unique) -join ', '
        $remedy = @($remedy) + "from a clean checkout of the PR head, on a Windows host: $($script:VerifyCmd) winhost <ID> --live --gui --status    # needs VHC_WINHOST; posts the 'verify-vhc/windows-host' status that covers: $steps"
    }
    if ($status -eq 'passed' -and $ciStatus -ne 'passed') { $remedy = @() }
    if ($status -eq 'passed' -or $status -eq 'failed') { $remedy = @() }
    $kind = if ($ciEntries.Count -gt 0) { 'ci' } elseif ($statusEntries.Count -gt 0 -and $localIds.Count -eq 0) { 'status' } else { 'local' }
    New-CheckResult -Id $id -Title $title -Status $status -Detail $detail -Remedy $remedy -Required $Required -Kind $kind -Url $url -Lab $lab
}

function Get-WorkflowSurfaceChecks {
    # Dynamic checks for a changed workflow file: each touched job must have run on the head SHA.
    param($Map, $Ctx, [string[]]$ChangedWorkflows)
    $out = @()
    foreach ($file in $ChangedWorkflows) {
        $name = Split-Path $file -Leaf
        $wf = Get-MapProp $Map.workflows $name
        $info = $Ctx.WorkflowChange[$name]
        if ($info -and $info.Removed) {
            $out += New-CheckResult -Id "$name" -Title "$name removed" -Status 'human' -Detail 'workflow deleted; review that nothing depended on it' -Kind 'human'
            continue
        }
        if (-not $wf) {
            $out += New-CheckResult -Id "$name" -Title "$name is not in the workflow registry" -Status 'human' -Detail "add $name to pr-surfaces.json `"workflows`" so its jobs and triggers are known" -Kind 'human'
            continue
        }
        $jobIds = if ($info) { @($info.TouchedJobs) } else { @($wf.jobs.PSObject.Properties.Name) }
        foreach ($jid in $jobIds) {
            $meta = Get-MapProp $wf.jobs $jid
            if (-not $meta) {
                $out += New-CheckResult -Id "$name#$jid" -Title "$name job '$jid'" -Status 'human' -Detail "job '$jid' is not in the workflow registry; add it to pr-surfaces.json" -Kind 'human'
                continue
            }
            $t = $meta.trigger
            $title = "$name job '$jid' ($(Get-TriggerText $t))"
            if ($t -in 'publish-only', 'scheduled') {
                $out += New-CheckResult -Id "$name#$jid" -Title $title -Status 'human' -Detail "cannot be exercised from a PR ($t). Review the diff; a publish path is proven only by publishing." -Kind 'human'
                continue
            }
            if (($t -eq 'pr-fork-only' -and -not $Ctx.IsFork) -or ($t -eq 'pr-same-repo-only' -and $Ctx.IsFork)) {
                $out += New-CheckResult -Id "$name#$jid" -Title $title -Status 'human' -Detail "job only runs for $(if ($t -eq 'pr-fork-only') { 'fork' } else { 'same-repo' }) PRs; this PR is $(if ($Ctx.IsFork) { 'a fork' } else { 'same-repo' }) so it cannot run here" -Kind 'human'
                continue
            }
            $chk = [pscustomobject]@{ id = "$name#$jid"; title = $title; ci = @([pscustomobject]@{ workflow = $name; job = $jid }) }
            $out += Get-CheckResult -Map $Map -Check $chk -Ctx $Ctx
        }
    }
    $out
}

function Get-SurfaceVerdict {
    param([object[]]$Checks)
    $req = @($Checks | Where-Object { $_.Required })
    if (@($req | Where-Object { $_.Status -eq 'failed' }).Count) { return 'FAILED' }
    if (@($req | Where-Object { $_.Status -in 'not-run', 'pending' }).Count) { return 'NEEDS-RUN' }
    if (@($req | Where-Object { $_.Status -eq 'human' }).Count) { return 'NEEDS-HUMAN' }
    'VERIFIED'
}

function Get-OverallVerdict {
    param([string[]]$SurfaceVerdicts, [bool]$HasUnmapped, [bool]$OtherFailed, [bool]$HygieneFailed, [bool]$OtherPending = $false)
    $v = @($SurfaceVerdicts)
    if ($OtherFailed -or $HygieneFailed -or $v -contains 'FAILED') { return 'FAILED' }
    if ($OtherPending -or $v -contains 'NEEDS-RUN') { return 'NEEDS-RUN' }
    if ($HasUnmapped -or $v -contains 'NEEDS-HUMAN') { return 'NEEDS-HUMAN' }
    'VERIFIED'
}


# ---------------------------------------------------------------------------
# Map integrity (the "map can't rot silently" test calls this)
# ---------------------------------------------------------------------------

function Test-PrSurfaceMap {
    # -> list of problem strings; empty means the map is consistent with the tree and the workflows.
    param([Parameter(Mandatory)]$Map, [Parameter(Mandatory)][string]$RepoRoot, [Parameter(Mandatory)][string[]]$TrackedFiles)
    $p = [System.Collections.Generic.List[string]]::new()
    $wfDir = Join-Path $RepoRoot '.github/workflows'

    # 1. every tracked file lands on a surface
    $res = Resolve-FileSurfaces -Map $Map -Files $TrackedFiles
    foreach ($u in $res.Unmapped) { $p.Add("tracked file matches no surface glob: $u") }

    # 2. every glob matches something (a dead glob is a rename the map did not follow)
    foreach ($sp in $Map.surfaces.PSObject.Properties) {
        foreach ($g in @($sp.Value.paths)) {
            if (-not ($TrackedFiles | Where-Object { Test-GlobMatch -Path $_ -Globs @($g) } | Select-Object -First 1)) { $p.Add("surface '$($sp.Name)': glob '$g' matches no tracked file") }
        }
        if (@($sp.Value.paths).Count -eq 0) { $p.Add("surface '$($sp.Name)' has no paths") }
        $ids = @()
        foreach ($c in @(Get-MapProp $sp.Value 'checks' @())) {
            $where = "surface '$($sp.Name)' check '$($c.id)'"
            if (-not (Get-MapProp $c 'id')) { $p.Add("surface '$($sp.Name)': a check has no id"); continue }
            if ($ids -contains $c.id) { $p.Add("$where is duplicated") }
            $ids += $c.id
            if (-not (Get-MapProp $c 'title')) { $p.Add("$where has no title") }
            $has = @('ci', 'local', 'builtin', 'human', 'status') | Where-Object { Get-MapProp $c $_ }
            if (@($has).Count -eq 0) { $p.Add("$where has none of ci/local/builtin/human/status") }
            if ((Get-MapProp $c 'builtin') -and (Get-MapProp $c 'builtin') -notin $script:KnownBuiltin) { $p.Add("$where names unknown builtin '$($c.builtin)'") }
            foreach ($st in @(Get-MapProp $c 'status' @())) {
                if (-not (Get-MapProp $st 'context')) { $p.Add("$where has a status entry with no context") }
                if ((Get-MapProp $st 'step') -notin $script:KnownStatusStep) { $p.Add("$where names unknown status step '$((Get-MapProp $st 'step'))'") }
            }
            foreach ($l in @(Get-MapProp $c 'local' @())) { if ($l -notin $script:KnownLocal) { $p.Add("$where names unknown local check '$l'") } }
            foreach ($e in @(Get-MapProp $c 'ci' @())) {
                $wf = Get-MapProp $Map.workflows $e.workflow
                if (-not $wf) { $p.Add("$where references workflow '$($e.workflow)' that is not in the registry"); continue }
                if (-not (Get-MapProp $wf.jobs $e.job)) { $p.Add("$where references job '$($e.job)' that is not registered for $($e.workflow)") }
            }
        }
    }

    # 3. the workflow registry matches .github/workflows both ways
    $onDisk = @(Get-ChildItem -LiteralPath $wfDir -File | Where-Object { $_.Name -match '\.ya?ml$' } | ForEach-Object { $_.Name })
    foreach ($f in $onDisk) { if (-not (Get-MapProp $Map.workflows $f)) { $p.Add("workflow file '$f' is not in the registry") } }
    foreach ($name in $Map.workflows.PSObject.Properties.Name) {
        $path = Join-Path $wfDir $name
        if (-not (Test-Path -LiteralPath $path)) { $p.Add("registry names workflow '$name' that does not exist in .github/workflows"); continue }
        $jobs = @(Get-WorkflowJobs -Text (Get-Content -LiteralPath $path -Raw))
        $reg = $Map.workflows.$name
        foreach ($j in $jobs) { if (-not (Get-MapProp $reg.jobs $j.Id)) { $p.Add("$name job '$($j.Id)' is not in the registry") } }
        foreach ($rj in $reg.jobs.PSObject.Properties) {
            if (-not ($jobs | Where-Object { $_.Id -eq $rj.Name })) { $p.Add("registry names job '$($rj.Name)' that does not exist in $name") }
            if ($rj.Value.trigger -notin $script:KnownTrigger) { $p.Add("$name job '$($rj.Name)' has unknown trigger '$($rj.Value.trigger)'") }
        }
        if ($null -eq (Get-MapProp $reg 'dispatch')) { $p.Add("registry entry '$name' must say whether it has workflow_dispatch") }
        else {
            $hasDispatch = (Get-Content -LiteralPath $path -Raw) -match '(?m)^  workflow_dispatch:'
            if ([bool]$reg.dispatch -ne [bool]$hasDispatch) { $p.Add("registry says $name dispatch=$($reg.dispatch) but the file says workflow_dispatch=$hasDispatch") }
        }
    }
    if (-not ($Map.surfaces.PSObject.Properties | Where-Object { (Get-MapProp $_.Value 'kind') -eq 'workflow' })) { $p.Add('no surface has kind "workflow", so workflow edits are not checked') }
    $p.ToArray()
}

# ---------------------------------------------------------------------------
# Dispatch safety gate
# ---------------------------------------------------------------------------

function Test-DispatchAllowed {
    # Non-negotiable gates for --dispatch. -> @{ Allowed; Reason }
    param($Map, [string]$Workflow, [bool]$IsFork, [string]$HeadBranch, [string]$HeadSha, [string]$BranchTipSha, $ExistingRuns)
    $deny = { param($r) @{ Allowed = $false; Reason = $r } }
    if ($Workflow -match '(?i)release|publish|deploy|sbom') { return & $deny "refusing to dispatch '$Workflow': publish-class workflow" }
    $wf = Get-MapProp $Map.workflows $Workflow
    if (-not $wf -or -not (Get-MapProp $wf 'autoDispatch' $false)) { return & $deny "'$Workflow' is not marked autoDispatch in pr-surfaces.json" }
    if ($IsFork) { return & $deny 'head is a fork; never dispatch a fork head' }
    if ($HeadBranch -in 'master', 'main') { return & $deny "head branch is '$HeadBranch'; never dispatch on master" }
    if ([string]::IsNullOrWhiteSpace($HeadBranch)) { return & $deny 'no head branch' }
    if ($BranchTipSha -ne $HeadSha) { return & $deny "branch '$HeadBranch' tip ($BranchTipSha) is not the PR head SHA ($HeadSha); a dispatch would test different code" }
    $prior = @($ExistingRuns | Where-Object { $_.path -eq ".github/workflows/$Workflow" -and $_.head_sha -eq $HeadSha })
    if ($prior.Count -gt 0) { return & $deny "a run of $Workflow already exists on $HeadSha (run $($prior[0].id), $($prior[0].event)); at most one per head SHA" }
    @{ Allowed = $true; Reason = 'ok' }
}

# ---------------------------------------------------------------------------
# Builtin static checks
# ---------------------------------------------------------------------------

function Get-MarkdownProblems {
    param([string]$Text, [string]$Path, [string[]]$TreePaths)
    $problems = [System.Collections.Generic.List[string]]::new()
    $lines = $Text -split "`r?`n"
    $fences = 0; $inFence = $false; $fenceMark = ''
    $treeSet = [System.Collections.Generic.HashSet[string]]::new([string[]]@($TreePaths))
    $dir = (Split-Path $Path -Parent) -replace '\\', '/'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\s{0,3}(`{3,}|~{3,})') {
            $mark = $Matches[1][0]
            if (-not $inFence) { $inFence = $true; $fenceMark = $mark; $fences++ }
            elseif ($mark -eq $fenceMark) { $inFence = $false }
            continue
        }
        if ($inFence) { continue }
        if ($l -match '^(<<<<<<<|>>>>>>>) ') { $problems.Add("merge-conflict marker") ; continue }
        $targets = @()
        foreach ($m in [regex]::Matches($l, '!?\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)')) { $targets += $m.Groups[1].Value }
        if ($l -match '^\s{0,3}\[[^\]]+\]:\s*(\S+)') { $targets += $Matches[1] }
        foreach ($t in $targets) {
            $t = $t.Trim('<', '>')
            if ($t -match '^(https?:|mailto:|tel:|ftp:|data:|#)' -or $t -match '^[a-z][a-z0-9+.-]*:') { continue }
            $p = ($t -replace '[#?].*$', '')
            if ([string]::IsNullOrWhiteSpace($p)) { continue }
            $p = [uri]::UnescapeDataString($p)
            $resolved = if ($p.StartsWith('/')) { $p.TrimStart('/') } else { ($dir ? "$dir/$p" : $p) }
            # normalise ./ and ../
            $stack = [System.Collections.Generic.List[string]]::new()
            foreach ($seg in ($resolved -split '/')) {
                if ($seg -eq '' -or $seg -eq '.') { continue }
                if ($seg -eq '..') { if ($stack.Count) { $stack.RemoveAt($stack.Count - 1) } ; continue }
                $stack.Add($seg)
            }
            $norm = $stack -join '/'
            $isDir = $TreePaths | Where-Object { $_.StartsWith("$norm/") } | Select-Object -First 1
            if (-not $treeSet.Contains($norm) -and -not $isDir) { $problems.Add("broken relative link '$t'") }
        }
    }
    if ($inFence) { $problems.Add('unclosed code fence') }
    $problems.ToArray() | Select-Object -Unique
}

function Invoke-DocsSanity {
    param($ChangedFiles, [string]$HeadSha, [string]$BaseSha)
    $md = @($ChangedFiles | Where-Object { $_.filename -match '\.md$' -and $_.status -ne 'removed' })
    if ($md.Count -eq 0) { return @{ Ok = $true; Detail = 'no markdown files changed' } }
    $headTree = Get-TreePaths -Ref $HeadSha
    $new = @()
    foreach ($f in $md) {
        $t = Get-FileAtRef -Path $f.filename -Ref $HeadSha
        if ($null -eq $t) { $new += "$($f.filename): could not read at head"; continue }
        $hp = @(Get-MarkdownProblems -Text $t -Path $f.filename -TreePaths $headTree)
        $bp = @()
        $basePath = if ($f.previous_filename) { $f.previous_filename } else { $f.filename }
        $bt = Get-FileAtRef -Path $basePath -Ref $BaseSha
        if ($null -ne $bt) { $bp = @(Get-MarkdownProblems -Text $bt -Path $basePath -TreePaths (Get-TreePaths -Ref $BaseSha)) }
        foreach ($p in $hp) { if ($bp -notcontains $p) { $new += "$($f.filename): $p" } }
    }
    if ($new.Count) { @{ Ok = $false; Detail = "new problems: $($new -join '; ')" } }
    else { @{ Ok = $true; Detail = "$($md.Count) markdown file(s) checked, no new problems" } }
}

function Get-ChangelogProblems {
    param([string]$Text)
    $p = [System.Collections.Generic.List[string]]::new()
    $lines = $Text -split "`r?`n"
    $versions = @(); $defs = @{}; $section = $null; $older = $false
    $allowed = 'Added', 'Changed', 'Deprecated', 'Removed', 'Fixed', 'Security'
    if (-not ($lines -match '^## \[Unreleased\]\s*$')) { $p.Add('missing "## [Unreleased]" section') }
    foreach ($l in $lines) {
        if ($l -match '^## Older releases') { $older = $true }
        if ($l -match '^## \[([^\]]+)\]') { $section = $Matches[1]; if ($section -ne 'Unreleased') { $versions += $section }; continue }
        if ($l -match '^## ') { $section = $null; continue }
        if (-not $older -and $section -and $l -match '^### (.+?)\s*$' -and $Matches[1] -notin $allowed) { $p.Add("unknown change group '### $($Matches[1])' under [$section]") }
        if ($l -match '^\[([^\]]+)\]:\s*\S+') { $defs[$Matches[1]] = $true }
    }
    foreach ($v in ($versions | Group-Object | Where-Object Count -gt 1)) { $p.Add("duplicate version section [$($v.Name)]") }
    foreach ($v in ($versions | Select-Object -Unique)) { if (-not $defs.ContainsKey($v)) { $p.Add("no link definition for [$v]") } }
    if (-not $defs.ContainsKey('Unreleased') -and ($lines -match '^## \[Unreleased\]')) { $p.Add('no link definition for [Unreleased]') }
    $p.ToArray()
}

function Invoke-ChangelogFormat {
    param($ChangedFiles, [string]$HeadSha, [string]$BaseSha)
    $f = $ChangedFiles | Where-Object { $_.filename -eq 'ChangeLog.md' } | Select-Object -First 1
    if (-not $f) { return @{ Ok = $true; Detail = 'ChangeLog.md not changed' } }
    $h = Get-FileAtRef -Path 'ChangeLog.md' -Ref $HeadSha
    if ($null -eq $h) { return @{ Ok = $false; Detail = 'could not read ChangeLog.md at head' } }
    $b = Get-FileAtRef -Path 'ChangeLog.md' -Ref $BaseSha
    $hp = @(Get-ChangelogProblems -Text $h)
    $bp = if ($null -ne $b) { @(Get-ChangelogProblems -Text $b) } else { @() }
    $new = @($hp | Where-Object { $bp -notcontains $_ })
    $fixed = @($bp | Where-Object { $hp -notcontains $_ })
    if ($new.Count) { return @{ Ok = $false; Detail = "new problems: $($new -join '; ')" } }
    $d = 'structure intact'
    if ($fixed.Count) { $d += "; fixes $($fixed.Count) existing problem(s): $($fixed -join '; ')" }
    @{ Ok = $true; Detail = $d }
}

function Invoke-ShellSyntax {
    param($ChangedFiles, [string]$HeadSha)
    $sh = @($ChangedFiles | Where-Object { $_.status -ne 'removed' -and ($_.filename -match '\.sh$' -or $_.filename -match '^\.githooks/') })
    if ($sh.Count -eq 0) { return @{ Ok = $true; Detail = 'no shell scripts changed' } }
    $bad = @()
    foreach ($f in $sh) {
        $t = Get-FileAtRef -Path $f.filename -Ref $HeadSha
        if ($null -eq $t) { $bad += "$($f.filename): unreadable"; continue }
        if ($t -match '^#!.*\b(pwsh|python|node|bun)\b') { continue }
        $tmp = New-TemporaryFile
        try {
            Set-Content -LiteralPath $tmp -Value $t -NoNewline -Encoding utf8NoBOM
            $o = bash -n $tmp.FullName 2>&1
            if ($LASTEXITCODE -ne 0) { $bad += "$($f.filename): $($o -join ' ')" }
        } finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }
    if ($bad.Count) { @{ Ok = $false; Detail = "bash -n failed: $($bad -join '; ')" } } else { @{ Ok = $true; Detail = "bash -n ok on $($sh.Count) file(s)" } }
}

function Invoke-JsonSyntax {
    param($ChangedFiles, [string]$HeadSha)
    $js = @($ChangedFiles | Where-Object { $_.status -ne 'removed' -and $_.filename -match '\.json$' })
    if ($js.Count -eq 0) { return @{ Ok = $true; Detail = 'no .json files changed' } }
    $bad = @()
    foreach ($f in $js) {
        $t = Get-FileAtRef -Path $f.filename -Ref $HeadSha
        if ($null -eq $t) { $bad += "$($f.filename): unreadable"; continue }
        try { $null = $t | ConvertFrom-Json -Depth 64 } catch { $bad += "$($f.filename): $($_.Exception.Message)" }
    }
    if ($bad.Count) { @{ Ok = $false; Detail = "does not parse: $($bad -join '; ')" } } else { @{ Ok = $true; Detail = "$($js.Count) json file(s) parse" } }
}

# ---------------------------------------------------------------------------
# Hygiene
# ---------------------------------------------------------------------------

function New-Hygiene {
    param([string]$Id, [ValidateSet('pass', 'warn', 'fail', 'info')][string]$Level, [string]$Message)
    [pscustomobject]@{ Id = $Id; Level = $Level; Message = $Message }
}

function Get-CommitType {
    param([string]$Subject)
    if ($Subject -match '^(?<t>[A-Za-z]+)(\([^)]*\))?(?<bang>!)?:\s') { return [pscustomobject]@{ Type = $Matches.t.ToLowerInvariant(); Breaking = [bool]$Matches.bang } }
    $null
}

function Get-Hygiene {
    param($Map, $Pr, $Files, $Commits, [string]$HeadSha, [string]$BaseSha, [string]$WorkflowDir, [string]$ConventionalModule)
    $out = @()
    $names = @($Files | ForEach-Object { $_.filename })
    $hyg = $Map.hygiene
    $product = @($names | Where-Object { (Test-GlobMatch -Path $_ -Globs @($hyg.productCode)) -and -not (Test-GlobMatch -Path $_ -Globs @($hyg.productExclude)) })
    $toolingOnly = ($product.Count -eq 0)

    # Conventional Commits title (types decide the release version: repo CLAUDE.md, Versioning)
    $title = [string]$Pr.title
    $lintProblems = @()
    if ($ConventionalModule -and (Test-Path -LiteralPath $ConventionalModule)) {
        Import-Module $ConventionalModule -Force -DisableNameChecking
        $lintProblems = @(Test-VhcCommitMessage -Subject $title)
    }
    $t = Get-CommitType $title
    if ($lintProblems.Count) { $out += New-Hygiene 'conventional-title' 'warn' "PR title is not a Conventional Commit; it counts as an untyped patch. $($lintProblems -join ' ')" }
    else { $out += New-Hygiene 'conventional-title' 'pass' "PR title type '$($t.Type)$(if ($t.Breaking) { '!' })'" }

    $commitTypes = @($Commits | ForEach-Object { Get-CommitType ($_.commit.message -split "`n")[0] } | Where-Object { $_ })
    $bumpers = @(@($t) + $commitTypes | Where-Object { $_ -and ($_.Type -eq 'feat' -or $_.Breaking) })
    $breakingFooter = @($Commits | Where-Object { $_.commit.message -match '(?m)^BREAKING[ -]CHANGE:' }).Count -gt 0
    if ($toolingOnly -and ($bumpers.Count -gt 0 -or $breakingFooter)) {
        $out += New-Hygiene 'version-bump-type' 'fail' "tooling-only diff (no product code under vHC/HC_Reporting) but the title/commits carry feat / '!' / BREAKING CHANGE, which would bump the product minor or major version. Retype as ci/test/docs/chore."
    } elseif ($bumpers.Count -gt 0 -or $breakingFooter) {
        $out += New-Hygiene 'version-bump-type' 'info' 'feat / breaking marker present on a diff that touches product code: expect a minor/major release bump.'
    } else {
        $out += New-Hygiene 'version-bump-type' 'pass' 'no minor/major marker, or none needed'
    }

    # Fixes #N
    $blob = (@($title, [string]$Pr.body) + @($Commits | ForEach-Object { $_.commit.message })) -join "`n"
    $closing = [regex]::Matches($blob, '(?i)\b(?:fix(?:es|ed)?|close[sd]?|resolve[sd]?)\b:?\s+(?:[\w.-]+/[\w.-]+)?#(\d+)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
    $subjects = @($title) + @($Commits | ForEach-Object { ($_.commit.message -split "`n")[0] })
    $shorthand = @($subjects | ForEach-Object { [regex]::Matches($_, '\(#(\d+)\)') | ForEach-Object { $_.Groups[1].Value } } | Select-Object -Unique | Where-Object { $_ -ne [string]$Pr.number -and $closing -notcontains $_ })
    $loose = @([regex]::Matches($blob, '(?i)\b(?:issue|for|addresses|re)\s+#(\d+)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique | Where-Object { $_ -ne [string]$Pr.number -and $closing -notcontains $_ })
    $unlinked = @($shorthand + $loose | Select-Object -Unique)
    if ($unlinked.Count) { $out += New-Hygiene 'fixes-keyword' 'warn' "references #$($unlinked -join ', #') without an auto-close keyword. '(#N)' does not close an issue; if this fixes it, write 'Fixes #N' in the PR body or a commit (repo CLAUDE.md, Commit / PR Convention)." }
    elseif ($closing.Count) { $out += New-Hygiene 'fixes-keyword' 'pass' "closes #$($closing -join ', #') with a keyword" }
    else { $out += New-Hygiene 'fixes-keyword' 'pass' 'no issue referenced as fixed' }

    # ChangeLog [Unreleased]
    $exempt = @($hyg.changelogExemptTypes)
    $typeNames = @(@($t) + $commitTypes | Where-Object { $_ } | ForEach-Object { $_.Type } | Select-Object -Unique)
    $userVisibleType = ($typeNames.Count -eq 0) -or (@($typeNames | Where-Object { $_ -notin $exempt }).Count -gt 0)
    if ($product.Count -eq 0) { $out += New-Hygiene 'changelog' 'pass' 'no product code changed; no ChangeLog entry needed' }
    elseif (-not $userVisibleType) { $out += New-Hygiene 'changelog' 'pass' "product files changed but every commit type is exempt ($($typeNames -join ', '))" }
    else {
        $added = Get-ChangelogUnreleasedAdditions -Head (Get-FileAtRef -Path 'ChangeLog.md' -Ref $HeadSha) -Base (Get-FileAtRef -Path 'ChangeLog.md' -Ref $BaseSha)
        if ($added.Count -gt 0) { $out += New-Hygiene 'changelog' 'pass' "$($added.Count) line(s) added under [Unreleased]" }
        else { $out += New-Hygiene 'changelog' 'warn' "user-visible product code changed ($($product.Count) file(s), type $($typeNames -join '/')) but no line was added under ## [Unreleased] in ChangeLog.md (repo CLAUDE.md, Changelog)." }
    }
    $gen = @($names | Where-Object { Test-GlobMatch -Path $_ -Globs @($hyg.generatedFiles) })
    if ($gen.Count) { $out += New-Hygiene 'generated-file' 'warn' "hand-edited generated file(s): $($gen -join ', '). CI regenerates them; edit ChangeLog.md only." }

    # csproj version hand-bump
    $bumped = @()
    foreach ($f in @($Files | Where-Object { $_.filename -match '\.csproj$' -and $_.patch })) {
        foreach ($l in ($f.patch -split "`n")) {
            if ($l -match '^[+-]\s*<(Version|VersionPrefix|AssemblyVersion|FileVersion|InformationalVersion|PackageVersion)>') { $bumped += $f.filename; break }
        }
    }
    if ($bumped.Count) { $out += New-Hygiene 'csproj-version' 'fail' "version element changed by hand in $($bumped -join ', '). Versions are computed from commits (ADR 0031); revert the auto-bump (git checkout -- the csproj) or raise the floor deliberately." }
    else { $out += New-Hygiene 'csproj-version' 'pass' 'no csproj version element changed' }

    # Actions pinned to SHAs
    $wfFiles = @($Files | Where-Object { $_.filename -match '^\.github/workflows/.+\.ya?ml$' -and $_.patch })
    if ($wfFiles.Count) {
        $unpinned = @()
        foreach ($f in $wfFiles) {
            foreach ($l in ($f.patch -split "`n")) {
                if ($l -notmatch '^\+(?!\+\+)') { continue }
                if ($l -match '^\+\s*-?\s*uses:\s*(?<ref>[^\s#]+)') {
                    $ref = $Matches.ref
                    if ($ref.StartsWith('./') -or $ref.StartsWith('docker://')) { continue }
                    if ($ref -notmatch '@[0-9a-f]{40}$') { $unpinned += "$($f.filename): $ref" }
                }
            }
        }
        $ratio = Get-PinRatio -WorkflowDir $WorkflowDir
        $level = if ($ratio -ge 0.8) { 'fail' } else { 'warn' }
        if ($unpinned.Count) { $out += New-Hygiene 'actions-pinned' $level "added action reference(s) not pinned to a 40-char SHA ($([int]($ratio*100))% of this repo's existing refs are pinned): $($unpinned -join '; ')" }
        else { $out += New-Hygiene 'actions-pinned' 'pass' 'added action references are SHA-pinned' }
    }
    [pscustomobject]@{ Items = $out; ProductFiles = $product; ToolingOnly = $toolingOnly }
}

function Get-PinRatio {
    param([string]$WorkflowDir)
    if (-not $WorkflowDir -or -not (Test-Path -LiteralPath $WorkflowDir)) { return 0.0 }
    $refs = Get-ChildItem -LiteralPath $WorkflowDir -Include *.yml, *.yaml -File -Recurse | ForEach-Object { Select-String -Path $_.FullName -Pattern '^\s*-?\s*uses:\s*(\S+)' } |
        ForEach-Object { $_.Matches[0].Groups[1].Value } | Where-Object { -not $_.StartsWith('./') -and -not $_.StartsWith('docker://') }
    $refs = @($refs)
    if ($refs.Count -eq 0) { return 0.0 }
    [double]@($refs | Where-Object { $_ -match '@[0-9a-f]{40}' }).Count / $refs.Count
}

function Get-ChangelogUnreleasedAdditions {
    param([AllowNull()][string]$Head, [AllowNull()][string]$Base)
    $section = {
        param($t)
        if ($null -eq $t) { return @() }
        $in = $false; $r = @()
        foreach ($l in ($t -split "`r?`n")) {
            if ($l -match '^## \[Unreleased\]') { $in = $true; continue }
            if ($in -and $l -match '^## ') { break }
            if ($in -and $l -match '^\s*[-*]\s+\S') { $r += $l.Trim() }
        }
        , $r
    }
    $h = & $section $Head; $b = & $section $Base
    @($h | Where-Object { $b -notcontains $_ })
}

# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------

function Get-VerdictBadge {
    param([string]$V)
    switch ($V) { 'VERIFIED' { 'VERIFIED' } 'NEEDS-RUN' { 'NEEDS-RUN' } 'NEEDS-HUMAN' { 'NEEDS-HUMAN' } 'FAILED' { 'FAILED' } default { $V } }
}

function Format-PrReportMarkdown {
    param($R)
    $sb = [System.Text.StringBuilder]::new()
    $short = $R.pr.headSha.Substring(0, 7)
    $gen = $R.generatedAt
    if ($gen -is [datetime]) { $gen = $gen.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [cultureinfo]::InvariantCulture) }
    [void]$sb.AppendLine("# PR verification: #$($R.pr.number) - $($R.pr.title)")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("**Verdict: $($R.verdict)**  |  head ``$short`` on ``$($R.pr.headBranch)`` -> ``$($R.pr.baseBranch)``  |  state: $($R.pr.state)$(if ($R.pr.draft) { ' (draft)' })  |  $($R.pr.sameRepo ? 'same-repo' : 'FORK')  |  $gen")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine($R.summary)
    [void]$sb.AppendLine()
    if ($R.pr.state -ne 'open') { [void]$sb.AppendLine("> This PR is $($R.pr.state); the report audits its last head SHA after the fact."); [void]$sb.AppendLine() }

    if (@($R.nextActions).Count) {
        [void]$sb.AppendLine('## What to do next')
        [void]$sb.AppendLine()
        foreach ($a in $R.nextActions) { [void]$sb.AppendLine("- $a") }
        [void]$sb.AppendLine()
    }

    [void]$sb.AppendLine('## Surfaces this PR touched')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('| Surface | Files | Verdict |')
    [void]$sb.AppendLine('|---|---|---|')
    foreach ($s in $R.surfaces) { [void]$sb.AppendLine("| ``$($s.id)`` $($s.title) | $($s.files.Count) | $($s.verdict) |") }
    if (@($R.unmapped).Count) { [void]$sb.AppendLine("| **unmapped** | $(@($R.unmapped).Count) | NEEDS-HUMAN |") }
    [void]$sb.AppendLine()

    [void]$sb.AppendLine('## Checks')
    foreach ($s in $R.surfaces) {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("### ``$($s.id)``: $($s.verdict)")
        $shown = @($s.files | Select-Object -First 6)
        $fileList = ($shown | ForEach-Object { '`' + $_ + '`' }) -join ', '
        $more = if ($s.files.Count -gt 6) { " and $($s.files.Count - 6) more" } else { '' }
        [void]$sb.AppendLine("Files: $fileList$more")
        [void]$sb.AppendLine()
        if (@($s.checks).Count -eq 0) { [void]$sb.AppendLine('- (no checks registered for this surface)') }
        foreach ($c in $s.checks) {
            $mark = switch ($c.status) { 'passed' { 'PASS' } 'failed' { 'FAIL' } 'pending' { 'PENDING' } 'not-run' { 'NOT EXERCISED' } 'human' { 'HUMAN' } default { $c.status.ToUpper() } }
            [void]$sb.AppendLine("- **$mark** $($c.title)$(if (-not $c.required) { ' (advisory)' })")
            if ($c.detail) { [void]$sb.AppendLine("  - evidence: $($c.detail)") }
            if ($c.url) { [void]$sb.AppendLine("  - run: $($c.url)") }
            foreach ($rem in @($c.remedy)) { [void]$sb.AppendLine("  - to exercise: ``$rem``") }
        }
    }
    if (@($R.unmapped).Count) {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine('### Unmapped files (NEEDS-HUMAN)')
        [void]$sb.AppendLine()
        [void]$sb.AppendLine('These match no glob in `pr-surfaces.json`. Add them to a surface so the next PR is checked automatically.')
        foreach ($u in $R.unmapped) { [void]$sb.AppendLine("- ``$u``") }
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine('## Hygiene')
    [void]$sb.AppendLine()
    foreach ($h in $R.hygiene) { [void]$sb.AppendLine("- **$($h.level.ToUpper())** ``$($h.id)``: $($h.message)") }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('_fail = turns the verdict to FAILED; warn = advisory; info = for the reviewer._')

    if (@($R.otherChecks).Count) {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine('## Other checks on this SHA (not tied to a touched surface)')
        [void]$sb.AppendLine()
        foreach ($o in $R.otherChecks) { [void]$sb.AppendLine("- $($o.state.ToUpper()) $($o.name)") }
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine('## Residue a human must still do')
    [void]$sb.AppendLine()
    if (@($R.residue).Count) { foreach ($x in $R.residue) { [void]$sb.AppendLine("- $x") } } else { [void]$sb.AppendLine('- none identified') }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine('## What this cannot prove')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('- It proves that checks mapped to the touched surfaces ran on this exact head SHA and passed; it does not read the diff for logic errors.')
    [void]$sb.AppendLine('- A surface is only as covered as `pr-surfaces.json` says. Collection on a real VBR/VB365 host, the Windows GUI window and PDF output stay with a human unless a live job covers them.')
    [void]$sb.AppendLine('- A check that never ran is reported as not exercised. It is never counted as passing.')
    $sb.ToString()
}

Export-ModuleMember -Function Initialize-PrVerify, Invoke-Gh, Invoke-GhJson, Get-GhList, Get-GhFixtureName, Get-FileAtRef, Get-TreePaths,
    ConvertTo-GlobRegex, Test-GlobMatch, Read-PrSurfaceMap, Get-MapProp, Resolve-FileSurfaces,
    Get-WorkflowJobs, Get-PatchTouchedLines, Get-TouchedJobIds, ConvertTo-CheckState, Merge-CheckStates, Get-JobStatus,
    New-CheckResult, Get-CheckResult, Get-WorkflowSurfaceChecks, Get-SurfaceVerdict, Get-OverallVerdict, Test-DispatchAllowed,
    Get-MarkdownProblems, Invoke-DocsSanity, Get-ChangelogProblems, Invoke-ChangelogFormat, Invoke-ShellSyntax, Invoke-JsonSyntax,
    Get-CommitType, Get-Hygiene, Get-ChangelogUnreleasedAdditions, Format-PrReportMarkdown,
    Test-PrSurfaceMap, Get-PinRatio, Get-PrVerifyKnownLocal, Get-PrVerifyKnownBuiltin, Get-PrVerifyKnownTrigger, Get-PrVerifyKnownStatusStep, Get-StatusStepResult, Get-PrVerifyMarker
