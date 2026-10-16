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
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check CsvOutputs -LabProfile vbr-2|vbr-13-sql|vbr-12
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check SessionLog [-RequireSessionRows]
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check ReportContent [-ReportPath <html>]
#   & "$env:GITHUB_WORKSPACE\.github\scripts\Test-VhcLiveRun.ps1" -Check ReportTables [-ReportPath <html>]
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
    [ValidateSet('', 'CsvContent', 'RowCounts', 'CsvOutputs', 'SessionLog', 'ReportContent', 'ReportTables', 'Json', 'Exports', 'Scrub')]
    [string]$Check = '',
    [string]$CsvRoot = 'C:\temp\vHC\Original\VBR',
    [string]$ReportDir = 'C:\temp\vHC\vHC-Report',
    [string]$AnonDir = 'C:\temp\vHC\vHC-AnonymousReport',
    [string]$LogDir = 'C:\temp\vHC\Original\Log',
    [string]$ReportPath = '',
    [switch]$RequireSessionRows,
    [string[]]$ScrubHost = @(),
    [datetime]$NewerThan = [datetime]::MinValue,
    [string]$E2EChecksPath = '',
    [string]$LabProfile = '',
    [string]$RepoRoot = '',
    [string]$BaselineRoot = '',
    [string]$ManifestPath = ''
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

# Core report sections, matched by element id (the nav ids in CHtmlCompiler.BuildSidebar). A core
# section that is absent, or whose first table has no data row, fails the job. Kind 'Grid' is a card
# of status tiles with no table (the security summary). ChildIdPrefix extends a section's scope across
# nested cards whose id starts with the prefix (the Job Info container holds one jobTable-* card per type).
$script:CoreSections = @(
    @{ Id = 'license';    Name = 'License Summary';    Kind = 'Table' },
    @{ Id = 'secsummary'; Name = 'Security Summary';   Kind = 'Grid' },
    @{ Id = 'vbrserver';  Name = 'Backup Server';      Kind = 'Table' },
    @{ Id = 'proxies';    Name = 'Proxy Info';         Kind = 'Table' },
    @{ Id = 'repos';      Name = 'Repository Details'; Kind = 'Table' },
    @{ Id = 'jobsummary'; Name = 'Job Summary';        Kind = 'Table' },
    @{ Id = 'jobs';       Name = 'Job Info';           Kind = 'Table'; ChildIdPrefix = 'jobTable' }
)

# Legitimately empty in a lab (no SOBR, no object storage, ...): reported, never failed.
$script:OptionalSections = @(
    @{ Id = 'sobr';        Name = 'Scale-out Repositories' },
    @{ Id = 'extents';     Name = 'SOBR Extents' },
    @{ Id = 'capextents';  Name = 'Capacity Tier' },
    @{ Id = 'archextents'; Name = 'Archive Tier' },
    @{ Id = 'objstorage';  Name = 'Object Storage Repositories' }
)

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

# --- CsvOutputs ------------------------------------------------------------
# Every CSV the collectors can write, enumerated from the collector source (not a hand list):
#   Export-VhciCsv -FileName '_X.csv'              (the vHC-VbrConfig module)
#   ${VBRServer}_X.csv                             (direct Export-Csv: Get-NasInfo, the collection manifest)
#   -ChildPath "X.csv"                             (the session report, which has no hostname prefix)
# For each one the check asserts, by tier from .github/scripts/live-csv-expectations.json:
#   required / profile nonEmpty: the file exists, has a valid header and at least one data row.
#   anything else (feature-dependent): may be absent (Export-VhciCsv writes nothing for zero
#   records), but when present it must have a valid header.
# "Valid header" = no blank or duplicate column names, and every column the golden baselines
# (intersection across GoldenBaselines/VBRConfig*) and ObjectSchemas requiredColumns list for that
# CSV. Extra columns are allowed. CSVs with no baseline get the structural check only.

function Get-VhcRepoRoot {
    param([string]$Root)
    if ($Root) { return $Root }
    (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-VhcCsvKey {
    param([string]$FileName)
    ([IO.Path]::GetFileNameWithoutExtension($FileName)).TrimStart('_')
}

# Key (name without leading '_' and '.csv') -> @{ Key; Suffix; Collector }.
function Get-VhcCollectorCsvMap {
    param([string]$Root)
    $vbr = Join-Path $Root 'vHC\HC_Reporting\Tools\Scripts\HealthCheck\VBR'
    $map = @{}
    $files = Get-ChildItem -Path $vbr -Recurse -File -Filter '*.ps1' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\.Tests\.ps1$' -and $_.FullName -notmatch '[\\/]Validation[\\/]' }
    $patterns = @(
        "-FileName\s+'([A-Za-z0-9_]+\.csv)'",
        '\$\{?VBRServer\}?(_[A-Za-z0-9_]+\.csv)',
        '-ChildPath\s+"([A-Za-z0-9_]+\.csv)"'
    )
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($vbr.Length).TrimStart('\', '/')
        foreach ($line in (Get-Content -Path $f.FullName)) {
            if ($line -match '^\s*#') { continue }
            foreach ($pat in $patterns) {
                foreach ($m in [regex]::Matches($line, $pat)) {
                    $suffix = $m.Groups[1].Value
                    $key = Get-VhcCsvKey $suffix
                    if (-not $map.ContainsKey($key)) { $map[$key] = @{ Key = $key; Suffix = $suffix; Collector = $rel } }
                }
            }
        }
    }
    return $map
}

function ConvertFrom-VhcCsvHeaderLine {
    param([string]$Line)
    $l = $Line.TrimStart([char]0xFEFF)
    @([regex]::Split($l, ',(?=(?:[^"]*"[^"]*")*[^"]*$)') | ForEach-Object {
        $c = $_.Trim()
        if ($c.Length -ge 2 -and $c.StartsWith('"') -and $c.EndsWith('"')) { $c = $c.Substring(1, $c.Length - 2).Replace('""', '"') }
        $c
    })
}

function Get-VhcCsvHeader {
    param([string]$Path)
    $line = Get-Content -Path $Path -TotalCount 1 -ErrorAction SilentlyContinue
    if (-not $line) { return @() }
    , @(ConvertFrom-VhcCsvHeaderLine ([string]$line))
}

# Key -> string[] of columns every healthy output must carry.
function Get-VhcExpectedColumns {
    param([string]$BaselineDir)
    $expected = @{}
    $dirs = @('VBRConfig', 'VBRConfig-v13-MsSql', 'VBRConfig-v13-PostgreSql', 'NasInfo', 'SessionReports')
    $perKey = @{}
    foreach ($d in $dirs) {
        $p = Join-Path $BaselineDir $d
        if (-not (Test-Path $p)) { continue }
        foreach ($f in (Get-ChildItem -Path $p -File -Filter '*.csv')) {
            $h = Get-VhcCsvHeader $f.FullName
            if ($h.Count -eq 0) { continue }
            $k = Get-VhcCsvKey $f.Name
            if (-not $perKey.ContainsKey($k)) { $perKey[$k] = New-Object System.Collections.ArrayList }
            [void]$perKey[$k].Add($h)
        }
    }
    foreach ($k in $perKey.Keys) {
        $common = @($perKey[$k][0])
        foreach ($h in $perKey[$k]) { $common = @($common | Where-Object { $h -contains $_ }) }
        $expected[$k] = $common
    }
    $schemaDir = Join-Path $BaselineDir 'ObjectSchemas'
    if (Test-Path $schemaDir) {
        foreach ($f in (Get-ChildItem -Path $schemaDir -File -Filter '*.schema.json')) {
            $s = Get-Content -Path $f.FullName -Raw | ConvertFrom-Json
            $req = @()
            if ($s.validationRules -and $s.validationRules.requiredColumns) { $req = @($s.validationRules.requiredColumns) }
            $k = Get-VhcCsvKey $s.csvFile
            $existing = @(); if ($expected.ContainsKey($k)) { $existing = @($expected[$k]) }
            $expected[$k] = @($existing + ($req | Where-Object { $existing -notcontains $_ }))
        }
    }
    return $expected
}

function Test-VhcCsvOutputs {
    param(
        [string]$Dir,
        [string]$LabName = '',
        [string]$Root = '',
        [string]$BaselineDir = '',
        [string]$Manifest = ''
    )
    $failures = New-Object System.Collections.ArrayList
    $Root = Get-VhcRepoRoot $Root
    if (-not $BaselineDir) { $BaselineDir = Join-Path $Root 'vHC\HC_Reporting\Tools\GoldenBaselines' }
    if (-not $Manifest) { $Manifest = Join-Path $PSScriptRoot 'live-csv-expectations.json' }

    if (-not (Test-Path $Manifest)) { Write-Fail "expectations manifest not found: $Manifest"; return , @('manifest missing') }
    $exp = Get-Content -Path $Manifest -Raw | ConvertFrom-Json

    $nonEmpty = @{}   # key -> reason
    foreach ($p in $exp.required.PSObject.Properties) { $nonEmpty[$p.Name] = [string]$p.Value }
    if ($LabName) {
        $prof = $exp.profiles.PSObject.Properties[$LabName]
        if (-not $prof) { Write-Fail "unknown LabProfile '$LabName' (manifest has: $(@($exp.profiles.PSObject.Properties.Name) -join ', '))"; return , @("unknown LabProfile $LabName") }
        if ($exp.labCommon) { foreach ($p in $exp.labCommon.PSObject.Properties) { $nonEmpty[$p.Name] = [string]$p.Value } }
        foreach ($p in $prof.Value.nonEmpty.PSObject.Properties) { $nonEmpty[$p.Name] = [string]$p.Value }
    }

    $map = Get-VhcCollectorCsvMap $Root
    if ($map.Count -eq 0) { Write-Fail "no CSV writers found under $Root (collector source missing?)"; return , @('no collectors enumerated') }
    foreach ($k in $nonEmpty.Keys) {
        if (-not $map.ContainsKey($k)) {
            Write-Fail "manifest names '$k' but no collector writes it (stale manifest entry)"
            [void]$failures.Add("$k manifest entry has no collector")
        }
    }
    $baseline = Get-VhcExpectedColumns $BaselineDir

    $present = 0; $absentOk = 0
    foreach ($k in ($map.Keys | Sort-Object)) {
        $entry = $map[$k]
        $label = "$($entry.Suffix.TrimStart('_')) [collector: $($entry.Collector)]"
        $isRequired = $nonEmpty.ContainsKey($k)
        $file = $null
        if ($entry.Suffix.StartsWith('_')) { $file = Find-VhcCsv $Dir ($entry.Suffix.TrimStart('_') -replace '\.csv$', '') }
        else { $file = Get-ChildItem -Path $Dir -File -Filter $entry.Suffix -ErrorAction SilentlyContinue | Select-Object -First 1 }

        if (-not $file) {
            if ($isRequired) { Write-Fail "$label MISSING (required: $($nonEmpty[$k]))"; [void]$failures.Add("$($entry.Suffix.TrimStart('_')) missing (collector $($entry.Collector))") }
            else { $absentOk++; Write-Host "  [INFO] $label not written (feature-dependent, allowed)" }
            continue
        }
        $present++
        $header = Get-VhcCsvHeader $file.FullName
        $problems = @()
        if ($header.Count -eq 0) { $problems += 'no header line' }
        else {
            if (@($header | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) { $problems += 'blank column name' }
            $dups = @($header | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
            if ($dups.Count -gt 0) { $problems += "duplicate column(s): $($dups -join ', ')" }
            if ($baseline.ContainsKey($k)) {
                $missingCols = @($baseline[$k] | Where-Object { $header -notcontains $_ })
                if ($missingCols.Count -gt 0) { $problems += "missing expected column(s): $($missingCols -join ', ')" }
            }
        }
        if ($problems.Count -gt 0) {
            Write-Fail "$label header broken: $($problems -join '; ')"
            [void]$failures.Add("$($file.Name) header broken: $($problems -join '; ') (collector $($entry.Collector))")
            continue
        }
        $rows = Get-VhcRowCount $file.FullName
        if ($isRequired -and $rows -eq 0) {
            Write-Fail "$label has a header but 0 data rows (required: $($nonEmpty[$k]))"
            [void]$failures.Add("$($file.Name) empty (collector $($entry.Collector))")
            continue
        }
        $src = 'structural check only'; if ($baseline.ContainsKey($k)) { $src = "$(@($baseline[$k]).Count) baseline columns" }
        $tier = 'optional'; if ($isRequired) { $tier = 'required' }
        Write-Pass "$label $tier, $rows rows, $($header.Count) columns ($src)"
    }
    Write-Host "  CSV outputs: $present written, $absentOk feature-dependent not written, $($nonEmpty.Count) required, $($map.Count) enumerated from collectors"
    return , $failures.ToArray()
}

# --- SessionLog ------------------------------------------------------------
# Asserts the session-reporting step of the collection actually ran. The log lines come from
# Get-VhcBackupSessions ("Fetching backup sessions ...") and Get-VhcSessionReport
# ("Exported N session rows" / "No session rows produced"). A server with no session history may
# instead log "No backup sessions available"; that is accepted unless -RequireSessions is set.
function Test-VhcSessionLog {
    param([string]$Dir, [switch]$RequireSessions)
    $failures = New-Object System.Collections.ArrayList

    $logs = @(Get-ChildItem -Path $Dir -Filter '*.log' -File -ErrorAction SilentlyContinue)
    if ($logs.Count -eq 0) {
        Write-Fail "no collection log found in $Dir"
        [void]$failures.Add('collection log missing')
        return , $failures.ToArray()
    }
    $text = ''
    foreach ($l in $logs) { $text += (Get-Content -Path $l.FullName -Raw -ErrorAction SilentlyContinue) + "`n" }

    if ($text -match 'Fetching backup sessions') { Write-Pass 'session fetch started (Get-VhcBackupSessions ran)' }
    else { Write-Fail "log has no 'Fetching backup sessions' line; session collection never ran"; [void]$failures.Add('session fetch not logged') }

    $exported = [regex]::Match($text, 'Exported (\d+) session rows')
    if ($exported.Success) {
        $n = [int]$exported.Groups[1].Value
        if ($n -gt 0) { Write-Pass "session report exported: $n rows" }
        elseif ($RequireSessions) { Write-Fail 'session report exported 0 rows'; [void]$failures.Add('session report 0 rows') }
        else { Write-Warn 'session report exported 0 rows' }
    } elseif ($RequireSessions) {
        Write-Fail "log has no 'Exported N session rows' line; this server has session history so rows are required"
        [void]$failures.Add('session report not exported')
    } elseif ($text -match 'No session rows produced') {
        Write-Warn 'session report produced no rows (not required on this server)'
    } elseif ($text -match 'No backup sessions available') {
        Write-Warn 'no backup sessions available (not required on this server)'
    } else {
        Write-Fail "log has no session-report outcome ('Exported N session rows', 'No session rows produced' or 'No backup sessions available')"
        [void]$failures.Add('session report outcome not logged')
    }
    return , $failures.ToArray()
}

# --- Report (HTML content + tables) -----------------------------------------
function Resolve-VhcReportFile {
    param([string]$Path, [string]$Dir)
    if ($Path) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { return Get-Item -LiteralPath $Path }
        return $null
    }
    Get-VhcLatestHtml -Dir $Dir
}

# Drop script, style and comment bodies so an id or label that only appears in JS/CSS/comments
# cannot satisfy a section check.
function Remove-VhcNonMarkup {
    param([string]$Html)
    $t = [regex]::Replace($Html, '(?is)<script\b.*?</script>', '')
    $t = [regex]::Replace($t, '(?is)<style\b.*?</style>', '')
    $t = [regex]::Replace($t, '(?s)<!--.*?-->', '')
    return $t
}

# The HTML from the element carrying id="$Id" up to the next card with a different id (cards are
# <div class="section-card" id=..> now, <div class="x card" id=..> in older reports). $null when
# no element has that id.
function Get-VhcSectionHtml {
    param([string]$Html, [string]$Id, [string]$ChildIdPrefix = '')
    $startRx = New-Object System.Text.RegularExpressions.Regex ('<[A-Za-z][A-Za-z0-9]*\b[^>]*?(?<![\w-])id=["'']' + [regex]::Escape($Id) + '["''][^>]*>')
    $start = $startRx.Match($Html)
    if (-not $start.Success) { return $null }
    $from = $start.Index + $start.Length
    $end = $Html.Length
    $divRx = New-Object System.Text.RegularExpressions.Regex '<div\b[^>]*>'
    $m = $divRx.Match($Html, $from)
    while ($m.Success) {
        $tag = $m.Value
        $idm = [regex]::Match($tag, '(?<![\w-])id=["'']([\w-]+)["'']')
        if ($idm.Success -and ($tag -match 'class=["''][^"'']*(?<![\w-])(?:section-card|card)(?![\w-])')) {
            if (-not ($ChildIdPrefix -and $idm.Groups[1].Value.StartsWith($ChildIdPrefix))) { $end = $m.Index; break }
        }
        $m = $m.NextMatch()
    }
    return $Html.Substring($start.Index, $end - $start.Index)
}

# Data rows of the first <table> in $SectionHtml: <tr> count minus the header row minus
# "No data available" / "Error loading data" placeholder rows (<tr><td colspan=..><em>..</em>).
# $null when the section has no table.
function Get-VhcFirstTableDataRows {
    param([string]$SectionHtml)
    $ts = $SectionHtml.IndexOf('<table', [StringComparison]::OrdinalIgnoreCase)
    if ($ts -lt 0) { return $null }
    $te = $SectionHtml.IndexOf('</table>', $ts, [StringComparison]::OrdinalIgnoreCase)
    if ($te -lt 0) { $te = $SectionHtml.Length }
    $table = $SectionHtml.Substring($ts, $te - $ts)
    $all = ([regex]::Matches($table, '(?i)<tr[\s>]')).Count
    $placeholders = ([regex]::Matches($table, '(?is)<tr[^>]*>\s*<td[^>]*colspan[^>]*>\s*<em>')).Count
    return [math]::Max(0, $all - 1 - $placeholders)
}

function Test-VhcReportContent {
    param([string]$ReportFile)
    $failures = New-Object System.Collections.ArrayList
    $raw = Get-Content -Path $ReportFile -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Fail "$([IO.Path]::GetFileName($ReportFile)) is empty"
        [void]$failures.Add('report empty')
        return , $failures.ToArray()
    }
    $markup = Remove-VhcNonMarkup $raw

    $missingCore = @()
    foreach ($sec in $script:CoreSections) {
        if ($null -eq (Get-VhcSectionHtml -Html $markup -Id $sec.Id -ChildIdPrefix $sec.ChildIdPrefix)) { $missingCore += $sec }
    }
    if ($missingCore.Count -eq 0) { Write-Pass "All $($script:CoreSections.Count) core sections present by element id" }
    else {
        foreach ($sec in $missingCore) {
            Write-Fail "core section '$($sec.Name)' (id=`"$($sec.Id)`") missing"
            [void]$failures.Add("section $($sec.Id) missing")
        }
    }
    foreach ($sec in $script:OptionalSections) {
        if ($null -eq (Get-VhcSectionHtml -Html $markup -Id $sec.Id)) { Write-Warn "optional section '$($sec.Name)' (id=`"$($sec.Id)`") not present (may not be configured)" }
    }

    # Navigation: every href="#x" needs an id="x". Ids may contain hyphens (jobTable-backup-...).
    $ids = New-Object 'System.Collections.Generic.HashSet[string]'   # ordinal: HTML ids are case-sensitive
    foreach ($m in [regex]::Matches($raw, '(?<![\w-])id=["'']([\w-]+)["'']')) { [void]$ids.Add($m.Groups[1].Value) }
    $dead = @()
    foreach ($m in [regex]::Matches($raw, 'href=["'']#([\w-]+)["'']')) {
        if (-not $ids.Contains($m.Groups[1].Value)) { $dead += $m.Groups[1].Value }
    }
    $dead = @($dead | Select-Object -Unique)
    if ($dead.Count -eq 0) { Write-Pass 'All navigation links are valid' }
    else {
        Write-Fail "$($dead.Count) dead navigation link(s)"
        $dead | ForEach-Object { Write-Host "    - #$_" }
        [void]$failures.Add('dead navigation links')
    }

    # Percentages must be 0-100.
    $badPct = @()
    foreach ($m in [regex]::Matches($raw, '(\d+\.?\d*)\s*%')) {
        $v = 0.0
        if ((ConvertTo-VhcDouble $m.Groups[1].Value ([ref]$v)) -and ($v -lt 0 -or $v -gt 100)) { $badPct += $v }
    }
    if ($badPct.Count -eq 0) { Write-Pass 'All percentages are within 0-100%' }
    else {
        Write-Fail "$($badPct.Count) percentage(s) out of range"
        $badPct | ForEach-Object { Write-Host "    - $_%" }
        [void]$failures.Add('percentages out of range')
    }
    return , $failures.ToArray()
}

function Test-VhcReportTables {
    param([string]$ReportFile)
    $failures = New-Object System.Collections.ArrayList
    $raw = Get-Content -Path $ReportFile -Raw -Encoding UTF8
    $markup = Remove-VhcNonMarkup $raw
    foreach ($sec in $script:CoreSections) {
        $html = Get-VhcSectionHtml -Html $markup -Id $sec.Id -ChildIdPrefix $sec.ChildIdPrefix
        if ($null -eq $html) {
            Write-Fail "[$($sec.Name)] section (id=`"$($sec.Id)`") not found"
            [void]$failures.Add("$($sec.Id) missing")
            continue
        }
        if ($sec.Kind -eq 'Grid') {
            $tiles = ([regex]::Matches($html, 'class=["''][^"'']*(?<![\w-])security-item(?![\w-])')).Count
            if ($tiles -gt 0) { Write-Pass "[$($sec.Name)] $tiles status tile(s)" }
            else { Write-Fail "[$($sec.Name)] no status tiles rendered"; [void]$failures.Add("$($sec.Id) empty") }
            continue
        }
        $rows = Get-VhcFirstTableDataRows $html
        if ($null -eq $rows) { Write-Fail "[$($sec.Name)] has no table"; [void]$failures.Add("$($sec.Id) no table") }
        elseif ($rows -lt 1) { Write-Fail "[$($sec.Name)] table has no data rows"; [void]$failures.Add("$($sec.Id) no data rows") }
        else { Write-Pass "[$($sec.Name)] table has $rows data row(s)" }
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
        else { Write-Fail "$ext export is 0 bytes"; [void]$failures.Add("$ext empty"); continue }
        if ($ext -eq '.pdf') { foreach ($f in (Test-VhcPdfContent -Path $p)) { [void]$failures.Add($f) } }
        else { foreach ($f in (Test-VhcPptxContent -Path $p)) { [void]$failures.Add($f) } }
    }
    return , $failures.ToArray()
}

# PDF: starts with %PDF and has at least one page object. Page objects are counted as
# "/Type /Page" not followed by a letter, so the "/Type /Pages" tree node does not count.
function Test-VhcPdfContent {
    param([string]$Path)
    $failures = New-Object System.Collections.ArrayList
    $name = [IO.Path]::GetFileName($Path)
    $bytes = [IO.File]::ReadAllBytes($Path)
    $magic = ''
    if ($bytes.Length -ge 4) { $magic = [Text.Encoding]::ASCII.GetString($bytes, 0, 4) }
    if ($magic -ne '%PDF') {
        Write-Fail "$name does not start with %PDF"
        [void]$failures.Add('pdf magic')
        return , $failures.ToArray()
    }
    $text = [Text.Encoding]::GetEncoding(28591).GetString($bytes)
    $pages = ([regex]::Matches($text, '/Type\s*/Page(?![A-Za-z])')).Count
    if ($pages -gt 0) { Write-Pass "$name has $pages page object(s)" }
    else { Write-Fail "$name has no page objects (/Type /Page)"; [void]$failures.Add('pdf no pages') }
    return , $failures.ToArray()
}

# PPTX: a valid zip with at least one ppt/slides/slideN.xml, and at least one slide carrying
# non-blank text (<a:t>..</a:t>).
function Test-VhcPptxContent {
    param([string]$Path)
    $failures = New-Object System.Collections.ArrayList
    $name = [IO.Path]::GetFileName($Path)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = $null
    try { $zip = [IO.Compression.ZipFile]::OpenRead($Path) }
    catch {
        Write-Fail "$name is not a valid zip: $($_.Exception.Message)"
        [void]$failures.Add('pptx not a zip')
        return , $failures.ToArray()
    }
    try {
        $slides = @($zip.Entries | Where-Object { $_.FullName -match '^ppt/slides/slide\d+\.xml$' })
        if ($slides.Count -eq 0) {
            Write-Fail "$name has no ppt/slides/slide*.xml entries"
            [void]$failures.Add('pptx no slides')
        } else {
            $withText = 0
            foreach ($e in $slides) {
                $reader = New-Object IO.StreamReader ($e.Open(), [Text.Encoding]::UTF8)
                try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($xml -match '<a:t(?:\s[^>]*)?>\s*[^<\s][^<]*</a:t>') { $withText++ }
            }
            if ($withText -gt 0) { Write-Pass "$name has $($slides.Count) slide(s), $withText with text" }
            else { Write-Fail "$name has $($slides.Count) slide(s) but none contain text"; [void]$failures.Add('pptx no text') }
        }
    } finally { $zip.Dispose() }
    return , $failures.ToArray()
}

# --- Scrub -----------------------------------------------------------------
function Test-VhcScrubbedReport {
    param([string]$HtmlDir, [string[]]$HostName, [datetime]$NewerThan)
    $failures = New-Object System.Collections.ArrayList
    # Every real name that must not survive scrubbing (for example the VBR host secret and, on a
    # local run, the runner's own computer name). Blank entries are dropped.
    $hostNames = @($HostName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() } | Select-Object -Unique)
    if ($hostNames.Count -eq 0) {
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
        foreach ($hn in $hostNames) {
            if ($text.IndexOf($hn, [StringComparison]::OrdinalIgnoreCase) -lt 0) { Write-Pass "$ext contains no occurrence of the real hostname" }
            else { Write-Fail "$ext leaks the real hostname"; [void]$failures.Add("hostname in $ext") }
        }
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
    'CsvOutputs' {
        $dir = Get-VhcLatestCsvDir $CsvRoot
        if (-not $dir) { Write-Fail "no CSV output directory under $CsvRoot"; $failures = @('csv dir missing') }
        else { Write-Host "CSV directory: $($dir.FullName)"; $failures = Test-VhcCsvOutputs -Dir $dir.FullName -LabName $LabProfile -Root $RepoRoot -BaselineDir $BaselineRoot -Manifest $ManifestPath }
    }
    'SessionLog' { $failures = Test-VhcSessionLog -Dir $LogDir -RequireSessions:$RequireSessionRows }
    'ReportContent' {
        $rf = Resolve-VhcReportFile -Path $ReportPath -Dir $ReportDir
        if (-not $rf) { Write-Fail "no HTML report found (ReportPath='$ReportPath', ReportDir='$ReportDir')"; $failures = @('report missing') }
        else { Write-Host "Report: $($rf.FullName)"; $failures = Test-VhcReportContent -ReportFile $rf.FullName }
    }
    'ReportTables' {
        $rf = Resolve-VhcReportFile -Path $ReportPath -Dir $ReportDir
        if (-not $rf) { Write-Fail "no HTML report found (ReportPath='$ReportPath', ReportDir='$ReportDir')"; $failures = @('report missing') }
        else { Write-Host "Report: $($rf.FullName)"; $failures = Test-VhcReportTables -ReportFile $rf.FullName }
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
