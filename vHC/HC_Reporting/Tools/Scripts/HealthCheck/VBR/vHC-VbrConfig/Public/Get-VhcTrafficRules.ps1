#Requires -Version 5.1

function Get-VhcTrafficRules {
    <#
    .Synopsis
        Collects VBR network traffic rules and exports to _trafficRules.csv.
    #>
    [CmdletBinding()]
    param()

    $message      = "Collecting traffic info..."
    $trafficRules = $null
    Write-LogFile $message

    try {
        $trafficRules = Get-VBRNetworkTrafficRule
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'TrafficRules' -ErrorMessage $_.Exception.Message
    }

    $trafficRules | Export-VhciCsv -FileName '_trafficRules.csv'
}
