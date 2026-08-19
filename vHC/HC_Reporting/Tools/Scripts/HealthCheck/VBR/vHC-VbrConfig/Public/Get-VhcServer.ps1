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
            @{ name = 'OSInfo';   expression = {
                # $_.Info.Info holds a good caption for hypervisor-managed hosts (e.g. ESXi:
                # "VMware ESXi 8.0.2 build-23305546") but is unpopulated for the backup
                # server's own host record. Only fall back to GetPhysicalHost() when the
                # primary source is empty, since GetPhysicalHost().OsType reports "Other"
                # for host types like ESXi where Info.Info is the better source.
                try {
                    if ($_.Info -and $_.Info.Info) {
                        return $_.Info.Info
                    }

                    $physHost = @($_.GetPhysicalHost())[0]
                    if (-not $physHost) { return '' }

                    $unixInfo = $null
                    $unixProp = $physHost.PSObject.Properties['UnixBasedOsInfo']
                    if ($unixProp) { $unixInfo = $unixProp.Value }

                    # VBR reports Type='Unknown'/DistribVersion='0.0' as a sentinel for
                    # non-OS-bearing entries (e.g. ExternalInfrastructureServer); treat
                    # that as no data rather than surfacing a fabricated OS caption.
                    if ($unixInfo -and $unixInfo.Type -and $unixInfo.Type -ne 'Unknown') {
                        "$($unixInfo.Type) $($unixInfo.DistribVersion)".Trim()
                    } elseif ($null -ne $physHost.OsType -and $physHost.OsType.ToString() -notin @('Other', 'Unknown')) {
                        $physHost.OsType.ToString()
                    } else {
                        ''
                    }
                } catch {
                    ''
                }
            } },
            @{ name = 'Platform'; expression = {
                $key = if ($_.Name) { $_.Name.ToLowerInvariant() } else { '' }
                if ($script:PlatformMap -and $script:PlatformMap.ContainsKey($key)) {
                    $script:PlatformMap[$key]
                } else { '' }
            }}

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
