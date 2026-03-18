#Requires -Version 5.1

function Get-VhcServer {
    <#
    .Synopsis
        Collects VBR server inventory and exports to _Servers.csv.
        Returns the raw server objects array for use by downstream concurrency collectors.
    .Outputs
        [object[]] Raw VBR server objects (as returned by Get-VBRServer).
        Required by Get-VhcConcurrencyData. Returns $null on failure.
    #>
    [CmdletBinding()]
    [OutputType([object[]])]
    param()

    $message = "Collecting server info..."
    Write-LogFile $message

    try {
        $VServers = Get-VBRServer
        $Servers  = $VServers | Select-Object -Property `
            "Info", "ParentId", "Id", "Uid", "Name", "Reference",
            "Description", "IsUnavailable", "Type", "ApiVersion",
            "PhysHostId", "ProxyServicesCreds",
            @{ name = 'Cores';    expression = { $_.GetPhysicalHost().hardwareinfo.CoresCount       } },
            @{ name = 'CPUCount'; expression = { $_.GetPhysicalHost().hardwareinfo.CPUCount          } },
            @{ name = 'RAM';      expression = { $_.GetPhysicalHost().hardwareinfo.PhysicalRamTotal  } },
            @{ name = 'OSInfo';   expression = { $_.Info.Info                                        } }

        Write-LogFile ($message + "DONE")
        $Servers | Export-VhciCsv -FileName '_Servers.csv'

        return $VServers
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Server' -ErrorMessage $_.Exception.Message
        return $null
    }
}
