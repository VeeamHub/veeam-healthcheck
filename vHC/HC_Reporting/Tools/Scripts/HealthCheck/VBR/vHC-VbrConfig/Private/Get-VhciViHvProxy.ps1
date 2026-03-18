#Requires -Version 5.1

function Get-VhciViHvProxy {
    <#
    .Synopsis
        Collects VMware and Hyper-V proxy data and exports to _Proxies.csv and _HvProxy.csv.
        Returns an array of role-entry descriptors (one per proxy) for merging by the caller.
        Source: Get-VBRConfig.ps1 lines 492-553.
    .Parameter VMwareProxies
        Array of VMware proxy objects returned by Get-VBRViProxy.
    .Parameter HyperVProxies
        Array of Hyper-V proxy objects returned by Get-VBRHvProxy.
    .Parameter VServers
        Array of VBR server objects (from Get-VBRServer) for hardware info fallback.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [object[]] $VMwareProxies = @(),
        [Parameter(Mandatory = $false)] [object[]] $HyperVProxies = @(),
        [Parameter(Mandatory)] [object[]] $VServers
    )

    $message = "Calculating Vi and HV Proxy Data..."
    Write-LogFile $message

    $roleEntries = [System.Collections.Generic.List[PSCustomObject]]::new()

    try {
        $VPProxies  = $VMwareProxies + $HyperVProxies
        $ProxyData  = [System.Collections.Generic.List[PSCustomObject]]::new()

        foreach ($Proxy in $VPProxies) {
            $NrofProxyTasks = $Proxy.MaxTasksCount

            try {
                $ProxyCores = $Proxy.GetPhysicalHost().HardwareInfo.CoresCount
                $ProxyRAM   = ConvertTo-GB($Proxy.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
            } catch {
                $hw         = Get-VhciHostHardware ($VServers | Where-Object { $_.Name -eq $Proxy.Name })
                $ProxyCores = $hw.Cores
                $ProxyRAM   = $hw.RAM
            }

            $proxytype = if ($Proxy.Type -eq 'Vi') { 'VMware' } else { $Proxy.Type }

            $ProxyDetails = [pscustomobject][ordered]@{
                Id               = $Proxy.Id
                Name             = $Proxy.Name
                Description      = $Proxy.Description
                Info             = $Proxy.Info
                HostId           = $Proxy.Host.Id
                Host             = $Proxy.Host.Name
                Type             = $proxytype
                IsDisabled       = $Proxy.IsDisabled
                Options          = $Proxy.Options
                MaxTasksCount    = $NrofProxyTasks
                UseSsl           = if ($Proxy.Type -eq 'Vi') { $Proxy.Options.UseSsl }          else { '' }
                FailoverToNetwork = if ($Proxy.Type -eq 'Vi') { $Proxy.Options.FailoverToNetwork } else { '' }
                TransportMode    = if ($Proxy.Type -eq 'Vi') { $Proxy.Options.TransportMode }   else { '' }
                IsVbrProxy       = ''
                ChosenVm         = if ($Proxy.Type -eq 'Vi') { $Proxy.Options.ChosenVm }        else { '' }
                ChassisType      = $Proxy.ChassisType
            }
            $ProxyData.Add($ProxyDetails)

            $roleEntries.Add([PSCustomObject]@{
                HostName     = $Proxy.Host.Name
                RoleName     = 'Proxy'
                EntryName    = $Proxy.Name
                TaskCount    = $NrofProxyTasks
                TaskCountKey = 'TotalVpProxyTasks'
                Cores        = $ProxyCores
                RAM          = $ProxyRAM
            })
        }

        Write-LogFile ($message + "DONE")

        $ProxyData | Export-VhciCsv -FileName '_Proxies.csv'

        $HyperVProxies | Select-Object `
            Id, Name, Description,
            @{ n = 'HostId'; e = { $_.Host.Id   } },
            @{ n = 'Host';   e = { $_.Host.Name } },
            Type, IsDisabled, Options, MaxTasksCount, Info |
            Export-VhciCsv -FileName '_HvProxy.csv'

        return $roleEntries.ToArray()
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        return @()
    }
}
