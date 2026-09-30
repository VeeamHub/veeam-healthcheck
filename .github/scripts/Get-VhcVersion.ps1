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
