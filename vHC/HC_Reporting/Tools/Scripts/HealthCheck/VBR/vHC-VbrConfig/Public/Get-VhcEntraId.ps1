#Requires -Version 5.1

function Get-VhcEntraId {
    <#
    .Synopsis
        Collects Entra ID (Azure AD) tenant, log backup job, and tenant backup job data.
        Exports _entraTenants.csv, _entraLogJob.csv, _entraTenantJob.csv.
        Each Entra cmdlet has its own try/catch: they throw on environments without an
        Entra tenant configured, and the job cmdlets can throw for an unrelated broken job.
    #>
    [CmdletBinding()]
    param()

    $message          = "Collecting Entra ID info..."
    $entraIDTenant    = $null
    $entraIdLogJobs   = $null
    $entraIdTenantJobs = $null
    Write-LogFile $message

    # Each cmdlet gets its own try/catch: the job cmdlets enumerate VBR jobs and
    # can throw for an unrelated broken job (seen on a lab: "Failed to wrap nas
    # backup job [id]: Backup repository 'id' was not found"), which must not
    # discard what the other cmdlets returned. The tenant call stays the gate:
    # when it throws (no Entra support or tenant) the rest are skipped, as before.
    $tenantOk = $true
    try {
        $entraTenant = Get-VBREntraIDTenant

        $entraIDTenant = [pscustomobject][ordered]@{
            tenantName    = $entraTenant.Name
            CacheRepoName = $entraTenant.cacherepository.name
        }
    } catch {
        $tenantOk = $false
        Write-LogFile ($message + "FAILED!")
        Write-LogFile "Error on Entra ID collection. $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'EntraId' -ErrorMessage $_.Exception.Message
    }

    if ($tenantOk) {
        $failed = $false
        try {
            $eIdLogJobs = Get-VBREntraIDLogsBackupJob
            $entraIdLogJobs = [pscustomobject][ordered]@{
                Name                   = $eIdLogJobs.Name
                Tenant                 = $eIdLogJobs.BackupObject.Tenant.Name
                shortTermRetType       = $eIdLogJobs.Name
                ShortTermRepo          = $eIdLogJobs.ShortTermBackupRepository.Name
                ShortTermRepoRetention = $eIdLogJobs.ShortTermRetentionPeriod
                CopyModeEnabled        = $eIdLogJobs.EnableCopyMode
                SecondaryTarget        = $eIdLogJobs.SecondaryTarget
            }
        } catch {
            $failed = $true
            Write-LogFile "Error on Entra ID collection (Get-VBREntraIDLogsBackupJob). $($_.Exception.Message)" -LogLevel "ERROR"
            Add-VhciModuleError -CollectorName 'EntraId' -ErrorMessage $_.Exception.Message
        }

        try {
            $eIdTenantBackup = Get-VBREntraIDTenantBackupJob
            $entraIdTenantJobs = [pscustomobject][ordered]@{
                Name            = $eIdTenantBackup.Tenant.Name
                RetentionPolicy = $eIdTenantBackup.RetentionPolicy
            }
        } catch {
            $failed = $true
            Write-LogFile "Error on Entra ID collection (Get-VBREntraIDTenantBackupJob). $($_.Exception.Message)" -LogLevel "ERROR"
            Add-VhciModuleError -CollectorName 'EntraId' -ErrorMessage $_.Exception.Message
        }

        if ($failed) { Write-LogFile ($message + "PARTIAL") } else { Write-LogFile ($message + "DONE") }
    }

    $entraIdLogJobs  | Export-VhciCsv -FileName '_entraLogJob.csv'
    $entraIDTenant   | Export-VhciCsv -FileName '_entraTenants.csv'
    $entraIdTenantJobs | Export-VhciCsv -FileName '_entraTenantJob.csv'
}
