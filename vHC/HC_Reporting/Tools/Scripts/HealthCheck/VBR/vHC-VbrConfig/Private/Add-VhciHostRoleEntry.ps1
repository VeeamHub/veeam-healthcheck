#Requires -Version 5.1

function Add-VhciHostRoleEntry {
    <#
    .Synopsis
        Upserts a server entry in the shared HostRoles hashtable and increments the
        role-specific and aggregate task counters. See ADR 0008.
    .Parameter HostRoles
        Shared hashtable keyed by server name. Modified in-place.
    .Parameter HostName
        Server hostname used as the hashtable key.
    .Parameter RoleName
        Role label (e.g. 'Proxy', 'GPProxy', 'CDPProxy', 'Repository', 'Gateway').
    .Parameter EntryName
        Display name for this specific entry (proxy name, repo name, etc.).
    .Parameter TaskCount
        Number of concurrent tasks this entry contributes.
    .Parameter TaskCountKey
        Name of the role-specific task counter key in the entry hashtable
        (e.g. 'TotalVpProxyTasks', 'TotalGPProxyTasks'). See ADR 0008 for the
        full mapping table.
    .Parameter Cores
        Physical CPU core count. Used only when creating a new entry; ignored
        for existing entries (hardware belongs to the host, not the role).
    .Parameter RAM
        Physical RAM in GB. Used only when creating a new entry.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [hashtable] $HostRoles,
        [Parameter(Mandatory)] [string]    $HostName,
        [Parameter(Mandatory)] [string]    $RoleName,
        [Parameter(Mandatory)] [string]    $EntryName,
        [Parameter(Mandatory)] [int]       $TaskCount,
        [Parameter(Mandatory)] [string]    $TaskCountKey,
        [Parameter(Mandatory = $false)] [int] $Cores = 0,
        [Parameter(Mandatory = $false)] [int] $RAM   = 0
    )

    if (-not $HostRoles.ContainsKey($HostName)) {
        $HostRoles[$HostName] = [ordered]@{
            Roles      = @($RoleName)
            Names      = @($EntryName)
            TotalTasks = 0
            Cores      = $Cores
            RAM        = $RAM
        }
        $HostRoles[$HostName][$TaskCountKey] = 0
    } else {
        $HostRoles[$HostName].Roles += $RoleName
        $HostRoles[$HostName].Names += $EntryName
        # Ensure the role-specific counter exists for hosts gaining a new role
        if (-not $HostRoles[$HostName].Contains($TaskCountKey)) {
            $HostRoles[$HostName][$TaskCountKey] = 0
        }
    }

    $HostRoles[$HostName][$TaskCountKey] += $TaskCount
    $HostRoles[$HostName].TotalTasks     += $TaskCount
}
