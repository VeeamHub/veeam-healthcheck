# PowerShell script to publish VeeamHealthCheck as a single-file, self-contained executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = $null
)

$SolutionDir = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
if (-not $OutputPath) {
    $OutputPath = Join-Path $SolutionDir "publish"
}

$ProjectPath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "VeeamHealthCheck.csproj"

Write-Host "Publishing VeeamHealthCheck..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Runtime: $Runtime" -ForegroundColor Yellow
Write-Host "Output: $OutputPath" -ForegroundColor Yellow

# Build the dotnet publish command
$publishArgs = @(
    "publish",
    $ProjectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-o", $OutputPath
)

# Execute publish
& dotnet $publishArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish completed successfully!" -ForegroundColor Green
    Write-Host "Output location: $OutputPath" -ForegroundColor Green
} else {
    Write-Host "`nPublish failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
