#Requires -Version 5.1

function Get-VhciCdpProxy {
    <#
    .Synopsis
        Collects CDP proxy data and exports to _CdpProxy.csv.
        Returns an array of role-entry descriptors (one per proxy) for merging by the caller.
        Source: Get-VBRConfig.ps1 lines 555-602.
    .Parameter CDPProxies
        Array of CDP proxy objects returned by Get-VBRCDPProxy.
    .Parameter VServers
        Array of VBR server objects (from Get-VBRServer) for hardware info lookup.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [object[]] $CDPProxies = @(),
        [Parameter(Mandatory)] [object[]] $VServers
    )

    $message = "Calculating CDP Proxy Data..."
    Write-LogFile $message

    $roleEntries = [System.Collections.Generic.List[PSCustomObject]]::new()

    try {
        $CDPProxyData = [System.Collections.Generic.List[PSCustomObject]]::new()

        foreach ($CDPProxy in $CDPProxies) {
            $CDPServer     = $VServers | Where-Object { $_.Id -eq $CDPProxy.ServerId }
            $hw            = Get-VhciHostHardware $CDPServer
            $CDPProxyCores = $hw.Cores
            $CDPProxyRAM   = $hw.RAM

            $CDPProxyDetails = [pscustomobject][ordered]@{
                ServerId                 = $CDPProxy.ServerId
                CacheSize                = $CDPProxy.CacheSize
                CachePath                = $CDPProxy.CachePath
                IsEnabled                = $CDPProxy.IsEnabled
                SourceProxyTrafficPort   = $CDPProxy.SourceProxyTrafficPort
                TargetProxyTrafficPort   = $CDPProxy.TargetProxyTrafficPort
                Id                       = $CDPProxy.Id
                Name                     = $CDPProxy.Name
                Description              = $CDPProxy.Description
            }
            $CDPProxyData.Add($CDPProxyDetails)

            $roleEntries.Add([PSCustomObject]@{
                HostName     = $CDPServer.Name
                RoleName     = 'CDPProxy'
                EntryName    = $CDPProxy.Name
                TaskCount    = 1
                TaskCountKey = 'TotalCDPProxyTasks'
                Cores        = $CDPProxyCores
                RAM          = $CDPProxyRAM
            })
        }

        Write-LogFile ($message + "DONE")
        $CDPProxyData | Export-VhciCsv -FileName '_CdpProxy.csv'
        return $roleEntries.ToArray()
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        return @()
    }
}
