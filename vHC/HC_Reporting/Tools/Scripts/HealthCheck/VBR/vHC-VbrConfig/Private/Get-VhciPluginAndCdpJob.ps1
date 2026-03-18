#Requires -Version 5.1

function Get-VhciPluginAndCdpJob {
    <#
    .Synopsis
        Collects plugin backup jobs, CDP policies, and VCD replica jobs.
        Exports _pluginjobs.csv, _cdpjobs.csv, _vcdjobs.csv.
        Source: Get-VBRConfig.ps1 lines 1309-1349.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting plugin, CDP, and VCD jobs..."
    Write-LogFile $message

    $piJob  = Get-VBRPluginJob
    Write-LogFile "Found $(@($piJob).Count) plugin jobs"
    $cdpJob = Get-VBRCDPPolicy
    Write-LogFile "Found $(@($cdpJob).Count) CDP policies"
    $vcdJob = Get-VBRvCDReplicaJob
    Write-LogFile "Found $(@($vcdJob).Count) VCD replica jobs"

    $piJob | Export-VhciCsv -FileName '_pluginjobs.csv'

    $vcdJob | Add-Member -MemberType NoteProperty -Name JobType -Value "VCD Replica" -ErrorAction SilentlyContinue
    $vcdJob | Export-VhciCsv -FileName '_vcdjobs.csv'

    $cdpJob | Add-Member -MemberType NoteProperty -Name JobType -Value "CDP Policy" -ErrorAction SilentlyContinue
    $cdpJob | Export-VhciCsv -FileName '_cdpjobs.csv'

    Write-LogFile ($message + "DONE")
}
