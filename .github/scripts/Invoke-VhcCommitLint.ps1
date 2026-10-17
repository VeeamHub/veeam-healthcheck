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
