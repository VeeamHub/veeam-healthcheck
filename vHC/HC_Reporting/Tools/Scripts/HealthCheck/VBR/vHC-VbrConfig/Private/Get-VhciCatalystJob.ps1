#Requires -Version 5.1

function Get-VhciCatalystJob {
    <#
    .Synopsis
        Collects Catalyst copy jobs.
        Exports _catCopyjob.csv.
        Source: Get-VBRConfig.ps1 lines 1102-1122.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting Catalyst jobs..."
    Write-LogFile $message

    $catCopy = Get-VBRCatalystCopyJob

    $catCopy | Export-VhciCsv -FileName '_catCopyjob.csv'

    Write-LogFile ($message + "DONE")
}
