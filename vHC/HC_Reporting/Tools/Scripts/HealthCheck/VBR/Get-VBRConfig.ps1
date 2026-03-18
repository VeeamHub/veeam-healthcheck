#Requires -Version 5.1

<#
.Synopsis
    Entry point for VBR configuration collection.
    Loads vHC-VbrConfig module, connects to VBR, runs all collectors, and writes CSV output.
.Notes
    Version: 1.0.0
    Part of the vHC VBR Config refactor - see docs/plans/2026-02-21-vbr-config-refactor.md
.EXAMPLE
    Get-VBRConfig.ps1 -VBRServer myserver -VBRVersion 12
    Get-VBRConfig.ps1 -VBRServer myserver -VBRVersion 13 -User admin -PasswordBase64 <base64>
#>
param(
    [Parameter(Mandatory)]
    [string]$VBRServer,

    [Parameter(Mandatory = $false)]
    [int]$VBRVersion = 0,

    [Parameter(Mandatory = $false)]
    [string]$User = "",

    # Plain-text password for manual CLI invocations. The C# invoker exclusively uses $PasswordBase64.
    # When both are provided, $PasswordBase64 takes precedence.
    [Parameter(Mandatory = $false)]
    [string]$Password = "",

    [Parameter(Mandatory = $false)]
    [string]$PasswordBase64 = "",

    [Parameter(Mandatory = $false)]
    [switch]$RemoteExecution,

    [Parameter(Mandatory = $false)]
    [string]$ReportPath = "",

    [Parameter(Mandatory = $false)]
    [int]$ReportInterval = 14,

    [Parameter(Mandatory = $false)]
    [string]$LogPath = "C:\temp\vHC\Original\Log",

    [Parameter(Mandatory = $false)]
    [switch]$RescanHosts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Load and validate VbrConfig.json
# ---------------------------------------------------------------------------
$configPath = "$PSScriptRoot\VbrConfig.json"
if (-not (Test-Path $configPath)) {
    throw "VbrConfig.json not found at '$configPath'. Cannot proceed without configuration."
}

$config = Get-Content -Path $configPath -Raw | ConvertFrom-Json

foreach ($key in @('ConfigVersion', 'Thresholds', 'SecurityComplianceRuleNames', 'SecurityComplianceRulesValidatedForVbrVersion')) {
    if ($null -eq $config.$key) {
        throw "VbrConfig.json is missing required key: '$key'"
    }
}

# Validate that all expected threshold keys are present within Thresholds.
# A missing key returns $null and causes silent wrong output in the concurrency analysis.
$requiredThresholds = @(
    'VpProxyRAMPerTask','VpProxyCPUPerTask','VpProxyOSCPU','VpProxyOSRAM',
    'GpProxyRAMPerTask','GpProxyCPUPerTask','GpProxyOSCPU','GpProxyOSRAM',
    'RepoGwRAMPerTask','RepoGwCPUPerTask','RepoOSCPU','RepoOSRAM',
    'CdpProxyRAM','CdpProxyCPU',
    'BackupServerCPU_v12','BackupServerRAM_v12','BackupServerCPU_v13','BackupServerRAM_v13',
    'SqlRAMMin','SqlCPUMin',
    'CompliancePollMaxSeconds','CompliancePollIntervalSeconds'
)
foreach ($key in $requiredThresholds) {
    if ($null -eq $config.Thresholds.$key) {
        throw "VbrConfig.json Thresholds is missing required key: '$key'"
    }
}

# ---------------------------------------------------------------------------
# Resolve default output path
# ---------------------------------------------------------------------------
if ([string]::IsNullOrEmpty($ReportPath)) {
    $timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
    $ReportPath = "$($config.DefaultOutputPath)\$VBRServer\$timestamp"
}

# ---------------------------------------------------------------------------
# Import the collector module
# ---------------------------------------------------------------------------
Import-Module "$PSScriptRoot\vHC-VbrConfig\vHC-VbrConfig.psd1" -Force

# ---------------------------------------------------------------------------
# Ensure Veeam module / PSSnapin is loaded
# PS 6+ (Core/7+) only supports modules; PS 5.1 tries the PSSnapin first.
# ---------------------------------------------------------------------------
if ($PSVersionTable.PSVersion.Major -ge 6) {
    if (-not (Get-Module -Name Veeam.Backup.PowerShell -ErrorAction SilentlyContinue)) {
        Import-Module -Name Veeam.Backup.PowerShell -ErrorAction Stop
    }
} else {
    if (-not (Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue)) {
        try {
            Add-PSSnapin -Name VeeamPSSnapIn -ErrorAction Stop
        } catch {
            if (-not (Get-Module -Name Veeam.Backup.PowerShell -ErrorAction SilentlyContinue)) {
                Import-Module -Name Veeam.Backup.PowerShell -ErrorAction Stop
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Initialise module-level state (infra concerns shared by all collectors)
# ---------------------------------------------------------------------------
Initialize-VhcModule -ReportPath $ReportPath -VBRServer $VBRServer `
                     -LogLevel $config.LogLevel `
                     -LogPath $LogPath

# Collector run summary list - each Invoke-VhcCollector call appends a result row.
$collectorResults = [System.Collections.Generic.List[PSCustomObject]]::new()

# ---------------------------------------------------------------------------
# Connect to VBR server
# ---------------------------------------------------------------------------
# Pre-connect cleanup in case a stale session already exists.
try { Disconnect-VBRServer -ErrorAction SilentlyContinue } catch {}

$useCreds = (-not [string]::IsNullOrWhiteSpace($User) -and
            (-not [string]::IsNullOrWhiteSpace($PasswordBase64) -or
             -not [string]::IsNullOrWhiteSpace($Password)))

# Wrap Connect -> Collectors -> Disconnect in try/finally so Disconnect always runs,
# even if a prerequisite collector aborts the run early.
try {
    if ($useCreds) {
        if (-not [string]::IsNullOrWhiteSpace($PasswordBase64)) {
            $passwordBytes = [System.Convert]::FromBase64String($PasswordBase64)
            $plainPassword = [System.Text.Encoding]::UTF8.GetString($passwordBytes)
        } else {
            $plainPassword = $Password
        }
        $securePassword = ConvertTo-SecureString -String $plainPassword -AsPlainText -Force
        $credential     = New-Object System.Management.Automation.PSCredential($User, $securePassword)
        Connect-VBRServer -Server $VBRServer -Credential $credential -ErrorAction Stop
    } else {
        Connect-VBRServer -Server $VBRServer -ErrorAction Stop
    }

# ---------------------------------------------------------------------------
# Version detection - replace parameter-supplied version with detected version
# ---------------------------------------------------------------------------
$VBRVersion = Get-VhcMajorVersion
Write-LogFile "VBR Version: $VBRVersion"

# PS edition guard: VBR < 13 is not supported under PowerShell Core.
if ($VBRVersion -gt 0 -and $VBRVersion -lt 13 -and $PSVersionTable.PSEdition -eq 'Core') {
    throw "VBR version $VBRVersion requires Windows PowerShell 5.1. " +
          "Rerun this script under powershell.exe (not pwsh)."
}

# ---------------------------------------------------------------------------
# Optional host rescan (must run before concurrency collection)
# ---------------------------------------------------------------------------
if ($RescanHosts) {
    Write-Host "[Get-VBRConfig] Rescanning all hosts - this may take several minutes..."
    Rescan-VBREntity -AllHosts -Wait
}

# ---------------------------------------------------------------------------
# Task 4: User roles and server collection
# ---------------------------------------------------------------------------
$collectorResults.Add((Invoke-VhcCollector -Name 'UserRoles' -Action { Get-VhcUserRoles }))

$serverResult = Invoke-VhcCollector -Name 'Server' -Action { Get-VhcServer }
$collectorResults.Add($serverResult)
$VServers = $serverResult.Output
if ($null -eq $VServers) {
    throw "[Get-VBRConfig] Get-VhcServer returned null - aborting. Check VBR connectivity and logs."
}
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Task 5: Concurrency data and analysis
$concurrencyResult = Invoke-VhcCollector -Name 'ConcurrencyData' -Action {
    Get-VhcConcurrencyData -VServers $VServers -Config $config -VBRServer $VBRServer -VBRVersion $VBRVersion
}
$collectorResults.Add($concurrencyResult)
$hostRoles = $concurrencyResult.Output
$collectorResults.Add((Invoke-VhcCollector -Name 'ConcurrencyAnalysis' -Action {
    Invoke-VhcConcurrencyAnalysis -HostRoles $hostRoles -Config $config -VBRVersion $VBRVersion -BackupServerName $VBRServer
}))
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Task 6: EntraId, CapacityTier, ArchiveTier, TrafficRules, Registry, Repository
$collectorResults.Add((Invoke-VhcCollector -Name 'EntraId'          -Action { Get-VhcEntraId }))
$collectorResults.Add((Invoke-VhcCollector -Name 'CapacityTier'     -Action { Get-VhcCapacityTier }))
$collectorResults.Add((Invoke-VhcCollector -Name 'ArchiveTier'      -Action { Get-VhcArchiveTier }))
$collectorResults.Add((Invoke-VhcCollector -Name 'TrafficRules'     -Action { Get-VhcTrafficRules }))
$collectorResults.Add((Invoke-VhcCollector -Name 'RegistrySettings' -Action {
    Get-VhcRegistrySettings -RemoteExecution $RemoteExecution.IsPresent
}))

$repoResult = Invoke-VhcCollector -Name 'Repository' -Action { Get-VhcRepository -VBRVersion $VBRVersion }
$collectorResults.Add($repoResult)
$RepositoryDetails = $repoResult.Output
# $RepositoryDetails may be $null if the collector fails - Get-VhcJob must tolerate a null map
# (repo names will simply be blank in _Jobs.csv).
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Fetch backup sessions as live .NET objects and pass them explicitly to SessionReport.
# Uses Get-VBRBackupSession; objects are never serialised so .NET methods
# (GetTaskSessions, Logger.GetLog) remain available to Get-VhcSessionReport. See ADR 0006.
$backupSessionsResult = Invoke-VhcCollector -Name 'BackupSessions' -Action {
    Get-VhcBackupSessions -ReportInterval $ReportInterval
}
$collectorResults.Add($backupSessionsResult)

# Generate VeeamSessionReport.csv from the sessions returned above.
# Uses Get-VBRTaskSession for all session types (VM, Backup Copy, agent). See ADR 0004, 0012.
$collectorResults.Add((Invoke-VhcCollector -Name 'SessionReport' -Action {
    Get-VhcSessionReport -BackupSessions $backupSessionsResult.Output
}))
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Task 7: Job collectors (require $RepositoryDetails from Task 6)
$collectorResults.Add((Invoke-VhcCollector -Name 'Jobs' -Action {
    Get-VhcJob -RepositoryDetails $RepositoryDetails -VBRVersion $VBRVersion
}))
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Task 9: WAN accelerators and license (spec positions 17 & 18 - after Jobs, before Malware)
$collectorResults.Add((Invoke-VhcCollector -Name 'WanAccelerator' -Action { Get-VhcWanAccelerator }))
$collectorResults.Add((Invoke-VhcCollector -Name 'License'        -Action { Get-VhcLicense }))
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Task 8: Malware detection, security compliance, protected workloads
$collectorResults.Add((Invoke-VhcCollector -Name 'MalwareDetection'   -Action { Get-VhcMalwareDetection -VBRVersion $VBRVersion }))
$collectorResults.Add((Invoke-VhcCollector -Name 'SecurityCompliance' -Action {
    Get-VhcSecurityCompliance -VBRVersion $VBRVersion -Config $config
}))
$collectorResults.Add((Invoke-VhcCollector -Name 'ProtectedWorkloads' -Action { Get-VhcProtectedWorkloads }))
# ---------------------------------------------------------------------------

# VbrInfo runs last - reads many registry paths that must not block earlier collectors
$collectorResults.Add((Invoke-VhcCollector -Name 'VbrInfo' -Action { Get-VhcVbrInfo -VBRVersion $VBRVersion -RemoteExecution $RemoteExecution.IsPresent }))
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Collector run summary
# ---------------------------------------------------------------------------
if ($collectorResults.Count -gt 0) {
    Write-Host "`n[Get-VBRConfig] ===== Collector Summary ====="
    foreach ($r in $collectorResults) {
        $status   = if ($r.Success) { "OK  " } else { "FAIL" }
        $duration = if ($r.Duration) { "$([math]::Round($r.Duration.TotalSeconds, 1))s" } else { "-" }
        $err      = if ($r.Error)    { " | $($r.Error)" } else { "" }
        Write-Host "  [$status] $($r.Name.PadRight(30)) $duration$err"
    }
    Write-Host "[Get-VBRConfig] ============================="
    $failed = @($collectorResults | Where-Object { -not $_.Success })
    if ($failed) {
        Write-LogFile "Run completed with $($failed.Count) failed collector(s): $(($failed.Name) -join ', ')" -LogLevel "WARNING"
    }
}

# Build manifest: merge Invoke-VhcCollector results with module-level error registry.
# $collectorResults tracks unhandled exceptions (catastrophic failures).
# Get-VhcModuleErrors returns internally-caught failures from public functions.
# Multiple errors for the same collector are joined with '; ' so none are lost.
$moduleErrorMap = @{}
foreach ($e in (Get-VhcModuleErrors)) {
    if ($moduleErrorMap.ContainsKey($e.CollectorName)) {
        $moduleErrorMap[$e.CollectorName] = "$($moduleErrorMap[$e.CollectorName]); $($e.Error)"
    } else {
        $moduleErrorMap[$e.CollectorName] = $e.Error
    }
}

$manifest = foreach ($r in $collectorResults) {
    $caughtError = $moduleErrorMap[$r.Name]
    $rawError    = if ($r.Error) { $r.Error } elseif ($caughtError) { $caughtError } else { $null }
    # Strip embedded newlines so each manifest row occupies exactly one CSV line.
    $errorStr    = if ($rawError) { ($rawError -replace '[\r\n]+', ' ').Trim() } else { $null }
    [PSCustomObject]@{
        Name            = $r.Name
        Success         = $r.Success -and (-not $caughtError)
        DurationSeconds = [math]::Round($r.Duration.TotalSeconds, 2)
        Error           = $errorStr
        Timestamp       = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'
    }
}

$manifest | Export-Csv -Path (Join-Path $ReportPath "${VBRServer}_CollectionManifest.csv") `
                       -NoTypeInformation -Encoding UTF8

Write-Host "[Get-VBRConfig] Collection complete. Output: $ReportPath"
} finally {
    Disconnect-VBRServer -ErrorAction SilentlyContinue
}
