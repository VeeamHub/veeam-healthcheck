#Requires -Version 5.1

function Get-VhciReplication {
    <#
    .Synopsis
        Collects replica jobs, replica objects, and failover plans.
        Exports _ReplicaJobs.csv, _Replicas.csv, _FailoverPlans.csv.
        Source: Get-VBRConfig.ps1 lines 1350-1385.
    .Parameter Jobs
        Array of VBR job objects already retrieved by the parent Get-VhcJob. Used to filter
        replica jobs without an additional Get-VBRJob call.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [object[]]$Jobs = @()
    )

    $message = "Collecting replication data..."
    Write-LogFile $message

    $replicaJobs = @($Jobs) | Where-Object { $_.JobType -eq "Replica" }
    Write-LogFile "Found $(@($replicaJobs).Count) replica jobs"
    $replicas = Get-VBRReplica
    Write-LogFile "Found $(@($replicas).Count) replicas"
    $failoverPlans = Get-VBRFailoverPlan
    Write-LogFile "Found $(@($failoverPlans).Count) failover plans"

    $replicaJobs  | Export-VhciCsv -FileName '_ReplicaJobs.csv'
    $replicas      | Export-VhciCsv -FileName '_Replicas.csv'
    $failoverPlans | Export-VhciCsv -FileName '_FailoverPlans.csv'

    Write-LogFile ($message + "DONE")
}
