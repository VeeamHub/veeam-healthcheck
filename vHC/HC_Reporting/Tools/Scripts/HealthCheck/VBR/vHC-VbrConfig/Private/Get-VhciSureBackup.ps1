#Requires -Version 5.1

function Get-VhciSureBackup {
    <#
    .Synopsis
        Collects SureBackup jobs, application groups, and virtual labs.
        Exports _SureBackupJob.csv, _SureBackupAppGroups.csv, _SureBackupVirtualLabs.csv.
        Source: Get-VBRConfig.ps1 lines 1147-1153 and 1386-1409.
        Note: the SureBackup job export (originally at line 1153) and the app group/virtual lab
        exports (lines 1386-1409) are consolidated here for cohesion.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting SureBackup data..."
    Write-LogFile $message

    $sbJob = Get-VBRSureBackupJob
    Write-LogFile "Found $(@($sbJob).Count) SureBackup jobs"
    $sbAppGroups = Get-VBRApplicationGroup
    Write-LogFile "Found $(@($sbAppGroups).Count) SureBackup application groups"
    $sbVirtualLabs = Get-VBRVirtualLab
    Write-LogFile "Found $(@($sbVirtualLabs).Count) SureBackup virtual labs"

    $sbJob         | Export-VhciCsv -FileName '_SureBackupJob.csv'
    $sbAppGroups   | Export-VhciCsv -FileName '_SureBackupAppGroups.csv'
    $sbVirtualLabs | Export-VhciCsv -FileName '_SureBackupVirtualLabs.csv'

    Write-LogFile ($message + "DONE")
}
