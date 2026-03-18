#Requires -Version 5.1

function Get-VhciServerOsOverhead {
    <#
    .Synopsis
        Computes the OS/service-layer CPU and RAM overhead for a server entry by taking
        the max of the per-role overhead values for each role that has active tasks.
        CPU and RAM are maxed independently. Returns @{ CPU = [int]; RAM = [int] }.

        These values represent the minimum service-layer floor for a server of each
        role type (per Veeam sizing guide). A multi-role server runs one OS instance,
        so the floor is the largest single-role requirement, not a cumulative sum.
        See ADR 0010.
    .Parameter Entry
        The value portion of a HostRoles hashtable entry (not the key).
    .Parameter Thresholds
        The Thresholds object from VbrConfig.json (Config.Thresholds).
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] $Entry,
        [Parameter(Mandatory)] $Thresholds
    )

    $cpu = 0
    $ram = 0

    if ((SafeValue $Entry.TotalRepoTasks) -gt 0 -or (SafeValue $Entry.TotalGWTasks) -gt 0) {
        $cpu = [Math]::Max($cpu, $Thresholds.RepoOSCPU)
        $ram = [Math]::Max($ram, $Thresholds.RepoOSRAM)
    }
    if ((SafeValue $Entry.TotalVpProxyTasks) -gt 0) {
        $cpu = [Math]::Max($cpu, $Thresholds.VpProxyOSCPU)
        $ram = [Math]::Max($ram, $Thresholds.VpProxyOSRAM)
    }
    if ((SafeValue $Entry.TotalGPProxyTasks) -gt 0) {
        $cpu = [Math]::Max($cpu, $Thresholds.GpProxyOSCPU)
        $ram = [Math]::Max($ram, $Thresholds.GpProxyOSRAM)
    }
    if ((SafeValue $Entry.TotalCDPProxyTasks) -gt 0) {
        $cpu = [Math]::Max($cpu, $Thresholds.CdpProxyOSCPU)
        $ram = [Math]::Max($ram, $Thresholds.CdpProxyOSRAM)
    }

    return @{ CPU = $cpu; RAM = $ram }
}
