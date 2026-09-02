#Requires -Version 7.0

# Invoke-VhcE2E.ps1
# ---------------------------------------------------------------------------
# End-to-end validation harness for Veeam Health Check. Optionally builds the
# solution, runs the tool (import-replay and/or live collection), then validates
# EVERY output artifact (HTML report, CSV set, run log, JSON report) and, when a
# baseline is supplied, diffs the JSON sections for regression.
#
# This is the pre-commit gate: run it green before committing any collection/report
# change. It generalizes the manual before/after replay used to prove 1.3.
#
#   # Import-replay regression against a known collection, diffed vs baseline:
#   ./Invoke-VhcE2E.ps1 -Mode import -ImportPath 'C:\temp\vHC\Original\VBR\host\ts' -Baseline .\baseline.json
#
#   # Live end-to-end ON the VBR server, then validate:
#   ./Invoke-VhcE2E.ps1 -Mode live -LabServer vbr-v13-rtm -Build
#
#   # Live end-to-end from THIS workstation against a REMOTE VBR (collect over
#   # PS-remoting). Needs a DPAPI-seeded cred for the host (run once:
#   # VeeamHealthCheck.exe /savecreds /host=vbr-v13-rtm.home.lab) or a -CredFile:
#   ./Invoke-VhcE2E.ps1 -Mode live -LabServer vbr-v13-rtm -RemoteHost vbr-v13-rtm.home.lab -Build
#
# Exit 0 = all gates passed; 1 = one or more gates failed; 2 = setup error.
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [ValidateSet('import', 'live', 'both')] [string] $Mode = 'import',
    [string] $ImportPath = '',
    [string] $LabServer = '',
    # Live mode against a REMOTE VBR: run the tool locally (this workstation) and
    # collect over PowerShell remoting from $RemoteHost. When empty, live mode runs
    # the local collect path (tool must execute ON the VBR server). When set to a
    # non-local FQDN, the tool is invoked with /vbr /host=<fqdn> (which implies
    # /remote) so the changed collection scripts run against the real server.
    [string] $RemoteHost = '',
    # DPAPI-stored creds for $RemoteHost are used by default (/silent). Supply a
    # JSON credfile to run fully unattended without a pre-seeded DPAPI entry.
    [string] $CredFile = '',
    [switch] $Build,
    [switch] $SkipTests,        # skip BOTH the xUnit suite and the Pester suites
    [switch] $SkipDotnetTest,   # skip only the (slow) xUnit suite, still run Pester
    [string] $ExePath = '',
    [string] $OutDir = 'C:\temp\vHC',
    [string] $Baseline = '',
    [switch] $UpdateBaseline,
    [string[]] $RequiredHtmlAnchors = @('id="jobs"', 'id="jobsummary"', 'id="license"', 'id="proxies"'),
    [hashtable] $RequiredJsonSections = @{ jobInfo = 1 },
    [switch] $AllowLogErrors,
    # Known-benign log errors surfaced but not gated:
    #   - optional-CSV-missing in import replay
    #   - VBR object-hierarchy API returning partial results during ProtectedWorkloads
    #     collection (a Write-Warning; collection continues and exports normally).
    #     Seen on remote/live lab collections; unrelated to job/report logic.
    [string[]] $IgnoreLogPatterns = @(
        'Failed to load VBR CSV data or no data found',
        'Failed to retrieve object hierarchy'
    ),
    [string] $ResultJson = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'VhcE2EChecks.ps1')

# Resolve HC_Reporting (the project dir) by walking up - robust to folder depth changes.
$hcReporting = $PSScriptRoot
while ($hcReporting -and (Split-Path $hcReporting -Leaf) -ne 'HC_Reporting') {
    $hcReporting = Split-Path $hcReporting -Parent
}
if (-not $hcReporting) { throw 'Invoke-VhcE2E: could not locate the HC_Reporting project directory above this script.' }
$results  = [System.Collections.ArrayList]::new()
function Add-Result { param($r) [void]$script:results.Add($r); $tag = if ($r.Pass) { 'PASS' } else { 'FAIL' }; Write-Host ("  [{0}] {1}: {2}" -f $tag, $r.Check, $r.Detail) -ForegroundColor ($(if ($r.Pass) { 'Green' } else { 'Red' })) }

# --- Locate exe -------------------------------------------------------------
if (-not $ExePath) {
    $candidate = Join-Path $hcReporting 'bin\Debug\net8.0-windows7.0\win-x64\VeeamHealthCheck.exe'
    if (Test-Path $candidate) { $ExePath = $candidate }
}

# --- Optional build ---------------------------------------------------------
if ($Build) {
    Write-Host '== Build ==' -ForegroundColor Cyan
    $csproj = Join-Path $hcReporting 'VeeamHealthCheck.csproj'
    $buildOut = & dotnet build $csproj -c Debug --nologo -v q 2>&1
    $ok = ($LASTEXITCODE -eq 0) -and ($buildOut -match 'Build succeeded')
    Add-Result (New-VhcE2EResult 'Build' $ok ($(if ($ok) { 'Build succeeded, 0 errors' } else { "build failed (exit $LASTEXITCODE)" })))
    if (-not $ok) { Write-Result; exit 2 }
}
if (-not $ExePath -or -not (Test-Path $ExePath)) {
    Add-Result (New-VhcE2EResult 'Setup' $false "exe not found ($ExePath) - build first or pass -ExePath"); Write-Result; exit 2
}

# --- Run + validate one pass ------------------------------------------------
function Invoke-Pass {
    param([string]$PassName, [string[]]$ExeArgs, [string]$CsvDir)

    Write-Host "== Run ($PassName) ==" -ForegroundColor Cyan
    $proc = Start-Process -FilePath $ExePath -ArgumentList $ExeArgs -NoNewWindow -PassThru -Wait
    Add-Result (New-VhcE2EResult "Run:$PassName" ($proc.ExitCode -eq 0) "exe exit code $($proc.ExitCode)")

    $reportDir = Join-Path $OutDir 'vHC-Report'
    $latestHtml = Get-ChildItem -Path $reportDir -Filter '*.html' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $latestJson = Get-ChildItem -Path $reportDir -Filter '*.json' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $latestLog  = Get-ChildItem -Path $OutDir -Filter '*.log' -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    Add-Result (Test-VhcHtmlReport -Path ($latestHtml.FullName ?? '<none>') -RequiredAnchors $RequiredHtmlAnchors)
    Add-Result (Test-VhcCsvSet -Dir $CsvDir)
    if ($latestLog) { Add-Result (Test-VhcLogFile -Path $latestLog.FullName -AllowErrors:$AllowLogErrors -IgnoreLogPatterns $IgnoreLogPatterns) }
    else { Add-Result (New-VhcE2EResult 'Log' $false "no .log found under $OutDir") }
    Add-Result (Test-VhcJsonReport -Path ($latestJson.FullName ?? '<none>') -RequiredSections $RequiredJsonSections)

    if ($UpdateBaseline -and $Baseline -and $latestJson) {
        Copy-Item $latestJson.FullName $Baseline -Force
        Write-Host "  baseline updated -> $Baseline" -ForegroundColor Yellow
    } elseif ($Baseline -and $latestJson) {
        Add-Result (Compare-VhcJsonBaseline -CurrentPath $latestJson.FullName -BaselinePath $Baseline)
    }
}

function Invoke-Tests {
    Write-Host '== Unit tests ==' -ForegroundColor Cyan
    # xUnit suite - the correctness backbone (645 tests). Slow (~2-3 min); -SkipDotnetTest to skip.
    if (-not $SkipDotnetTest) {
        $csproj = Join-Path (Split-Path $hcReporting -Parent) 'VhcXTests\VhcXTests.csproj'
        if (Test-Path $csproj) {
            $out   = & dotnet test $csproj -c Debug --nologo -v q 2>&1
            $match = $out | Select-String 'Passed!|Failed!' | Select-Object -Last 1
            # Use the MatchInfo's .Line as an explicit string; coercing a MatchInfo
            # (or a null/array) straight through -replace can yield Object[] and break .Trim().
            $lineText = if ($match) { [string]$match.Line } else { 'no test summary line found' }
            $ok   = ($LASTEXITCODE -eq 0) -and ($out -match 'Passed!')
            Add-Result (New-VhcE2EResult 'Tests:xUnit' $ok (($lineText -replace '\s+', ' ').Trim()))
        } else {
            Add-Result (New-VhcE2EResult 'Tests:xUnit' $false "VhcXTests.csproj not found at $csproj")
        }
    }
    # Pester suites - module unit tests + validation guards + E2E check functions (exclude LiveVBR).
    try {
        Import-Module Pester -MinimumVersion 5.0 -ErrorAction Stop
        $cfg = New-PesterConfiguration
        $cfg.Run.Path      = (Split-Path (Split-Path $PSScriptRoot))   # vHC-VbrConfig root
        $cfg.Run.PassThru  = $true
        $cfg.Filter.ExcludeTag = 'LiveVBR'
        $cfg.Output.Verbosity  = 'None'
        $pr = Invoke-Pester -Configuration $cfg
        Add-Result (New-VhcE2EResult 'Tests:Pester' ($pr.FailedCount -eq 0) "$($pr.PassedCount) passed, $($pr.FailedCount) failed, $($pr.SkippedCount) skipped")
    } catch {
        Add-Result (New-VhcE2EResult 'Tests:Pester' $false "Pester run error: $($_.Exception.Message)")
    }
}

function Write-Result {
    $pass = @($results | Where-Object { $_.Pass }).Count
    $fail = @($results | Where-Object { -not $_.Pass }).Count
    Write-Host ''
    Write-Host ("=== E2E RESULT: {0} passed, {1} failed ===" -f $pass, $fail) -ForegroundColor ($(if ($fail -eq 0) { 'Green' } else { 'Red' }))
    if ($ResultJson) {
        @{ pass = $pass; fail = $fail; checks = $results } | ConvertTo-Json -Depth 6 | Set-Content -Path $ResultJson -Encoding UTF8
        Write-Host "result written -> $ResultJson"
    }
}

# --- Drive modes ------------------------------------------------------------
if (-not $SkipTests) { Invoke-Tests }
if ($Mode -in 'import', 'both') {
    if (-not $ImportPath) { Add-Result (New-VhcE2EResult 'Setup' $false 'import mode needs -ImportPath'); Write-Result; exit 2 }
    Invoke-Pass -PassName 'import' -ExeArgs @("/import:$ImportPath", '/run', '/scrub:false') -CsvDir $ImportPath
}
if ($Mode -in 'live', 'both') {
    if (-not $LabServer) { Add-Result (New-VhcE2EResult 'Setup' $false 'live mode needs -LabServer'); Write-Result; exit 2 }
    # Live run uses the tool's normal collect+report path. Two shapes:
    #   - LOCAL  : run the tool ON the VBR server -> @('/run','/scrub:false')
    #   - REMOTE : run the tool on this workstation and collect from -RemoteHost
    #              -> @('/run','/remote','/host=<fqdn>','/scrub:false')
    #              The normal /run /remote path uses the DPAPI-stored cred for the
    #              host (seed once: VeeamHealthCheck.exe /savecreds /host=<fqdn>),
    #              or a /credfile= when supplied. NOTE: do NOT add /silent here --
    #              its credential lookup misses stored creds that the normal path
    #              resolves, which breaks unattended remote collection.
    $liveArgs =
        if ($RemoteHost -and -not ($RemoteHost -in @('localhost', '127.0.0.1', '.', $env:COMPUTERNAME))) {
            $a = @('/run', '/remote', "/host=$RemoteHost", '/scrub:false')
            if ($CredFile) { $a += "/credfile=$CredFile" }
            $a
        }
        else { @('/run', '/scrub:false') }
    Invoke-Pass -PassName 'live' -ExeArgs $liveArgs -CsvDir (
        (Get-ChildItem -Path (Join-Path $OutDir 'Original\VBR') -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { Get-ChildItem $_.FullName -Filter '*_Jobs.csv' -ErrorAction SilentlyContinue } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName ?? $OutDir
    )
}

Write-Result
exit ($(if (@($results | Where-Object { -not $_.Pass }).Count -gt 0) { 1 } else { 0 }))
