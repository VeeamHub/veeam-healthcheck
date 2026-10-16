#Requires -Version 7
# Invoke-SecurityComplianceDriftCheck.ps1
# Per-runner Security & Compliance Analyzer (SCA) label triage. Detect-only: this script never
# touches GitHub. It writes sca-triage.json (uploaded as an artifact) and a job summary; the single
# downstream job Invoke-ScaDriftProposal.ps1 unions every server's triage and opens the PR/issue.
#
# WHAT COUNTS AS DRIFT
#   Only rules the server actually evaluated (<host>_SecurityCompliance.csv, the rules customers
#   see in the report). An evaluated rule is drift when
#     - IsMapped is false / the label map has no entry          -> verdict "missing"
#     - its console label (REST) differs from the map value     -> verdict "label-changed"
#   The catalog (<host>_SecurityComplianceCatalog.csv) lists every value of the SDK enum
#   VBRBestPracticeType, including ones this server never evaluates. Those are "dormant": listed in
#   the job summary and sca-triage.json, never a reason to open an issue.
#
# CONSOLE LABELS (deterministic join, no AI)
#   REST GET /api/v1/securityAnalyzer/bestPractices returns {id, bestPractice (full console label)}.
#   REST id == SDK rule Id == RuleId column of _SecurityCompliance.csv, so evaluated rule -> console
#   label is a join on that GUID. If REST is unreachable, credentials are absent, or the server's
#   API has no analyzer endpoint, the verdict degrades to CSV-only (IsMapped) with a visible
#   notice. This script never fails the build.
#
# USAGE (workflow step)
#   ./Invoke-SecurityComplianceDriftCheck.ps1 `
#     -CatalogPath 'C:\temp\vHC\Original\VBR\*\*\*_SecurityComplianceCatalog.csv' `
#     -Server vbr-13-sql -OutDir "$env:RUNNER_TEMP\sca-triage" `
#     -RestHost localhost [-VbrExePath '<Veeam.Backup.Service.exe>']
#   REST credentials come from env only (never parameters, never logged):
#     SCA_REST_USER, SCA_REST_PASSWORD   (SCA_REST_HOST / SCA_REST_PORT optional defaults)
#
# EXIT CODES: always 0 (drift is informational, not a build break)

param(
    [Parameter(Mandatory = $true)][string]$CatalogPath,
    [string]$Server = '',
    [string]$OutDir = '.',
    [string]$VbrConfigPath = '',
    [string]$RestHost = $env:SCA_REST_HOST,
    [int]$RestPort = $(if ($env:SCA_REST_PORT) { [int]$env:SCA_REST_PORT } else { 9419 }),
    [string]$VbrExePath = ''
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ScaTriage.psm1') -Force

function Write-Summary {
    param([string]$Text)
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $Text -Encoding UTF8
    }
}

try {
    $catalogFile = Get-ChildItem -Path $CatalogPath -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $catalogFile) {
        Write-Host "::warning::No _SecurityComplianceCatalog.csv found at '$CatalogPath' - SCA triage skipped"
        Write-Summary "## SCA Triage`n`n> Skipped - no catalog CSV found (Phase 1 may not have run on this branch)."
        exit 0
    }
    $compliancePath = Join-Path $catalogFile.DirectoryName ($catalogFile.Name -replace '_SecurityComplianceCatalog\.csv$', '_SecurityCompliance.csv')
    if (-not (Test-Path $compliancePath)) {
        Write-Host "::warning::No evaluated-rules CSV at '$compliancePath' - SCA triage skipped"
        Write-Summary "## SCA Triage`n`n> Skipped - no _SecurityCompliance.csv next to the catalog (the analyzer produced no results)."
        exit 0
    }
    if (-not $Server) { $Server = $catalogFile.Name -replace '_SecurityComplianceCatalog\.csv$', '' }

    Write-Host "Reading evaluated rules: $compliancePath"
    Write-Host "Reading catalog:         $($catalogFile.FullName)"
    $evaluated = @(Import-Csv -Path $compliancePath)
    $catalog   = @(Import-Csv -Path $catalogFile.FullName)

    if (-not $VbrConfigPath) {
        $VbrConfigPath = Join-Path $PSScriptRoot '../../vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/VbrConfig.json'
    }
    $config = Get-Content -Path $VbrConfigPath -Raw | ConvertFrom-Json
    $labelMap = $config.SecurityComplianceRuleNames
    $validatedFor = [string]$config.SecurityComplianceRulesValidatedForVbrVersion

    $vbrMajor = [string](($catalog | Select-Object -First 1).VbrVersion)

    $rest = Get-ScaRestBestPractices -RestHost $RestHost -Port $RestPort `
        -User $env:SCA_REST_USER -Password $env:SCA_REST_PASSWORD

    # Full build version: REST serverInfo first, then the local Veeam service binary. A remote
    # console host's local binary would describe the console, not the server, so it is opt-in.
    $vbrVersion = ''
    if ($rest.BuildVersion) { $vbrVersion = $rest.BuildVersion }
    elseif ($VbrExePath -and (Test-Path $VbrExePath)) {
        $vbrVersion = (Get-Item $VbrExePath).VersionInfo.ProductVersion
    }

    $triage = Get-ScaTriage -Server $Server -Evaluated $evaluated -Catalog $catalog -LabelMap $labelMap `
        -ValidatedFor $validatedFor -VbrMajor $vbrMajor -VbrVersion $vbrVersion -Rest $rest

    $evaluatedHaveIds = @($evaluated | Where-Object { $_.PSObject.Properties['RuleId'] -and $_.RuleId }).Count -gt 0
    $notices = [System.Collections.Generic.List[string]]::new()
    if (-not $triage.rest.available) {
        $notices.Add("REST console labels unavailable ($($triage.rest.reason)). Verdicts are CSV-only: unmapped rules are reported but no exact label can be proposed from this server.")
    }
    elseif (-not $evaluatedHaveIds) {
        $notices.Add('REST answered but _SecurityCompliance.csv has no RuleId column or values, so rules could not be joined to console labels. CSV-only verdicts.')
    }
    foreach ($n in $notices) { Write-Host "::notice title=SCA triage ($Server)::$n" }

    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
    $jsonPath = Join-Path $OutDir 'sca-triage.json'
    $triage | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding UTF8
    Write-Host "Wrote $jsonPath"

    $drift = @($triage.evaluated | Where-Object { $_.verdict -ne 'ok' })
    $c = $triage.counts
    if ($drift.Count -eq 0) {
        Write-Host "SCA: no drift - $($c.evaluated) evaluated rule(s) all ok; $($c.dormant) dormant enum(s) not evaluated"
    }
    else {
        Write-Host "SCA: $($drift.Count) evaluated rule(s) with drift:"
        foreach ($d in $drift) { Write-Host "  $($d.verdict) $($d.enum) -> $(if ($d.proposedLabel) { $d.proposedLabel } else { '(console label unknown)' })" }
    }

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("## SCA Triage - $Server")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("VBR $(if ($vbrVersion) { $vbrVersion } else { "major $vbrMajor" }); map validated for $validatedFor; evaluated $($c.evaluated), ok $($c.ok), missing $($c.missing), label-changed $($c.labelChanged); dormant $($c.dormant) ($($c.dormantUnmapped) unmapped).")
    foreach ($n in $notices) { [void]$sb.AppendLine(''); [void]$sb.AppendLine("> Notice: $n") }
    if ($drift.Count -gt 0) {
        [void]$sb.AppendLine(''); [void]$sb.AppendLine('| Verdict | RuleType | Map label | Console label |'); [void]$sb.AppendLine('|---|---|---|---|')
        foreach ($d in $drift) { [void]$sb.AppendLine("| $($d.verdict) | ``$($d.enum)`` | $($d.mapLabel) | $($d.consoleLabel) |") }
    }
    if ($triage.dormant.Count -gt 0) {
        [void]$sb.AppendLine(''); [void]$sb.AppendLine("<details><summary>$($triage.dormant.Count) dormant enum value(s) (never evaluated here; informational only)</summary>"); [void]$sb.AppendLine('')
        [void]$sb.AppendLine((($triage.dormant | ForEach-Object { "- ``$($_.enum)``$(if (-not $_.mapped) { ' (no map entry)' })" }) -join "`n"))
        [void]$sb.AppendLine(''); [void]$sb.AppendLine('</details>')
    }
    Write-Summary $sb.ToString()
}
catch {
    Write-Host "::warning::SCA triage failed and was skipped: $($_.Exception.Message)"
    Write-Summary "## SCA Triage`n`n> Skipped - triage errored: $($_.Exception.Message)"
}

exit 0
