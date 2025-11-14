# Run tests locally with detailed output

param(
    [switch]$Verbose,
    [switch]$Coverage
)

Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build vHC/HC.sln --configuration Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nRunning tests..." -ForegroundColor Cyan

$testArgs = @(
    "test",
    "vHC/HC.sln",
    "--no-build",
    "--configuration", "Debug"
)

if ($Verbose) {
    $testArgs += "--verbosity", "detailed"
} else {
    $testArgs += "--verbosity", "normal"
}

if ($Coverage) {
    $testArgs += "--collect", "XPlat Code Coverage"
}

$testArgs += "--logger", "console;verbosity=detailed"

& dotnet $testArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nAll tests passed!" -ForegroundColor Green
} else {
    Write-Host "`nSome tests failed!" -ForegroundColor Red
}

exit $LASTEXITCODE
