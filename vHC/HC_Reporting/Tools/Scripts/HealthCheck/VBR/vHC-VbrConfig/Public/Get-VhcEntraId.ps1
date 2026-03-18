#Requires -Version 5.1

function Get-VhcEntraId {
    <#
    .Synopsis
        Collects Entra ID (Azure AD) tenant, log backup job, and tenant backup job data.
        Exports _entraTenants.csv, _entraLogJob.csv, _entraTenantJob.csv.
        Wrapped in a single try/catch because Entra ID cmdlets throw on environments
        without an Entra tenant configured.
    #>
    [CmdletBinding()]
    param()

    $message          = "Collecting Entra ID info..."
    $entraIDTenant    = $null
    $entraIdLogJobs   = $null
    $entraIdTenantJobs = $null
    Write-LogFile $message

    try {
        $entraTenant = Get-VBREntraIDTenant

        $entraIDTenant = [pscustomobject][ordered]@{
            tenantName    = $entraTenant.Name
            CacheRepoName = $entraTenant.cacherepository.name
        }

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

        $eIdTenantBackup = Get-VBREntraIDTenantBackupJob
        $entraIdTenantJobs = [pscustomobject][ordered]@{
            Name            = $eIdTenantBackup.Tenant.Name
            RetentionPolicy = $eIdTenantBackup.RetentionPolicy
        }

        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile "Error on Entra ID collection. $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'EntraId' -ErrorMessage $_.Exception.Message
    }

    $entraIdLogJobs  | Export-VhciCsv -FileName '_entraLogJob.csv'
    $entraIDTenant   | Export-VhciCsv -FileName '_entraTenants.csv'
    $entraIdTenantJobs | Export-VhciCsv -FileName '_entraTenantJob.csv'
}
