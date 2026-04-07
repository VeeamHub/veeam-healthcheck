#Requires -Version 5.1

function Invoke-VhciJobSubCollectors {
    <#
    .Synopsis
        Runs the nine private job-type sub-collectors with individual fault isolation.
        A single sub-collector failure does not abort the remaining ones.
        Called exclusively by Get-VhcJob. See ADR 0007.
    .Parameter Jobs
        Array of VBR job objects (from Get-VBRJob). Passed to Get-VhciReplication.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [object[]] $Jobs = @()
    )

    Write-LogFile "Running job sub-collectors..."

    try { Get-VhciCatalystJob } catch {
        Write-LogFile "Get-VhciCatalystJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciAgentJob } catch {
        Write-LogFile "Get-VhciAgentJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciSureBackup } catch {
        Write-LogFile "Get-VhciSureBackup failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciTapeInfrastructure } catch {
        Write-LogFile "Get-VhciTapeInfrastructure failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciNasJob } catch {
        Write-LogFile "Get-VhciNasJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciPluginAndCdpJob } catch {
        Write-LogFile "Get-VhciPluginAndCdpJob failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciReplication -Jobs @($Jobs) } catch {
        Write-LogFile "Get-VhciReplication failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciCloudConnect } catch {
        Write-LogFile "Get-VhciCloudConnect failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    try { Get-VhciCredentialsAndNotifications } catch {
        Write-LogFile "Get-VhciCredentialsAndNotifications failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    Write-LogFile "Job sub-collectors complete."
}
