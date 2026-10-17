#Requires -Version 7
# Invoke-ScaDriftProposal.ps1
# The single SCA drift proposer. Runs once per CI run after the integration jobs: loads every
# server's sca-triage.json (see Invoke-SecurityComplianceDriftCheck.ps1), unions them, and
#   - always: writes the union, the proposed VbrConfig.json and a change list to -OutDir and the
#     job summary (these are uploaded as an artifact);
#   - unless -ReportOnly: proposes the fix. ONE draft PR from the fixed bot branch into the base
#     branch, edited text-only in VbrConfig.json SecurityComplianceRuleNames with the exact
#     console labels, labeled AI-assisted, plus ONE sca-drift issue that links it. A clean run
#     closes both.
# -ReportOnly makes no gh or git call at all. CI passes it on every ref except dev and master.
#
# Safety rules
#   - "Clean" needs data: if no triage artifact was found, nothing is opened or closed.
#   - Never merges. The PR is a draft; a human approves it.
#   - If the PR cannot be created (for example the repo setting "Allow GitHub Actions to create
#     and approve pull requests" is off), the exact changes go into the issue body instead.
#
# USAGE
#   ./Invoke-ScaDriftProposal.ps1 -TriageDir sca-triage -Repo org/repo -RunUrl <url> `
#     -ConfigPath vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VbrConfig.json -OutDir sca-proposal [-ReportOnly]
# REQUIREMENTS (non-report-only): gh authenticated via GH_TOKEN; contents, pull-requests and
# issues write permission; a git checkout with origin configured.
# EXIT CODES: always 0.

param(
    [Parameter(Mandatory)][string]$TriageDir,
    [Parameter(Mandatory)][string]$Repo,
    [Parameter(Mandatory)][string]$RunUrl,
    [Parameter(Mandatory)][string]$ConfigPath,
    [string]$OutDir = 'sca-proposal',
    [string]$BaseBranch = 'dev',
    [string]$BotBranch = 'bot/sca-labels',
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ScaTriage.psm1') -Force

function Write-Summary {
    param([string]$Text)
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $Text -Encoding UTF8 }
}

function Invoke-Native {
    # Runs git/gh, returns stdout; sets $script:NativeOk from the exit code. Output never echoes secrets.
    param([string]$Tool, [string[]]$Arguments)
    $global:LASTEXITCODE = 0
    $out = & $Tool @Arguments 2>&1
    $script:NativeOk = ($LASTEXITCODE -eq 0)
    return $out
}

try {
    $files = @(Get-ChildItem -Path $TriageDir -Filter 'sca-triage*.json' -Recurse -File -ErrorAction SilentlyContinue)
    $triage = @($files | ForEach-Object { Get-Content -Path $_.FullName -Raw | ConvertFrom-Json })
    if ($triage.Count -eq 0) {
        Write-Host '::warning::No sca-triage.json artifacts found - nothing proposed, nothing closed'
        Write-Summary "## SCA Drift Proposal`n`n> No triage artifacts were produced (integration jobs skipped or failed before the check). No issue or PR touched."
        exit 0
    }

    $m = Merge-ScaTriage -Triage $triage
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
    $m | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $OutDir 'sca-merged-triage.json') -Encoding UTF8

    $summary = [System.Text.StringBuilder]::new()
    [void]$summary.AppendLine('## SCA Drift Proposal')
    [void]$summary.AppendLine('')
    [void]$summary.AppendLine("Servers: $($m.servers -join ', '); highest VBR $(if ($m.highestVersion) { $m.highestVersion } else { 'unknown (major only)' }); $($m.dormant.Count) dormant enum(s) across all servers (informational only).")
    foreach ($r in $m.restUnavailable) { [void]$summary.AppendLine(''); [void]$summary.AppendLine("> Notice: $($r.server) ran CSV-only. $($r.reason)") }

    $mode = if ($ReportOnly) { 'report-only' } else { 'propose' }
    Write-Host "SCA proposal ($mode): $($m.drift.Count) drifting evaluated rule(s), $($m.patchable.Count) patchable, $($m.needsLabel.Count) need a console label, $($m.conflicts.Count) conflict(s)"

    $configText = Get-Content -Path $ConfigPath -Raw
    $newText = $null
    $changeLines = @()
    $bump = $null
    if ($m.patchable.Count -gt 0) {
        $labels = [ordered]@{}
        foreach ($p in $m.patchable) { $labels[$p.enum] = $p.proposedLabel }
        $cur = ConvertTo-ScaVersion $m.validatedFor
        $hi = ConvertTo-ScaVersion $m.highestVersion
        if ($hi -and ($null -eq $cur -or $hi -gt $cur)) { $bump = $m.highestVersion }
        if ($labels.Count -gt 0) {
            $newText = Update-VbrConfigLabels -Text $configText -Labels $labels -ValidatedFor $bump
            $changeLines = @(Get-ScaChangeLines -Patchable $m.patchable)
            if ($bump) { $changeLines += "bumped SecurityComplianceRulesValidatedForVbrVersion: $($m.validatedFor) -> $bump" }
        }
    }

    # Human text shared by the summary, the PR and the issue.
    $detail = [System.Text.StringBuilder]::new()
    if ($changeLines.Count -gt 0) {
        [void]$detail.AppendLine('### Proposed VbrConfig.json changes (exact console labels)')
        [void]$detail.AppendLine('')
        foreach ($l in $changeLines) { [void]$detail.AppendLine("- $l") }
        [void]$detail.AppendLine('')
    }
    if ($m.needsLabel.Count -gt 0) {
        [void]$detail.AppendLine('### Drift with no console label available')
        [void]$detail.AppendLine('')
        [void]$detail.AppendLine('These evaluated rules have no map entry, and no server could supply the console label over REST. Run `Check-SecurityComplianceDrift-REST.ps1` against a lab and add the label by hand.')
        [void]$detail.AppendLine('')
        foreach ($d in $m.needsLabel) { [void]$detail.AppendLine("- ``$($d.enum)`` ($($d.verdict); seen on $($d.servers -join ', '))") }
        [void]$detail.AppendLine('')
    }
    if ($m.conflicts.Count -gt 0) {
        [void]$detail.AppendLine('### Conflicting console labels (not patched; needs a human decision)')
        [void]$detail.AppendLine('')
        foreach ($d in $m.conflicts) { [void]$detail.AppendLine("- ``$($d.enum)``: $($d.conflict)") }
        [void]$detail.AppendLine('')
    }

    if ($null -ne $newText) {
        Set-Content -Path (Join-Path $OutDir 'VbrConfig.proposed.json') -Value $newText -Encoding UTF8 -NoNewline
    }
    Set-Content -Path (Join-Path $OutDir 'sca-proposed-changes.md') -Value $detail.ToString() -Encoding UTF8

    if ($m.drift.Count -eq 0) {
        [void]$summary.AppendLine('')
        [void]$summary.AppendLine('> No drift: every rule the servers evaluated has a matching label.')
    }
    else {
        [void]$summary.AppendLine('')
        [void]$summary.AppendLine("> **$($m.drift.Count) evaluated rule(s) drifted.**")
        [void]$summary.AppendLine('')
        [void]$summary.Append($detail.ToString())
    }

    if ($ReportOnly) {
        Write-Host 'Report-only: no issue, branch or PR touched'
        [void]$summary.AppendLine('')
        [void]$summary.AppendLine('> Report-only run (not dev/master): nothing opened. The proposed `VbrConfig.json` is in the sca-proposal artifact.')
        Write-Summary $summary.ToString()
        exit 0
    }

    # ---- propose / close (dev and master only) -------------------------------------------------
    $listArgs = @('issue', 'list', '--label', 'sca-drift', '--state', 'open', '--json', 'number', '--repo', $Repo)
    $openIssue = Invoke-Native gh $listArgs | Out-String | ConvertFrom-Json -ErrorAction SilentlyContinue
    $issueNum = if ($openIssue -and @($openIssue).Count -gt 0) { @($openIssue)[0].number } else { $null }

    $prList = Invoke-Native gh @('pr', 'list', '--head', $BotBranch, '--base', $BaseBranch, '--state', 'open', '--json', 'number,url', '--repo', $Repo) |
        Out-String | ConvertFrom-Json -ErrorAction SilentlyContinue
    $prNum = if ($prList -and @($prList).Count -gt 0) { @($prList)[0].number } else { $null }
    $prUrl = if ($prNum) { @($prList)[0].url } else { $null }

    if ($m.drift.Count -eq 0) {
        if ($prNum) {
            Invoke-Native gh @('pr', 'close', "$prNum", '--delete-branch', '--comment', 'Drift resolved - every evaluated rule is mapped. Closing automatically.', '--repo', $Repo) | Out-Null
            Write-Host "Closed SCA PR #$prNum"
        }
        if ($issueNum) {
            Invoke-Native gh @('issue', 'close', "$issueNum", '--comment', 'Drift resolved - every evaluated rule is mapped. Closing automatically.', '--repo', $Repo) | Out-Null
            Write-Host "Closed drift issue #$issueNum"
        }
        Write-Summary $summary.ToString()
        exit 0
    }

    # Branch + draft PR with the exact labels.
    $prNote = ''
    if ($null -ne $newText) {
        $pushed = $false
        # Never force-push (repo rule). Build on the existing bot branch when there is one so the
        # push is a plain fast-forward; otherwise cut it from the base branch. A stale base is fine:
        # the draft PR merges server-side.
        Invoke-Native git @('fetch', 'origin', $BaseBranch) | Out-Null
        Invoke-Native git @('fetch', 'origin', $BotBranch) | Out-Null
        $remoteExists = $script:NativeOk
        $from = if ($remoteExists) { "origin/$BotBranch" } else { "origin/$BaseBranch" }
        Invoke-Native git @('checkout', '-B', $BotBranch, $from) | Out-Null
        if ($script:NativeOk) {
            $currentText = Get-Content -Path $ConfigPath -Raw
            $labels = [ordered]@{}
            foreach ($p in $m.patchable) { $labels[$p.enum] = $p.proposedLabel }
            $patched = Update-VbrConfigLabels -Text $currentText -Labels $labels -ValidatedFor $bump
            if ($patched -ceq $currentText) {
                if ($remoteExists) { $pushed = $true; Write-Host "Bot branch $BotBranch already carries these labels; nothing to push" }
                else { $prNote = "The base branch ($BaseBranch) already carries these labels; no PR needed." }
            }
            else {
                Set-Content -Path $ConfigPath -Value $patched -Encoding UTF8 -NoNewline
                Invoke-Native git @('add', '--', $ConfigPath) | Out-Null
                Invoke-Native git @('-c', 'user.name=github-actions[bot]', '-c', 'user.email=41898282+github-actions[bot]@users.noreply.github.com',
                    'commit', '-m', 'fix(config): refresh SCA rule labels from the VBR console', '-m', "Proposed automatically from $RunUrl") | Out-Null
                Invoke-Native git @('push', 'origin', $BotBranch) | Out-Null
                $pushed = $script:NativeOk
                if (-not $pushed) { $prNote = 'The bot branch push was rejected (not a fast-forward or no permission) and the proposer never force-pushes, so the exact changes are in this issue instead.' }
            }
        }

        if ($pushed) {
            Invoke-Native gh @('label', 'create', 'AI-assisted', '--description', 'Change authored with AI assistance; human review required', '--repo', $Repo) | Out-Null
            $prBody = "## SCA label drift`n`nDetected in $RunUrl.`n`n$($detail.ToString())`nProposed by CI from live VBR console labels (deterministic join, no AI in the loop). Draft; a human approves the merge."
            $bodyFile = Join-Path $OutDir 'pr-body.md'
            Set-Content -Path $bodyFile -Value $prBody -Encoding UTF8
            if ($prNum) {
                Invoke-Native gh @('pr', 'edit', "$prNum", '--body-file', $bodyFile, '--repo', $Repo) | Out-Null
                Write-Host "Updated SCA PR #$prNum"
            }
            else {
                $created = Invoke-Native gh @('pr', 'create', '--draft', '--base', $BaseBranch, '--head', $BotBranch,
                    '--title', 'fix(config): refresh SCA rule labels from the VBR console', '--body-file', $bodyFile,
                    '--label', 'AI-assisted', '--repo', $Repo)
                if ($script:NativeOk) { $prUrl = ($created | Out-String).Trim(); Write-Host "Created draft PR: $prUrl" }
                else {
                    $prNote = 'The bot could not create the PR (the repo setting "Allow GitHub Actions to create and approve pull requests" is probably off), so the exact changes are in this issue instead.'
                    Write-Host "::warning::$prNote"
                }
            }
        }
        elseif (-not $prNote) {
            $prNote = 'The bot could not push its branch, so the exact changes are in this issue instead.'
            Write-Host "::warning::$prNote"
        }
    }
    elseif ($prNum) {
        Invoke-Native gh @('pr', 'close', "$prNum", '--delete-branch', '--comment', 'No label can be proposed from this run any more. Closing; see the sca-drift issue.', '--repo', $Repo) | Out-Null
        $prNum = $null; $prUrl = $null
    }

    # Issue: one canonical, idempotent.
    $title = "[SCA Drift] $($m.drift.Count) evaluated compliance rule(s) need label attention"
    $issueBody = "## $($m.drift.Count) evaluated VBR compliance rule(s) drifted`n`n" +
        "**Servers:** $($m.servers -join ', ')`n**Highest VBR seen:** $(if ($m.highestVersion) { $m.highestVersion } else { 'unknown' })`n**Mapping validated for:** $($m.validatedFor)`n**Detected in run:** $RunUrl`n`n"
    if ($prUrl) { $issueBody += "**Proposed fix (draft PR):** $prUrl`n`n" }
    if ($prNote) { $issueBody += "> $prNote`n`n" }
    $issueBody += $detail.ToString()
    $issueBody += "Dormant enum values (never evaluated) are not drift and are not listed here; see the run's job summary.`nThis issue closes automatically on the first clean run."
    if ($prNote) { [void]$summary.AppendLine(''); [void]$summary.AppendLine("> $prNote") }
    $issueFile = Join-Path $OutDir 'issue-body.md'
    Set-Content -Path $issueFile -Value $issueBody -Encoding UTF8

    if ($issueNum) {
        Invoke-Native gh @('issue', 'edit', "$issueNum", '--title', $title, '--body-file', $issueFile, '--repo', $Repo) | Out-Null
        Invoke-Native gh @('issue', 'comment', "$issueNum", '--body', "Re-checked by run $RunUrl - $($m.drift.Count) evaluated rule(s) still drifting.", '--repo', $Repo) | Out-Null
        Write-Host "Updated drift issue #$issueNum"
    }
    else {
        $made = Invoke-Native gh @('issue', 'create', '--label', 'sca-drift', '--title', $title, '--body-file', $issueFile, '--repo', $Repo)
        Write-Host "Created drift issue: $(($made | Out-String).Trim())"
    }
    Write-Summary $summary.ToString()
}
catch {
    Write-Host "::warning::SCA proposal failed and was skipped: $($_.Exception.Message)"
    Write-Summary "## SCA Drift Proposal`n`n> Skipped - proposer errored: $($_.Exception.Message)"
}

exit 0
