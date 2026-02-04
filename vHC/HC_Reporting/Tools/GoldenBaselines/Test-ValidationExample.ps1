<#
.SYNOPSIS
    Demonstrates CSV schema validation with example pass and fail scenarios.

.DESCRIPTION
    This script provides examples of running the Compare-GoldenBaseline.ps1 validation
    script with different scenarios to demonstrate success and failure cases.

.EXAMPLE
    ./Test-ValidationExample.ps1

.NOTES
    This is a demonstration script for documentation purposes.
#>

$ErrorActionPreference = 'Continue'

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  CSV Schema Validation - Example Scenarios" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

#region Example 1: Successful Validation

Write-Host "EXAMPLE 1: Successful Schema Validation" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Green
Write-Host ""
Write-Host "Command:" -ForegroundColor DarkGray
Write-Host "  ./Compare-GoldenBaseline.ps1 \" -ForegroundColor DarkGray
Write-Host "    -ActualPath 'GoldenBaselines/VBRConfig/' \" -ForegroundColor DarkGray
Write-Host "    -BaselinePath 'GoldenBaselines/VBRConfig/' \" -ForegroundColor DarkGray
Write-Host "    -ValidateObjectMapping" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Running validation..." -ForegroundColor Yellow
Write-Host ""

& "$scriptDir/Compare-GoldenBaseline.ps1" `
    -ActualPath "$scriptDir/VBRConfig/" `
    -BaselinePath "$scriptDir/VBRConfig/" `
    -ValidateObjectMapping

Write-Host ""
Write-Host ""

#endregion

#region Example 2: Validation with ShowDiff

Write-Host "EXAMPLE 2: Validation with Detailed Differences" -ForegroundColor Green
Write-Host "------------------------------------------------" -ForegroundColor Green
Write-Host ""
Write-Host "Command:" -ForegroundColor DarkGray
Write-Host "  ./Compare-GoldenBaseline.ps1 \" -ForegroundColor DarkGray
Write-Host "    -ActualCsv 'GoldenBaselines/VBRConfig/_Servers.csv' \" -ForegroundColor DarkGray
Write-Host "    -BaselineCsv 'GoldenBaselines/VBRConfig/_Servers.csv' \" -ForegroundColor DarkGray
Write-Host "    -ValidateObjectMapping \" -ForegroundColor DarkGray
Write-Host "    -ShowDiff" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Running validation with -ShowDiff..." -ForegroundColor Yellow
Write-Host ""

& "$scriptDir/Compare-GoldenBaseline.ps1" `
    -ActualCsv "$scriptDir/VBRConfig/_Servers.csv" `
    -BaselineCsv "$scriptDir/VBRConfig/_Servers.csv" `
    -ValidateObjectMapping `
    -ShowDiff

Write-Host ""
Write-Host ""

#endregion

#region Example 3: Simulated Failure Scenario

Write-Host "EXAMPLE 3: Simulated Failure Scenario" -ForegroundColor Red
Write-Host "-------------------------------------" -ForegroundColor Red
Write-Host ""
Write-Host "This example creates a temporary CSV with a missing column to demonstrate" -ForegroundColor DarkGray
Write-Host "what a failure looks like." -ForegroundColor DarkGray
Write-Host ""

# Create a temporary CSV with missing columns
$tempDir = Join-Path $env:TEMP "vhc-validation-test"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}

# Create a CSV that's missing the 'OSInfo' column
$badCsvContent = @"
"Info","ParentId","Id","Uid","Name","Reference","Description","IsUnavailable","Type","ApiVersion","PhysHostId","ProxyServicesCreds","Cores","CPUCount","RAM"
"Server Info","00000000-0000-0000-0000-000000000000","a1b2c3d4-e5f6-7890-abcd-ef1234567890","urn:veeam:Server:a1b2c3d4-e5f6-7890-abcd-ef1234567890","VEEAM-VBR01","","Veeam Backup Server","False","Local","12.0.0.1420","a1b2c3d4-e5f6-7890-abcd-ef1234567890","","16","2","68719476736"
"@

$badCsvPath = Join-Path $tempDir "_Servers.csv"
$badCsvContent | Out-File -FilePath $badCsvPath -Encoding utf8

Write-Host "Created test CSV at: $badCsvPath" -ForegroundColor DarkGray
Write-Host "This CSV is missing the 'OSInfo' column from the baseline." -ForegroundColor DarkGray
Write-Host ""
Write-Host "Command:" -ForegroundColor DarkGray
Write-Host "  ./Compare-GoldenBaseline.ps1 \" -ForegroundColor DarkGray
Write-Host "    -ActualCsv '$badCsvPath' \" -ForegroundColor DarkGray
Write-Host "    -BaselineCsv 'GoldenBaselines/VBRConfig/_Servers.csv' \" -ForegroundColor DarkGray
Write-Host "    -ShowDiff" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Running validation (expecting failure)..." -ForegroundColor Yellow
Write-Host ""

& "$scriptDir/Compare-GoldenBaseline.ps1" `
    -ActualCsv $badCsvPath `
    -BaselineCsv "$scriptDir/VBRConfig/_Servers.csv" `
    -ShowDiff

# Cleanup
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host ""

#endregion

#region Example 4: Markdown Output for CI/CD

Write-Host "EXAMPLE 4: Markdown Output for GitHub Actions" -ForegroundColor Green
Write-Host "----------------------------------------------" -ForegroundColor Green
Write-Host ""
Write-Host "Command:" -ForegroundColor DarkGray
Write-Host "  ./Compare-GoldenBaseline.ps1 \" -ForegroundColor DarkGray
Write-Host "    -ActualPath 'GoldenBaselines/VBRConfig/' \" -ForegroundColor DarkGray
Write-Host "    -BaselinePath 'GoldenBaselines/VBRConfig/' \" -ForegroundColor DarkGray
Write-Host "    -ValidateObjectMapping \" -ForegroundColor DarkGray
Write-Host "    -OutputMarkdown" -ForegroundColor DarkGray
Write-Host ""
Write-Host "This generates markdown output suitable for GitHub Actions job summaries." -ForegroundColor DarkGray
Write-Host "The markdown is output after the standard console output." -ForegroundColor DarkGray
Write-Host ""

# Note: We skip actually running this to avoid duplicating output
Write-Host "(Skipping execution - see README.md for markdown output format)" -ForegroundColor Yellow
Write-Host ""

#endregion

#region Summary

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The validation script supports the following modes:" -ForegroundColor White
Write-Host ""
Write-Host "  1. Basic Schema Validation" -ForegroundColor Green
Write-Host "     Compares CSV columns against golden baseline" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  2. Object Mapping Validation (-ValidateObjectMapping)" -ForegroundColor Green
Write-Host "     Also validates against C# object schema definitions" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  3. Strict Order Validation (-StrictOrder)" -ForegroundColor Green
Write-Host "     Requires columns to be in exact order" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  4. CI/CD Mode (-FailOnMismatch)" -ForegroundColor Green
Write-Host "     Exits with error code 1 on any failure" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  5. GitHub Actions (-OutputMarkdown)" -ForegroundColor Green
Write-Host "     Generates markdown for job summaries" -ForegroundColor DarkGray
Write-Host ""
Write-Host "For full documentation, see:" -ForegroundColor White
Write-Host "  - GoldenBaselines/README.md" -ForegroundColor Cyan
Write-Host "  - GoldenBaselines/ObjectSchemas/README.md" -ForegroundColor Cyan
Write-Host ""

#endregion
