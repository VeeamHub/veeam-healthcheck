#Requires -Version 7.0

# VhcE2EChecks.ps1
# ---------------------------------------------------------------------------
# Pure, unit-testable validation functions for the Veeam Health Check end-to-end
# harness. Each returns a result object: @{ Check; Pass; Detail; Data }.
# Invoke-VhcE2E.ps1 orchestrates a build/run and then calls these against the
# produced artifacts. Keeping them pure (path/string in -> result out) means the
# logic is covered by VhcE2E.Tests.ps1 without a live run.
# ---------------------------------------------------------------------------

function New-VhcE2EResult {
    param([string]$Check, [bool]$Pass, [string]$Detail, $Data = $null)
    [pscustomobject]@{ Check = $Check; Pass = $Pass; Detail = $Detail; Data = $Data }
}

function Test-VhcHtmlReport {
    <#
    .Synopsis
        Validate an HTML report: exists, non-trivial size, contains every required
        anchor/section marker, and shows no raw error/exception leakage.
    #>
    param(
        [Parameter(Mandatory)] [string] $Path,
        [int] $MinBytes = 20000,
        [string[]] $RequiredAnchors = @(),
        [string[]] $ErrorMarkers = @('Unhandled exception', 'System.NullReferenceException', 'StackTrace:')
    )
    if (-not (Test-Path $Path)) { return New-VhcE2EResult 'HTML' $false "report not found: $Path" }
    $info = Get-Item $Path
    if ($info.Length -lt $MinBytes) {
        return New-VhcE2EResult 'HTML' $false "report too small: $($info.Length) bytes (< $MinBytes)"
    }
    $html = Get-Content $Path -Raw
    $missing = @($RequiredAnchors | Where-Object { $html -notmatch [regex]::Escape($_) })
    if ($missing.Count -gt 0) {
        return New-VhcE2EResult 'HTML' $false "missing required anchors: $($missing -join ', ')"
    }
    $leaks = @($ErrorMarkers | Where-Object { $html -match [regex]::Escape($_) })
    if ($leaks.Count -gt 0) {
        return New-VhcE2EResult 'HTML' $false "error markers present in report: $($leaks -join ', ')"
    }
    return New-VhcE2EResult 'HTML' $true "$([math]::Round($info.Length/1KB))KB, all $($RequiredAnchors.Count) anchors present, no error leakage" $info.Length
}

function Test-VhcCsvSet {
    <#
    .Synopsis
        Validate the collected CSV set: required files present and every CSV parses
        (header + 0..N rows) without throwing. Returns per-file parse status.
    #>
    param(
        [Parameter(Mandatory)] [string] $Dir,
        [string[]] $RequiredCsvs = @('_Jobs.csv', '_Repositories.csv', '_Servers.csv'),
        [int] $MinFiles = 5
    )
    if (-not (Test-Path $Dir)) { return New-VhcE2EResult 'CSV' $false "collection dir not found: $Dir" }
    $files = @(Get-ChildItem -Path $Dir -Filter '*.csv' -File)
    if ($files.Count -lt $MinFiles) {
        return New-VhcE2EResult 'CSV' $false "only $($files.Count) CSVs (< $MinFiles)"
    }
    $missing = @($RequiredCsvs | Where-Object { $suffix = $_; -not ($files | Where-Object { $_.Name -like "*$suffix" }) })
    if ($missing.Count -gt 0) {
        return New-VhcE2EResult 'CSV' $false "missing required CSVs: $($missing -join ', ')"
    }
    $unparseable = [System.Collections.ArrayList]::new()
    foreach ($f in $files) {
        try { $null = Import-Csv -Path $f.FullName -ErrorAction Stop } catch { [void]$unparseable.Add($f.Name) }
    }
    if ($unparseable.Count -gt 0) {
        return New-VhcE2EResult 'CSV' $false "unparseable CSVs: $($unparseable -join ', ')"
    }
    return New-VhcE2EResult 'CSV' $true "$($files.Count) CSVs present, all parse, required set complete" $files.Count
}

function Test-VhcLogFile {
    <#
    .Synopsis
        Validate the run log: present, contains the completion marker, and surface
        ERROR/exception lines. By default ERROR lines fail the check (-AllowErrors
        relaxes to surface-only).
    #>
    param(
        [Parameter(Mandatory)] [string] $Path,
        [string] $CompletionMarker = 'Starting RUN...complete',
        [switch] $AllowErrors,
        [string[]] $IgnoreLogPatterns = @()
    )
    if (-not (Test-Path $Path)) { return New-VhcE2EResult 'Log' $false "log not found: $Path" }
    $lines = Get-Content $Path
    # -not ($x -match ...) is the boolean-safe idiom for BOTH a scalar and an array
    # ($x -match on an array returns the matching elements; on a scalar returns a bool).
    if (-not ($lines -match [regex]::Escape($CompletionMarker))) {
        return New-VhcE2EResult 'Log' $false "completion marker not found: '$CompletionMarker'"
    }
    $errors = @($lines | Where-Object { $_ -match '\bERROR\b' -or $_ -match 'Exception' })
    # Known-benign patterns (e.g. optional-CSV-missing in import replay) are surfaced, not failed.
    if ($IgnoreLogPatterns.Count -gt 0) {
        $errors = @($errors | Where-Object { $line = $_; -not ($IgnoreLogPatterns | Where-Object { $line -match [regex]::Escape($_) }) })
    }
    if ($errors.Count -gt 0 -and -not $AllowErrors) {
        $sample = ($errors | Select-Object -First 3) -join ' | '
        return New-VhcE2EResult 'Log' $false "$($errors.Count) unexpected error line(s); first: $sample" $errors.Count
    }
    $note = if ($errors.Count -gt 0) { "completed with $($errors.Count) surfaced error line(s) (allowed)" } else { 'completed cleanly, no unexpected errors' }
    return New-VhcE2EResult 'Log' $true $note $errors.Count
}

function Get-VhcJsonSections {
    <#
    .Synopsis
        Load a VHC JSON report and return its Sections object (or $null).
    #>
    param([Parameter(Mandatory)] [string] $Path)
    if (-not (Test-Path $Path)) { return $null }
    try { return (Get-Content $Path -Raw | ConvertFrom-Json).Sections } catch { return $null }
}

function Test-VhcJsonReport {
    <#
    .Synopsis
        Validate the JSON report: parses, has every required section, and each
        required section has Rows.Count >= its minimum.
    .Parameter RequiredSections
        Hashtable of sectionName -> minimum row count, e.g. @{ jobInfo = 1; repos = 1 }.
    #>
    param(
        [Parameter(Mandatory)] [string] $Path,
        [hashtable] $RequiredSections = @{ jobInfo = 1 }
    )
    $sections = Get-VhcJsonSections -Path $Path
    if ($null -eq $sections) { return New-VhcE2EResult 'JSON' $false "report missing or unparseable: $Path" }
    $problems = [System.Collections.ArrayList]::new()
    foreach ($name in $RequiredSections.Keys) {
        $sec = $sections.PSObject.Properties[$name]
        if (-not $sec) { [void]$problems.Add("$name missing"); continue }
        $rowCount = @($sec.Value.Rows).Count
        if ($rowCount -lt $RequiredSections[$name]) {
            [void]$problems.Add("$name has $rowCount rows (< $($RequiredSections[$name]))")
        }
    }
    if ($problems.Count -gt 0) {
        return New-VhcE2EResult 'JSON' $false ($problems -join '; ')
    }
    return New-VhcE2EResult 'JSON' $true "all $($RequiredSections.Count) required sections present with expected rows"
}

function Compare-VhcJsonBaseline {
    <#
    .Synopsis
        Regression diff of a current JSON report's Sections against a saved baseline.
        Compares the chosen sections' Headers+Rows by normalized JSON. Returns a
        result listing any section that drifted. This is the technique used to prove
        the 1.3 [Bjobs] retirement was output-neutral.
    .Parameter Sections
        Section names to compare. Empty = compare all sections present in baseline.
    #>
    param(
        [Parameter(Mandatory)] [string] $CurrentPath,
        [Parameter(Mandatory)] [string] $BaselinePath,
        [string[]] $Sections = @()
    )
    $cur  = Get-VhcJsonSections -Path $CurrentPath
    $base = Get-VhcJsonSections -Path $BaselinePath
    if ($null -eq $cur)  { return New-VhcE2EResult 'BaselineDiff' $false "current report unreadable: $CurrentPath" }
    if ($null -eq $base) { return New-VhcE2EResult 'BaselineDiff' $false "baseline unreadable: $BaselinePath" }

    $names = if ($Sections.Count -gt 0) { $Sections } else { @($base.PSObject.Properties.Name) }
    $drift = [System.Collections.ArrayList]::new()
    foreach ($n in $names) {
        $cj = if ($cur.PSObject.Properties[$n])  { $cur.$n  | ConvertTo-Json -Depth 12 -Compress } else { $null }
        $bj = if ($base.PSObject.Properties[$n]) { $base.$n | ConvertTo-Json -Depth 12 -Compress } else { $null }
        if ($cj -ne $bj) { [void]$drift.Add($n) }
    }
    if ($drift.Count -gt 0) {
        return New-VhcE2EResult 'BaselineDiff' $false "sections drifted vs baseline: $($drift -join ', ')" $drift
    }
    return New-VhcE2EResult 'BaselineDiff' $true "all $($names.Count) compared sections match baseline"
}
