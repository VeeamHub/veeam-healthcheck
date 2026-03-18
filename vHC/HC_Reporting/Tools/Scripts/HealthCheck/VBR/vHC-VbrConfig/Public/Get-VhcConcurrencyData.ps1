#Requires -Version 5.1

function Get-VhcConcurrencyData {
    <#
    .Synopsis
        Collects all concurrency-related infrastructure data: GP proxies, VMware/HV proxies,
        CDP proxies, and repository/gateway servers. Each sub-collector returns role-entry
        descriptors; this function merges them into the host-role map and appends BackupServer
        and SQL Server roles.
        Source: Get-VBRConfig.ps1 lines 374-717.
    .Parameter VServers
        Array of VBR server objects returned by Get-VhcServer. Required for hardware info lookup.
    .Parameter Config
        Parsed VbrConfig.json object. Provides threshold values.
    .Parameter VBRServer
        VBR server hostname (string). Used to identify the backup server role and resolve
        "localhost" in Get-SqlSName.
    .Parameter VBRVersion
        Major VBR version integer. Reserved for future per-version branching in sub-collectors.
    .Outputs
        [hashtable] $hostRoles - keyed by server name, each entry contains Roles, Names,
        TotalTasks, Cores, RAM, and per-role task counters. Required by Invoke-VhcConcurrencyAnalysis.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param (
        [Parameter(Mandatory)] [object[]]     $VServers,
        [Parameter(Mandatory)] [PSCustomObject] $Config,
        [Parameter(Mandatory)] [string]       $VBRServer,
        [Parameter(Mandatory)] [int]          $VBRVersion
    )

    Write-LogFile "Collecting component info for concurrency inspection..."

    # ---------------------------------------------------------------------------
    # Fetch all VBR proxy/repository objects
    # ---------------------------------------------------------------------------
    $message = "Collecting Proxy and Repository info..."
    Write-LogFile $message
    try {
        $VMwareProxies    = @(Get-VBRViProxy)
        $HyperVProxies    = @(Get-VBRHvProxy)
        $CDPProxies       = @(Get-VBRCDPProxy)
        $VBRRepositories  = @(Get-VBRBackupRepository)
        $GPProxies        = @(Get-VBRNASProxyServer)
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        return $null
    }

    # ---------------------------------------------------------------------------
    # Collect role-entry descriptors from each sub-collector, then merge.
    # Get-VhciGpProxy, Get-VhciViHvProxy, Get-VhciCdpProxy catch internally
    # and return @() on failure. Get-VhciRepoGateway has no try/catch and
    # will propagate exceptions; this is intentional (pre-existing behaviour).
    # ---------------------------------------------------------------------------
    $gpEntries   = @(Get-VhciGpProxy    -GPProxies     $GPProxies       -VServers $VServers)
    $viHvEntries = @(Get-VhciViHvProxy  -VMwareProxies $VMwareProxies `
                                        -HyperVProxies $HyperVProxies `
                                        -VServers      $VServers)
    $cdpEntries  = @(Get-VhciCdpProxy   -CDPProxies    $CDPProxies      -VServers $VServers)
    $repoEntries = @(Get-VhciRepoGateway -Repositories $VBRRepositories -VServers $VServers)

    $hostRoles = @{}
    $allEntries = (@($gpEntries) + @($viHvEntries) + @($cdpEntries) + @($repoEntries)) |
        Where-Object { $null -ne $_ }
    foreach ($entry in $allEntries) {
        Add-VhciHostRoleEntry -HostRoles $hostRoles `
            -HostName     $entry.HostName `
            -RoleName     $entry.RoleName `
            -EntryName    $entry.EntryName `
            -TaskCount    $entry.TaskCount `
            -TaskCountKey $entry.TaskCountKey `
            -Cores        $entry.Cores `
            -RAM          $entry.RAM
    }

    # ---------------------------------------------------------------------------
    # Add BackupServer role to the host-role map
    # ---------------------------------------------------------------------------
    try {
        $bsKey = if ($VBRServer -eq 'localhost') {
            [System.Net.Dns]::GetHostByName($env:COMPUTERNAME).HostName
        } else {
            $VBRServer
        }

        # Only append to an existing entry - do not create a standalone BackupServer row.
        # If the backup server is not co-located with any proxy/repo, it is logged only
        # (matches original source behaviour).
        if ($hostRoles.ContainsKey($bsKey)) {
            $hostRoles[$bsKey].Roles += 'BackupServer'
            Write-LogFile "Backup Server role added for host: $bsKey"
        } else {
            Write-LogFile "Backup Server '$bsKey' is not used in another role."
        }
    } catch {
        Write-LogFile "Backup Server '$VBRServer' is not used in another role."
    }

    # ---------------------------------------------------------------------------
    # Add SQL Server role (skipped for Linux backup servers - no local SQL)
    # ---------------------------------------------------------------------------
    $backupServerEntry = $VServers | Where-Object { $_.Name -eq $VBRServer }
    $backupServerType  = if ($backupServerEntry) { $backupServerEntry.Type } else { '' }

    if ($backupServerType -ne 'Linux') {
        $SQLServer = Get-SqlSName -VBRServer $VBRServer

        if ($SQLServer) {
            # Only append to an existing entry - do not create a standalone SQL row.
            # SQL is "skipped - printed only" when not co-located with a backup component
            # (matches original source behaviour).
            if ($hostRoles.ContainsKey($SQLServer)) {
                $hostRoles[$SQLServer].Roles += 'SQLServer'
                Write-LogFile "SQL Server role added for host: $SQLServer"
            } else {
                Write-LogFile "SQL Server host: $SQLServer"
            }
        }
    }

    # ---------------------------------------------------------------------------
    # Log component summary and multi-role servers
    # ---------------------------------------------------------------------------
    Write-LogFile "Found components:"
    Write-LogFile "$((($hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles -contains 'Repository' }).Count)) Repositories"
    Write-LogFile "$((($hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles -contains 'Gateway'    }).Count)) Gateway Servers"
    Write-LogFile "$((($hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles -contains 'Proxy'      }).Count)) Vi and HV Proxy Servers"
    Write-LogFile "$((($hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles -contains 'CDPProxy'   }).Count)) CDP Proxies"
    Write-LogFile "$((($hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles -contains 'GPProxy'    }).Count)) GP Proxies"

    $multiRoleServers = $hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles.Count -gt 1 }
    if ($multiRoleServers) {
        $multiRoleServers | ForEach-Object {
            Write-LogFile "$($_.Key) has roles: $($_.Value.Roles -join '/ ') - Names: $($_.Value.Names -join '/ ')"
        }
    } else {
        Write-LogFile "No servers are being used for multiple roles."
    }

    return $hostRoles
}
