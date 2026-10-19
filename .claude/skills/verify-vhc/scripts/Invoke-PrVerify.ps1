#Requires -Version 7
<#
.SYNOPSIS
  Verify that a pull request's green checks actually exercised what it changed.
.DESCRIPTION
  Maps the PR's changed files to surfaces (.claude/skills/verify-vhc/pr-surfaces.json), reads the real check-runs and
  workflow runs on the PR head SHA, and writes .verify-vhc/evidence/pr-<N>/report.md and report.json.
  Exit code: 0 VERIFIED, 1 FAILED, 2 NEEDS-RUN, 3 NEEDS-HUMAN, 70 tool error.
  Normally invoked as: .claude/skills/verify-vhc/scripts/vhc-verify.sh pr <number|branch> [--run] [--dispatch] [--comment]
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Pr,
    [string]$Repo,
    [switch]$Run,
    [switch]$Dispatch,
    [switch]$Comment,
    [int]$WaitMinutes = 0,
    [string]$SelfWorkflow = '',
    [string]$OutDir,
    [int]$DispatchTimeoutMinutes = 120
)
$ErrorActionPreference = 'Stop'
try {
    Import-Module (Join-Path $PSScriptRoot 'PrVerifyRun.psm1') -Force -DisableNameChecking
    $p = @{ PrRef = $Pr; Run = $Run; Dispatch = $Dispatch; Comment = $Comment; WaitMinutes = $WaitMinutes; SelfWorkflow = $SelfWorkflow; DispatchTimeoutMinutes = $DispatchTimeoutMinutes }
    if ($Repo) { $p.Repo = $Repo }
    if ($OutDir) { $p.OutDir = $OutDir }
    $r = Invoke-PrVerification @p
    Write-Host $r.Markdown
    if ($r.CommentNote) { Write-Host "`n$($r.CommentNote)" }
    Write-Host "`nVERDICT: $($r.Verdict)"
    Write-Host "report: $($r.EvidenceDir)/report.md"
    if ($env:GITHUB_OUTPUT) { "verdict=$($r.Verdict)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8 }
    exit $r.ExitCode
} catch {
    Write-Host "pr-verify error: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
    exit 70
}
