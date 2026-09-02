param(
    [Parameter(Mandatory)]
    [string]$VBRServer,
    [Parameter(Mandatory)]
    [int]$VBRVersion,
    [Parameter(Mandatory = $false)]
    [string]$ReportPath = "",
    [Parameter(Mandatory = $false)]
    [string]$LogPath = ""
)

# Self-contained CSV formula-injection neutralizer (this script runs standalone,
# outside the vHC-VbrConfig module, so it cannot use the module-private copy).
# Keep in sync with vHC-VbrConfig/Private/Protect-VhciCsvInjection.ps1.
function ConvertTo-VhciCsvSafeValue {
    param($Value)
    if ($Value -isnot [string] -or [string]::IsNullOrEmpty($Value)) { return $Value }
    $first = $Value[0]
    if (-not ($first -eq '=' -or $first -eq '+' -or $first -eq '-' -or
              $first -eq '@' -or $first -eq [char]9 -or $first -eq [char]13)) { return $Value }
    $parsed = [double]0
    if ([double]::TryParse($Value, [System.Globalization.NumberStyles]::Any,
            [System.Globalization.CultureInfo]::InvariantCulture, [ref] $parsed)) { return $Value }
    return "'" + $Value
}
function Protect-VhciCsvInjection {
    [CmdletBinding()]
    param([Parameter(ValueFromPipeline)] $InputObject)
    process {
        if ($null -eq $InputObject) { return }
        if ($InputObject -is [string] -or $InputObject -is [System.ValueType]) { return $InputObject }
        $safe = [ordered]@{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $safe[$prop.Name] = ConvertTo-VhciCsvSafeValue -Value $prop.Value
        }
        [pscustomobject] $safe
    }
}

# Self-contained logger. This script is launched standalone by PSInvoker, without going
# through Initialize-VhcModule (VBR-Orchestrator's setup step for the vHC-VbrConfig
# module's Write-LogFile), so it can't call that function here. Every line is written to
# STDOUT (captured by the GUI as [PS][STDOUT]) and appended to its own log file - named to
# match the CollectorX.log family other phases write - when a log directory is resolvable.
# This phase used to run completely silently, which is why a slow or stuck VMC.log parse
# looked like the whole tool hanging.
$script:NasLogFile = $null
function Write-NasLog {
    param([string]$Message, [string]$Level = 'INFO')
    $line = "{0} [{1}] [Get-NasInfo] {2}" -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $Level, $Message
    try { [Console]::Out.WriteLine($line) } catch { }
    if ($script:NasLogFile) {
        try { [System.IO.File]::AppendAllText($script:NasLogFile, $line + [Environment]::NewLine) } catch { }
    }
}

# If ReportPath not provided, use default with server name and timestamp structure
if ([string]::IsNullOrEmpty($ReportPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $ReportPath = "C:\temp\vHC\Original\VBR\$VBRServer\$timestamp"
}

# Resolve the log file. Fall back to ReportPath when no LogPath was passed so the phase
# is never silent, even when invoked standalone.
if ([string]::IsNullOrEmpty($LogPath)) { $LogPath = $ReportPath }
try {
    if (-not (Test-Path -LiteralPath $LogPath)) { New-Item -Path $LogPath -ItemType Directory -Force | Out-Null }
    $script:NasLogFile = Join-Path $LogPath "CollectorNasInfo.log"
} catch {
    $script:NasLogFile = $null
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Write-NasLog "Starting. VBRServer=$VBRServer VBRVersion=$VBRVersion ReportPath=$ReportPath PS=$($PSVersionTable.PSVersion)"

try {
    # Without this, most cmdlet failures below (Get-Content, Export-Csv, New-Item, ...) are
    # non-terminating and silently skip past the catch block, defeating the point of having one.
    $ErrorActionPreference = 'Stop'

    # VMC log path is hardcoded for now. If logs are sent elsewhere, please adjust accordingly.
    $logsPath = "C:\ProgramData\Veeam\Backup\Utils\VMC.log"

    # section identifiers
    $unstrucStart = "=====UNSTRUCTURED DATA===="
    $nasStart = "=====NAS INFRASTRUCTURE===="
    $sectionEnd = "========"

    # Report VMC.log presence and size up front -- this is the single most useful signal
    # for diagnosing a "hang": a missing file finishes instantly, a multi-GB file is the
    # slow read. Get-Item is guarded so a mocked/absent path never throws.
    if (Test-Path -LiteralPath $logsPath) {
        $vmcSizeInfo = "size unavailable"
        try {
            $fi = Get-Item -LiteralPath $logsPath -ErrorAction Stop
            $vmcSizeInfo = "{0:N1} MB, last written {1}" -f (($fi.Length) / 1MB), $fi.LastWriteTime
        } catch { }
        Write-NasLog "VMC.log found at $logsPath ($vmcSizeInfo). Reading..."
        $readSw = [System.Diagnostics.Stopwatch]::StartNew()
        $content = Get-Content -LiteralPath $logsPath
        $readSw.Stop()
        $contentCount = @($content).Count
        Write-NasLog ("Read {0} line(s) in {1:N1}s" -f $contentCount, ($readSw.Elapsed.TotalSeconds))
    } else {
        Write-NasLog "VMC.log not found at $logsPath - skipping unstructured/NAS section parsing" 'WARNING'
        $content = @()
        $contentCount = 0
    }

    $sections = @()
    $currentSection = @()
    $capturing = $false

    $lineNum = 0
    foreach($line in $content){
        $lineNum++
        if (($lineNum % 200000) -eq 0) {
            Write-NasLog ("Scanning VMC.log... {0}/{1} lines, {2} section(s) captured" -f $lineNum, $contentCount, $sections.Count)
        }
        if(-not $capturing -and $line -match $unstrucStart){
            $capturing = $true
            $currentSection = @()
        }
        elseif(-not $capturing -and $line -match $nasStart){
           $capturing = $true
           $currentSection = @()
       }
        elseif($capturing){
            if($line -match $sectionEnd){
                $capturing = $false
                $sections += ,($currentSection)
                $currentSection = @()
            }
            else{
                if (-not ($line -match '\[VmcStats\]')) {
                    $stripped = $line -replace '^\[[\d.:\s]+\]\s+\d+(?:\s+\[\w+\])?\s+\w+\s+\(\d+\)\s+', ''
                    $currentSection += $stripped
                }
            }
        }
    }
    Write-NasLog ("Scan complete: {0} line(s) processed, {1} section(s) found" -f $lineNum, $sections.Count)

    # Here we set a new list to only contain the final data section from the log:
    $dataLines = $sections[$sections.Count-1]

    # search each line, looking for these strings: TotalObjectStorageSize, NasBackupSourceShareStats, TotalShareSize. Group each into their own list
    $totalObjectStorageSize = @()
    $nasBackupSourceShareStats = @()
    $totalShareSize = @()      # v12: combined single-line records
    $parentShares = @()        # v13: parent lines (SmbServer / NfsServer, not ChildShare)
    $childShares = @()         # v13: child lines (SmbServerChildShare / NfsServerChildShare)

    $dataLines | ForEach-Object {
        if ($_ -match "TotalObjectStorageSize") {
            $totalObjectStorageSize += $_
        }
        elseif ($_ -match "NasBackupSourceShareStats") {
            $nasBackupSourceShareStats += $_
        }
        # Check child before parent -- "SmbServer" is a substring of "SmbServerChildShare"
        elseif ($_ -match "SmbServerChildShare|NfsServerChildShare") {
            $childShares += $_
        }
        elseif ($_ -match "SmbServer|NfsServer") {
            $parentShares += $_
        }
        elseif ($_ -match "TotalShareSize") {
            $totalShareSize += $_
        }
    }
    Write-NasLog ("Parsed data section: {0} ObjectStorageSize, {1} SourceShareStats, {2} parent share(s), {3} child share(s), {4} v12 share line(s)" -f `
        $totalObjectStorageSize.Count, $nasBackupSourceShareStats.Count, $parentShares.Count, $childShares.Count, $totalShareSize.Count)

    # Parse a log line into a hashtable of key-value pairs
    function ConvertTo-LogProperties([string]$logLine) {
        $props = @{}
        $logLine.Trim() -split ', ' | ForEach-Object {
            $parts = $_.Split(':', 2)
            if ($parts.Count -eq 2) {
                $props[$parts[0].Trim()] = $parts[1].Trim()
            }
        }
        return $props
    }

    $csvData = @($totalObjectStorageSize | ForEach-Object { [PSCustomObject](ConvertTo-LogProperties $_) })
    if (!(Test-Path $ReportPath)) { New-Item -Path $ReportPath -ItemType Directory -Force | Out-Null }
    $csvData | Protect-VhciCsvInjection | Export-Csv -Path "$ReportPath\${VBRServer}_NasObjectSourceStorageSize.csv" -NoTypeInformation
    Write-NasLog ("Exported {0} row(s) to {1}_NasObjectSourceStorageSize.csv" -f $csvData.Count, $VBRServer)

    $csvData2 = @($nasBackupSourceShareStats | ForEach-Object { [PSCustomObject](ConvertTo-LogProperties $_) })
    $csvData2 | Protect-VhciCsvInjection | Export-Csv -Path "$ReportPath\${VBRServer}_NasFileData.csv" -NoTypeInformation
    Write-NasLog ("Exported {0} row(s) to {1}_NasFileData.csv" -f $csvData2.Count, $VBRServer)

    # Parse v12 combined lines
    $v12Rows = @($totalShareSize | ForEach-Object {
        $props = ConvertTo-LogProperties $_
        if (-not $props.ContainsKey('ParentServerID')) { $props['ParentServerID'] = $null }
        [PSCustomObject]$props
    })

    # Parse v13 parent lines into a hashtable keyed by FileShareID
    $parentMap = @{}
    $parentShares | ForEach-Object {
        $props = ConvertTo-LogProperties $_
        if ($props.ContainsKey('FileShareID')) {
            $parentMap[$props['FileShareID']] = $props
        }
    }

    # Parse v13 child lines and join with parent
    $v13Rows = @($childShares | ForEach-Object {
        $childProps = ConvertTo-LogProperties $_
        $merged = @{}

        # Start with parent properties (if parent found)
        $parentId = $childProps['ParentServerID']
        if ($parentId -and $parentMap.ContainsKey($parentId)) {
            foreach ($key in $parentMap[$parentId].Keys) {
                $merged[$key] = $parentMap[$parentId][$key]
            }
        }

        # Overlay child properties (child wins on conflict)
        foreach ($key in $childProps.Keys) {
            $merged[$key] = $childProps[$key]
        }

        # Ensure ParentServerID always present
        if (-not $merged.ContainsKey('ParentServerID')) { $merged['ParentServerID'] = $null }
        [PSCustomObject]$merged
    })

    # Union v12 and v13 rows; v13 rows first so the header reflects the full v13 schema
    $allShareRows = @()
    if ($v13Rows.Count -gt 0) { $allShareRows += $v13Rows }
    if ($v12Rows.Count -gt 0) { $allShareRows += $v12Rows }

    $allShareRows | Protect-VhciCsvInjection | Export-Csv -Path "$ReportPath\${VBRServer}_NasSharesize.csv" -NoTypeInformation
    Write-NasLog ("Exported {0} row(s) to {1}_NasSharesize.csv ({2} v13 + {3} v12)" -f $allShareRows.Count, $VBRServer, $v13Rows.Count, $v12Rows.Count)
}
catch {
    # Deliberately not re-thrown: PSInvoker treats a non-zero exit from this script as a
    # collection failure and aborts the whole run (unlike the VBR config phase, which
    # tolerates a bad exit code when its manifest is already on disk). NAS info is
    # supplementary - log the failure loudly and let the script finish normally so a NAS-only
    # problem can't take down report generation for everything else.
    Write-NasLog "FAILED: $($_.Exception.Message)" 'ERROR'
    Write-NasLog "$($_.ScriptStackTrace)" 'ERROR'
}
finally {
    $stopwatch.Stop()
    Write-NasLog ("Completed in {0:N1}s" -f $stopwatch.Elapsed.TotalSeconds)
}
