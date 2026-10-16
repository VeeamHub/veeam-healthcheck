# Test-VhcLiveRun.ps1
# Shared assertions for the live-VBR integration jobs in .github/workflows/ci-cd.yaml
# (integration-test-vbr-2, integration-test-vbr-13-sql, integration-test-vbr-12).
#
# Runs on Windows PowerShell 5.1 and PowerShell 7. Keep it ASCII and free of 7-only syntax
# (the vbr-12 runner is 5.1). Only the Json check dot-sources a PowerShell-7-only helper.
#
# USAGE (from a workflow step):
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check CsvContent
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check RowCounts [-RequireSessionRows]
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check Json
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check Exports
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check Scrub -ScrubHost $env:VBR_HOST -NewerThan $passStart
#
# Dot-source it with no -Check to load the functions (used by the local RED/GREEN tests).
# EXIT CODE: 1 when any assertion fails, 0 otherwise.
#
# Design rule: a file a check depends on that is missing is a FAILURE, never a silent skip.
# CSV files are named <hostname>_<Name>.csv (for example localhost_vbrinfo.csv), so every lookup
# is a glob on the suffix; the hostname prefix is never assumed.

param(
    [ValidateSet('', 'CsvContent', 'RowCounts', 'Json', 'Exports', 'Scrub')]
    [string]$Check = '',
    [string]$CsvRoot = 'C:\temp\vHC\Original\VBR',
    [string]$ReportDir = 'C:\temp\vHC\vHC-Report',
    [string]$AnonDir = 'C:\temp\vHC\vHC-AnonymousReport',
    [switch]$RequireSessionRows,
    [string]$ScrubHost = '',
    [datetime]$NewerThan = [datetime]::MinValue,
    [string]$E2EChecksPath = ''
)

# Top-level keys of the JSON export that every VBR report carries (CFullReportJson).
$script:JsonTopLevelKeys = @('VhcVersion', 'Licenses', 'Sections')

# Sections["..."] keys written by the VBR table builders on a populated server. Presence only,
# except jobInfo which must have >0 rows (same >0 rule as the Jobs CSV). No per-server counts.
$script:JsonRequiredSections = @{
    jobInfo       = 1
    jobSummary    = 0
    repos         = 0
    proxies       = 0
    serverSummary = 0
    backupServer  = 0
}

function Write-Pass { param([string]$Text) Write-Host "  [OK]   $Text" -ForegroundColor Green }
function Write-Warn { param([string]$Text) Write-Host "  [WARN] $Text" -ForegroundColor Yellow }
function Write-Fail { param([string]$Text) Write-Host "  [FAIL] $Text" -ForegroundColor Red }

function Get-VhcLatestCsvDir {
    param([string]$Root)
    Get-ChildItem -Path (Join-Path $Root '*\*') -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

# <hostname>_<Name>.csv by suffix glob. Shortest match wins so '*_Jobs.csv' cannot pick a longer sibling.
function Find-VhcCsv {
    param([string]$Dir, [string]$Name)
    Get-ChildItem -Path $Dir -File -Filter "*_$Name.csv" -ErrorAction SilentlyContinue |
        Sort-Object { $_.Name.Length } | Select-Object -First 1
}

function Get-VhcRowCount {
    param([string]$Path)
    @(Import-Csv -Path $Path -ErrorAction SilentlyContinue).Count
}

function ConvertTo-VhcDouble {
    param($Value, [ref]$Result)
    [double]::TryParse([string]$Value, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, $Result)
}

# --- CsvContent ------------------------------------------------------------
function Test-VhcCsvContent {
    param([string]$Dir)
    $failures = New-Object System.Collections.ArrayList

    $vbrinfoFile = Find-VhcCsv $Dir 'vbrinfo'
    if (-not $vbrinfoFile) {
        Write-Fail '*_vbrinfo.csv not found'
        [void]$failures.Add('vbrinfo.csv missing')
    } else {
        $row = @(Import-Csv -Path $vbrinfoFile.FullName) | Select-Object -First 1
        if (-not $row) {
            Write-Fail "$($vbrinfoFile.Name) has no data row"
            [void]$failures.Add('vbrinfo.csv empty')
        } elseif ($row.Version -match '^\d{1,2}\.\d{1,2}\.\d{1,2}\.\d{1,5}$') {
            Write-Pass "VBR version format valid: $($row.Version) ($($vbrinfoFile.Name))"
        } else {
            Write-Fail "VBR version format unexpected: '$($row.Version)' ($($vbrinfoFile.Name))"
            [void]$failures.Add('vbrinfo.csv Version malformed')
        }
    }

    # The columns are CachedFreeSpace / CachedTotalSpace (GB). The old check read FreeSpace /
    # TotalSpace, which do not exist, so it compared null to null and always passed.
    $reposFile = Find-VhcCsv $Dir 'Repositories'
    if (-not $reposFile) {
        Write-Fail '*_Repositories.csv not found'
        [void]$failures.Add('Repositories.csv missing')
    } else {
        $repos = @(Import-Csv -Path $reposFile.FullName)
        $columns = @()
        if ($repos.Count -gt 0) { $columns = @($repos[0].PSObject.Properties.Name) }
        if ($repos.Count -eq 0) {
            Write-Warn "$($reposFile.Name) has no rows; FreeSpace <= TotalSpace not evaluated"
        } elseif (($columns -notcontains 'CachedFreeSpace') -or ($columns -notcontains 'CachedTotalSpace')) {
            Write-Fail "$($reposFile.Name) lacks CachedFreeSpace/CachedTotalSpace columns; the space check would be a no-op"
            [void]$failures.Add('Repositories.csv space columns missing')
        } else {
            $bad = @()
            $compared = 0
            foreach ($r in $repos) {
                $free = 0.0; $total = 0.0
                if ((ConvertTo-VhcDouble $r.CachedFreeSpace ([ref]$free)) -and (ConvertTo-VhcDouble $r.CachedTotalSpace ([ref]$total))) {
                    $compared++
                    if ($free -gt $total) { $bad += $r }
                }
            }
            if ($bad.Count -eq 0) {
                Write-Pass "All $compared comparable repositories have CachedFreeSpace <= CachedTotalSpace ($($repos.Count) rows)"
            } else {
                Write-Fail "$($bad.Count) repositories with CachedFreeSpace > CachedTotalSpace"
                $bad | ForEach-Object { Write-Host "    - $($_.Name): Free=$($_.CachedFreeSpace) > Total=$($_.CachedTotalSpace)" }
                [void]$failures.Add('Repositories.csv free > total')
            }
        }
    }

    $serversFile = Find-VhcCsv $Dir 'Servers'
    $proxiesFile = Find-VhcCsv $Dir 'Proxies'
    if (-not $serversFile) { Write-Fail '*_Servers.csv not found'; [void]$failures.Add('Servers.csv missing') }
    if (-not $proxiesFile) { Write-Fail '*_Proxies.csv not found'; [void]$failures.Add('Proxies.csv missing') }
    if ($serversFile -and $proxiesFile) {
        $serverNames = @(Import-Csv -Path $serversFile.FullName).Name
        $orphan = @(Import-Csv -Path $proxiesFile.FullName | Where-Object { $serverNames -notcontains $_.Host })
        if ($orphan.Count -eq 0) {
            Write-Pass 'All proxy hosts have corresponding server entries'
        } else {
            Write-Warn "$($orphan.Count) proxies with missing server entries (informational)"
            $orphan | ForEach-Object { Write-Host "    - Proxy '$($_.Name)' references unknown host '$($_.Host)'" }
        }
    }
    return , $failures.ToArray()
}

# --- RowCounts -------------------------------------------------------------
function Test-VhcCsvRowCounts {
    param([string]$Dir, [switch]$RequireSessions)
    $failures = New-Object System.Collections.ArrayList

    $jobs = Find-VhcCsv $Dir 'Jobs'
    if (-not $jobs) {
        Write-Fail '*_Jobs.csv not found'
        [void]$failures.Add('Jobs.csv missing')
    } else {
        $n = Get-VhcRowCount $jobs.FullName
        if ($n -gt 0) { Write-Pass "$($jobs.Name): $n rows" }
        else { Write-Fail "$($jobs.Name) has 0 rows"; [void]$failures.Add('Jobs.csv 0 rows') }
    }

    $sessions = Get-ChildItem -Path $Dir -File -Filter '*VeeamSessionReport.csv' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($RequireSessions) {
        if (-not $sessions) {
            Write-Fail 'VeeamSessionReport.csv not found'
            [void]$failures.Add('VeeamSessionReport.csv missing')
        } else {
            $n = Get-VhcRowCount $sessions.FullName
            if ($n -gt 0) { Write-Pass "$($sessions.Name): $n rows" }
            else { Write-Fail "$($sessions.Name) has 0 rows"; [void]$failures.Add('VeeamSessionReport.csv 0 rows') }
        }
    } elseif ($sessions) {
        Write-Host "  [INFO] $($sessions.Name): $(Get-VhcRowCount $sessions.FullName) rows (not required on this server)"
    }
    return , $failures.ToArray()
}

# --- helpers shared by Json / Exports / Scrub -------------------------------
function Get-VhcLatestHtml {
    param([string]$Dir, [datetime]$NewerThan = [datetime]::MinValue)
    Get-ChildItem -Path $Dir -Filter '*.html' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $NewerThan } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

function Get-VhcE2EChecksPath {
    param([string]$Path)
    if (-not $Path) {
        $Path = Join-Path $PSScriptRoot '..\..\vHC\HC_Reporting\Tools\Scripts\HealthCheck\VBR\vHC-VbrConfig\Validation\E2E\VhcE2EChecks.ps1'
    }
    if (-not (Test-Path $Path)) { throw "VhcE2EChecks.ps1 not found at $Path" }
    return $Path
}

# --- Json ------------------------------------------------------------------
function Test-VhcJsonExport {
    param([string]$HtmlDir, [string]$E2EChecksPath = '')
    $failures = New-Object System.Collections.ArrayList

    $html = Get-VhcLatestHtml -Dir $HtmlDir
    if (-not $html) {
        Write-Fail "no HTML report in $HtmlDir; cannot locate the JSON export"
        [void]$failures.Add('report missing')
        return , $failures.ToArray()
    }
    $jsonPath = [IO.Path]::ChangeExtension($html.FullName, '.json')
    if (-not (Test-Path $jsonPath)) {
        Write-Fail "JSON export missing next to the report: $([IO.Path]::GetFileName($jsonPath))"
        [void]$failures.Add('json missing')
        return , $failures.ToArray()
    }
    $json = $null
    try { $json = Get-Content -Path $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch {
        Write-Fail "JSON export is not valid JSON: $($_.Exception.Message)"
        [void]$failures.Add('json invalid')
        return , $failures.ToArray()
    }
    if ($null -eq $json) {
        Write-Fail 'JSON export parsed to nothing'
        [void]$failures.Add('json empty')
        return , $failures.ToArray()
    }
    Write-Pass "$([IO.Path]::GetFileName($jsonPath)) parses ($([math]::Round((Get-Item $jsonPath).Length / 1KB)) KB)"

    $names = @($json.PSObject.Properties.Name)
    foreach ($k in $script:JsonTopLevelKeys) {
        if ($names -notcontains $k) { Write-Fail "top-level key '$k' missing"; [void]$failures.Add("top-level $k") }
    }
    if ($json.VhcVersion) { Write-Pass "VhcVersion = $($json.VhcVersion)" }
    elseif ($names -contains 'VhcVersion') { Write-Fail 'VhcVersion is empty'; [void]$failures.Add('VhcVersion empty') }

    # Section keys + the jobInfo row floor, through the existing E2E helper.
    . (Get-VhcE2EChecksPath $E2EChecksPath)   # dot-source here: it must define Test-VhcJsonReport in THIS scope
    $res = Test-VhcJsonReport -Path $jsonPath -RequiredSections $script:JsonRequiredSections
    if ($res.Pass) { Write-Pass "Sections: $($res.Detail)" }
    else { Write-Fail "Sections: $($res.Detail)"; [void]$failures.Add('json sections') }

    return , $failures.ToArray()
}

# --- Exports (PDF / PPTX) -------------------------------------------------
function Test-VhcExports {
    param([string]$HtmlDir)
    $failures = New-Object System.Collections.ArrayList
    $html = Get-VhcLatestHtml -Dir $HtmlDir
    if (-not $html) {
        Write-Fail "no HTML report in $HtmlDir"
        [void]$failures.Add('report missing')
        return , $failures.ToArray()
    }
    foreach ($ext in @('.pdf', '.pptx')) {
        $p = [IO.Path]::ChangeExtension($html.FullName, $ext)
        if (-not (Test-Path $p)) { Write-Fail "$ext export missing: $([IO.Path]::GetFileName($p))"; [void]$failures.Add("$ext missing"); continue }
        $len = (Get-Item $p).Length
        if ($len -gt 0) { Write-Pass "$([IO.Path]::GetFileName($p)): $len bytes" }
        else { Write-Fail "$ext export is 0 bytes"; [void]$failures.Add("$ext empty") }
    }
    return , $failures.ToArray()
}

# --- Scrub -----------------------------------------------------------------
function Test-VhcScrubbedReport {
    param([string]$HtmlDir, [string]$HostName, [datetime]$NewerThan)
    $failures = New-Object System.Collections.ArrayList
    if (-not $HostName) {
        Write-Fail 'no real hostname supplied; the leak check would be vacuous'
        [void]$failures.Add('no host')
        return , $failures.ToArray()
    }
    $html = Get-VhcLatestHtml -Dir $HtmlDir -NewerThan $NewerThan
    if (-not $html) {
        Write-Fail "no scrubbed HTML produced in $HtmlDir by this pass"
        [void]$failures.Add('scrubbed html missing')
        return , $failures.ToArray()
    }
    Write-Pass "scrubbed report: $($html.Name)"
    $jsonPath = [IO.Path]::ChangeExtension($html.FullName, '.json')
    $targets = @($html.FullName)
    if (Test-Path $jsonPath) { $targets += $jsonPath }
    else { Write-Fail 'scrubbed JSON export missing'; [void]$failures.Add('scrubbed json missing') }

    foreach ($t in $targets) {
        $text = Get-Content -Path $t -Raw -Encoding UTF8
        $ext = [IO.Path]::GetExtension($t)
        if ($text.IndexOf($HostName, [StringComparison]::OrdinalIgnoreCase) -lt 0) { Write-Pass "$ext contains no occurrence of the real hostname" }
        else { Write-Fail "$ext leaks the real hostname"; [void]$failures.Add("hostname in $ext") }
    }

    # The CSS and JS of every report mention "Orphaned & Superseded Backups" in comments, so a
    # plain substring check passes even when the section is gone. Anchor on the section element.
    $htmlText = Get-Content -Path $html.FullName -Raw -Encoding UTF8
    if ($htmlText -match '(?s)id="orphanedsupersededbackups".{0,500}?Orphaned (?:&amp;|&) Superseded Backups\s*</h2>') {
        Write-Pass 'Orphaned & Superseded Backups section present in HTML'
    } else {
        Write-Fail 'Orphaned & Superseded Backups section missing from HTML'
        [void]$failures.Add('orphaned html section')
    }

    if (Test-Path $jsonPath) {
        $json = $null
        try { $json = Get-Content -Path $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
        if ($null -eq $json) { Write-Fail 'scrubbed JSON is not valid JSON'; [void]$failures.Add('scrubbed json invalid') }
        elseif (@($json.PSObject.Properties.Name) -contains 'OrphanedSupersededBackups') { Write-Pass 'OrphanedSupersededBackups key present in scrubbed JSON' }
        else { Write-Fail 'OrphanedSupersededBackups key missing from scrubbed JSON'; [void]$failures.Add('orphaned json key') }
    }
    return , $failures.ToArray()
}

# --- dispatch --------------------------------------------------------------
if (-not $Check) { return }

$failures = @()
switch ($Check) {
    'CsvContent' {
        $dir = Get-VhcLatestCsvDir $CsvRoot
        if (-not $dir) { Write-Fail "no CSV output directory under $CsvRoot"; $failures = @('csv dir missing') }
        else { Write-Host "CSV directory: $($dir.FullName)"; $failures = Test-VhcCsvContent -Dir $dir.FullName }
    }
    'RowCounts' {
        $dir = Get-VhcLatestCsvDir $CsvRoot
        if (-not $dir) { Write-Fail "no CSV output directory under $CsvRoot"; $failures = @('csv dir missing') }
        else { Write-Host "CSV directory: $($dir.FullName)"; $failures = Test-VhcCsvRowCounts -Dir $dir.FullName -RequireSessions:$RequireSessionRows }
    }
    'Json'    { $failures = Test-VhcJsonExport -HtmlDir $ReportDir -E2EChecksPath $E2EChecksPath }
    'Exports' { $failures = Test-VhcExports -HtmlDir $ReportDir }
    'Scrub'   { $failures = Test-VhcScrubbedReport -HtmlDir $AnonDir -HostName $ScrubHost -NewerThan $NewerThan }
}

if (@($failures).Count -gt 0) {
    Write-Host "$Check FAILED: $(@($failures).Count) problem(s): $(@($failures) -join '; ')" -ForegroundColor Red
    exit 1
}
Write-Host "$Check passed" -ForegroundColor Green
