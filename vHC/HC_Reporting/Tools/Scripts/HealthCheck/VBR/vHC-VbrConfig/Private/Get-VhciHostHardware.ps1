#Requires -Version 5.1

function Get-VhciHostHardware {
    <#
    .Synopsis
        Returns CPU core count and RAM in GB for a VBR server object.
        Returns @{Cores=0; RAM=0} if the server is null or if GetPhysicalHost()
        throws (e.g. offline/unreachable host). Logs a WARNING in that case.
    .Parameter Server
        A VBR server object (e.g. from Get-VBRServer). May be $null.
    .Outputs
        [hashtable] @{ Cores = [int]; RAM = [int] }
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] $Server
    )

    if ($null -eq $Server) {
        Write-LogFile "Server lookup returned null - defaulting to 0 cores / 0 RAM." -LogLevel "WARNING"
        return @{ Cores = 0; RAM = 0 }
    }

    try {
        $physHost = $Server.GetPhysicalHost()
        return @{
            Cores = $physHost.HardwareInfo.CoresCount
            RAM   = ConvertTo-GB($physHost.HardwareInfo.PhysicalRAMTotal)
        }
    } catch {
        Write-LogFile "Hardware info unavailable for '$($Server.Name)' - defaulting to 0 cores / 0 RAM." -LogLevel "WARNING"
        return @{ Cores = 0; RAM = 0 }
    }
}
