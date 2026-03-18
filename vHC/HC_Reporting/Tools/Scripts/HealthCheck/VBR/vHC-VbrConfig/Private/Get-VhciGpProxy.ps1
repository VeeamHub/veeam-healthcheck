#Requires -Version 5.1

function Get-VhciGpProxy {
    <#
    .Synopsis
        Collects General Purpose (NAS) proxy data and exports to _NasProxy.csv.
        Returns an array of role-entry descriptors (one per proxy) for merging by the caller.
        Source: Get-VBRConfig.ps1 lines 447-490.
    .Parameter GPProxies
        Array of GP/NAS proxy objects returned by Get-VBRNASProxyServer.
    .Parameter VServers
        Array of VBR server objects (from Get-VBRServer) for hardware info lookup.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [object[]] $GPProxies = @(),
        [Parameter(Mandatory)] [object[]] $VServers
    )

    $message = "Calculating GP Proxy Data..."
    Write-LogFile $message

    $roleEntries = [System.Collections.Generic.List[PSCustomObject]]::new()

    try {
        $GPProxyData = [System.Collections.Generic.List[PSCustomObject]]::new()

        foreach ($GPProxy in $GPProxies) {
            $NrofGPProxyTasks = $GPProxy.ConcurrentTaskNumber
            $hw           = Get-VhciHostHardware ($VServers | Where-Object { $_.Name -eq $GPProxy.Server.Name })
            $GPProxyCores = $hw.Cores
            $GPProxyRAM   = $hw.RAM

            $GPProxyDetails = [pscustomobject][ordered]@{
                ConcurrentTaskNumber = $NrofGPProxyTasks
                Host                 = $GPProxy.Server.Name
                HostId               = $GPProxy.Server.Id
            }
            $GPProxyData.Add($GPProxyDetails)

            $roleEntries.Add([PSCustomObject]@{
                HostName     = $GPProxy.Server.Name
                RoleName     = 'GPProxy'
                EntryName    = $GPProxy.Server.Name
                TaskCount    = $NrofGPProxyTasks
                TaskCountKey = 'TotalGPProxyTasks'
                Cores        = $GPProxyCores
                RAM          = $GPProxyRAM
            })
        }

        Write-LogFile ($message + "DONE")
        $GPProxyData | Export-VhciCsv -FileName '_NasProxy.csv'
        return $roleEntries.ToArray()
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        return @()
    }
}
