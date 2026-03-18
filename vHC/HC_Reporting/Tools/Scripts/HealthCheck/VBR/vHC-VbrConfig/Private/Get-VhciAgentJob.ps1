#Requires -Version 5.1

function Get-VhciAgentJob {
    <#
    .Synopsis
        Collects computer backup jobs and legacy endpoint (EP) backup jobs.
        Exports _AgentBackupJob.csv, _EndpointJob.csv.
        Source: Get-VBRConfig.ps1 lines 1117-1145.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting agent backup jobs..."
    Write-LogFile $message

    $vaBJob = Get-VBRComputerBackupJob
    $epJob  = Get-VBREPJob

    $vaBJob | Export-VhciCsv -FileName '_AgentBackupJob.csv'
    $epJob  | Export-VhciCsv -FileName '_EndpointJob.csv'

    Write-LogFile ($message + "DONE")
}
