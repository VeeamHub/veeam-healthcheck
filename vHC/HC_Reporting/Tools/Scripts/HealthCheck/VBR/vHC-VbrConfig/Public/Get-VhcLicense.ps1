#Requires -Version 5.1

function Get-VhcLicense {
    <#
    .Synopsis
        Collects VBR installed licence details including socket, instance, and capacity
        licence summaries. Exports to _LicInfo.csv.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting License info..."
    $lic     = $null
    Write-LogFile $message

    try {
        $lic = Get-VBRInstalledLicense
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'License' -ErrorMessage $_.Exception.Message
    }

    if ($null -ne $lic) {
        [pscustomobject][ordered]@{
            LicensedTo                           = $lic.LicensedTo
            Edition                              = $lic.Edition
            ExpirationDate                       = $lic.ExpirationDate
            Type                                 = $lic.Type
            SupportId                            = $lic.SupportId
            SupportExpirationDate                = $lic.SupportExpirationDate
            AutoUpdateEnabled                    = $lic.AutoUpdateEnabled
            FreeAgentInstanceConsumptionEnabled  = $lic.FreeAgentInstanceConsumptionEnabled
            CloudConnect                         = $lic.CloudConnect
            LicensedSockets                      = $lic.SocketLicenseSummary.LicensedSocketsNumber
            UsedSockets                          = $lic.SocketLicenseSummary.UsedSocketsNumber
            RemainingSockets                     = $lic.SocketLicenseSummary.RemainingSocketsNumber
            LicensedInstances                    = $lic.InstanceLicenseSummary.LicensedInstancesNumber
            UsedInstances                        = $lic.InstanceLicenseSummary.UsedInstancesNumber
            NewInstances                         = $lic.InstanceLicenseSummary.NewInstancesNumber
            RentalInstances                      = $lic.InstanceLicenseSummary.RentalInstancesNumber
            LicensedCapacityTB                   = $lic.CapacityLicenseSummary.LicensedCapacityTb
            UsedCapacityTb                       = $lic.CapacityLicenseSummary.UsedCapacityTb
            Status                               = $lic.Status
        } | Export-VhciCsv -FileName '_LicInfo.csv'
    } else {
        $null | Export-VhciCsv -FileName '_LicInfo.csv'
    }
}
