#Requires -Version 5.1

function Get-VhciTapeInfrastructure {
    <#
    .Synopsis
        Collects tape infrastructure: jobs, servers, libraries, media pools, and vaults.
        Exports _TapeJobs.csv, _TapeServers.csv, _TapeLibraries.csv, _TapeMediaPools.csv, _TapeVaults.csv.
        Source: Get-VBRConfig.ps1 lines 1155-1212.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting tape infrastructure..."
    Write-LogFile $message

    $tapeJob = Get-VBRTapeJob
    $tapeServers = Get-VBRTapeServer
    Write-LogFile "Found $(@($tapeServers).Count) tape servers"
    $tapeLibraries = Get-VBRTapeLibrary
    Write-LogFile "Found $(@($tapeLibraries).Count) tape libraries"
    $tapeMediaPools = Get-VBRTapeMediaPool
    Write-LogFile "Found $(@($tapeMediaPools).Count) tape media pools"
    $tapeVaults = Get-VBRTapeVault
    Write-LogFile "Found $(@($tapeVaults).Count) tape vaults"

    $tapeJob        | Export-VhciCsv -FileName '_TapeJobs.csv'
    $tapeServers    | Export-VhciCsv -FileName '_TapeServers.csv'
    $tapeLibraries  | Export-VhciCsv -FileName '_TapeLibraries.csv'
    $tapeMediaPools | Export-VhciCsv -FileName '_TapeMediaPools.csv'
    $tapeVaults     | Export-VhciCsv -FileName '_TapeVaults.csv'

    Write-LogFile ($message + "DONE")
}
