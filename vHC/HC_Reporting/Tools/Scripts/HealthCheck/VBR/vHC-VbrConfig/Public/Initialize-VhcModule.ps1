#Requires -Version 5.1

function Initialize-VhcModule {
    <#
    .Synopsis
        Injects infrastructure state into module-scoped variables.
        Must be called once by VBR-Orchestrator.ps1 before any collector runs.
    .Parameter ReportPath
        Full path where CSV output files will be written.
    .Parameter VBRServer
        VBR server name - used as filename prefix for all CSV outputs.
    .Parameter LogLevel
        Minimum log level for Write-LogFile output. Defaults to INFO.
    .Parameter LogPath
        Directory for collector log files. Defaults to C:\temp\vHC\Original\Log.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string] $ReportPath,
        [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string] $VBRServer,
        [Parameter(Mandatory = $false)] [ValidateSet("TRACE","PROFILE","DEBUG","INFO","WARNING","ERROR","FATAL")]
                               [string] $LogLevel = "INFO",
        [Parameter(Mandatory = $false)] [string] $LogPath = "C:\temp\vHC\Original\Log"
    )

    $script:ReportPath   = $ReportPath
    $script:VBRServer    = $VBRServer
    $script:LogLevel     = $LogLevel
    $script:LogPath      = $LogPath
    $script:ModuleErrors = [System.Collections.Generic.List[PSCustomObject]]::new()

    if (-not (Test-Path $ReportPath)) {
        New-Item -Path $ReportPath -ItemType Directory -Force | Out-Null
    }

    Write-LogFile "Module initialised. ReportPath=$ReportPath  VBRServer=$VBRServer  LogLevel=$LogLevel"
}
