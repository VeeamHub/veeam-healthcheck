#Requires -Version 5.1

function Get-VhciCloudConnect {
    <#
    .Synopsis
        Collects Cloud Connect gateway and tenant data.
        Exports _CloudGateways.csv, _CloudTenants.csv.
        Source: Get-VBRConfig.ps1 lines 1411-1433.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting Cloud Connect data..."
    Write-LogFile $message

    # Pre-flight: Cloud Connect gateway/tenant cmdlets require the SP licence.
    # Get-VBRInstalledLicense.CloudConnect = 'Disabled' on standard installations.
    $lic = $null
    try { $lic = Get-VBRInstalledLicense } catch {}
    if ($null -eq $lic -or $lic.CloudConnect -eq 'Disabled') {
        Write-LogFile "Cloud Connect not licensed (CloudConnect=$($lic.CloudConnect)) - skipping." -LogLevel "INFO"
        return
    }

    $cloudGateways = Get-VBRCloudGateway
    Write-LogFile "Found $(@($cloudGateways).Count) cloud gateways"
    $cloudTenants = Get-VBRCloudTenant
    Write-LogFile "Found $(@($cloudTenants).Count) cloud tenants"

    $cloudGateways | Export-VhciCsv -FileName '_CloudGateways.csv'
    $cloudTenants  | Export-VhciCsv -FileName '_CloudTenants.csv'

    Write-LogFile ($message + "DONE")
}
