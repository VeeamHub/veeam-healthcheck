#Requires -Version 7
# Invoke-SecurityComplianceDriftCheck.ps1
# Reads _SecurityComplianceCatalog.csv from a VHC run and manages a canonical
# GitHub issue (label: sca-drift) for unmapped compliance rule types.
# Idempotent: updates the existing open issue rather than creating duplicates.
# Auto-closes the issue on a clean run (no unmapped rules).
#
# USAGE (from GitHub Actions step):
#   ./Invoke-SecurityComplianceDriftCheck.ps1 `
#     -CatalogPath 'C:\temp\vHC\Original\VBR\*\*\*_SecurityComplianceCatalog.csv' `
#     -Repo 'org/repo' `
#     -RunUrl 'https://github.com/org/repo/actions/runs/123' `
#     [-ReportOnly]
#
# -ReportOnly: detect and summarise drift, but never open, edit, comment on or close
# the sca-drift issue. CI passes it on every ref except dev and master so feature-branch
# dispatches cannot churn the canonical issue.
#
# REQUIREMENTS: gh CLI authenticated via GH_TOKEN env var, issues: write permission
# EXIT CODES: always 0 (drift is informational, not a build break)

param(
    [Parameter(Mandatory=$true)][string]$CatalogPath,
    [Parameter(Mandatory=$true)][string]$Repo,
    [Parameter(Mandatory=$true)][string]$RunUrl,
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'

function Write-Summary {
    param([string]$Text)
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $Text -Encoding UTF8
    }
}

# Resolve catalog file (glob path support)
$catalog = Get-ChildItem -Path $CatalogPath -ErrorAction SilentlyContinue `
    | Sort-Object LastWriteTime -Descending `
    | Select-Object -First 1

if ($null -eq $catalog) {
    Write-Host "::warning::No _SecurityComplianceCatalog.csv found at '$CatalogPath' - drift check skipped"
    Write-Summary "## SCA Drift Check`n`n> Skipped - no catalog CSV found (Phase 1 may not have run on this branch)."
    exit 0
}

Write-Host "Reading catalog: $($catalog.FullName)"
$rows     = Import-Csv -Path $catalog.FullName
$unmapped = @($rows | Where-Object { $_.IsMapped -eq 'False' })

if ($unmapped.Count -eq 0) {
    Write-Host "SCA: no drift detected - all rules mapped"
    Write-Summary "## SCA Drift Check`n`n> All compliance rules mapped. No action required."

    if ($ReportOnly) { Write-Host "Report-only: not touching the sca-drift issue"; exit 0 }

    # Auto-close any open drift issue
    $open = gh issue list --label sca-drift --state open --json number --repo $Repo 2>$null | ConvertFrom-Json
    if ($open -and $open.Count -gt 0) {
        $issueNum = $open[0].number
        gh issue close $issueNum --repo $Repo --comment "Drift resolved - all rules now mapped. Closing automatically." 2>$null
        Write-Host "Closed drift issue #$issueNum"
    }
    exit 0
}

# Build issue content
$vbrVersion   = ($rows | Select-Object -First 1).VbrVersion
$validatedFor = ($rows | Select-Object -First 1).ValidatedFor

$tableLines = @('| RuleType | LabelSource | FallbackLabel |', '|---|---|---|')
foreach ($r in $unmapped) {
    $tableLines += "| ``$($r.RuleType)`` | $($r.LabelSource) | $($r.MappedLabel) |"
}
$table = $tableLines -join "`n"

$body = @"
## $($unmapped.Count) unmapped VBR compliance rule(s) detected

**VBR version on runner:** $vbrVersion
**Mapping validated for:** $validatedFor
**Detected in run:** $RunUrl

$table

### How to fix

1. Run ``.\Check-SecurityComplianceDrift-REST.ps1`` against a lab to confirm the console labels.
2. Add each RuleType to ``VbrConfig.json`` SecurityComplianceRuleNames with the correct label.
3. Bump ``SecurityComplianceRulesValidatedForVbrVersion`` to match the current VBR version.
4. Push - CI will auto-close this issue on the next clean run.
"@

$title = "[SCA Drift] $($unmapped.Count) unmapped compliance rule(s) (VBR $vbrVersion)"

if ($ReportOnly) {
    Write-Host "Report-only: $($unmapped.Count) unmapped rule(s); not touching the sca-drift issue"
    Write-Summary "## SCA Drift Check`n`n> **$($unmapped.Count) unmapped compliance rule(s) detected** (VBR $vbrVersion). Report-only run: no issue opened.`n`n$table"
    exit 0
}

# Idempotent: find existing open drift issue
$existing = gh issue list --label sca-drift --state open --json number,title --repo $Repo 2>$null | ConvertFrom-Json

if ($existing -and $existing.Count -gt 0) {
    $issueNum = $existing[0].number
    gh issue edit $issueNum --body $body --repo $Repo 2>$null
    gh issue comment $issueNum --body "Re-checked by run $RunUrl - $($unmapped.Count) unmapped rule(s) still present." --repo $Repo 2>$null
    Write-Host "Updated drift issue #$issueNum"
} else {
    $created = gh issue create --label sca-drift --title $title --body $body --repo $Repo 2>$null
    Write-Host "Created drift issue: $created"
}

Write-Summary @"
## SCA Drift Check

> **$($unmapped.Count) unmapped compliance rule(s) detected** (VBR $vbrVersion)

$table

See [workflow run]($RunUrl) for details. A GitHub issue has been opened/updated with remediation steps.
"@

exit 0
