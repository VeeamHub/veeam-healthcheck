#Requires -Version 5.1

function Add-VhciModuleError {
    <#
    .Synopsis
        Registers a failure in the module-level error registry ($script:ModuleErrors).
        Must be called from within a catch block in a public collector function.
        Initialize-VhcModule must have been called before this function is used.
        See ADR 0007 and its 2026-03-05 amendment.
    .Parameter CollectorName
        The collector name string as used in the Invoke-VhcCollector -Name argument
        in Get-VBRConfig.ps1. Case must match exactly for manifest merging.
    .Parameter ErrorMessage
        The error message string to record (typically $_.Exception.Message).
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $CollectorName,
        [Parameter(Mandatory)] [string] $ErrorMessage
    )

    $script:ModuleErrors.Add([PSCustomObject]@{
        CollectorName = $CollectorName
        Error         = $ErrorMessage
        Timestamp     = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'
    })
}
