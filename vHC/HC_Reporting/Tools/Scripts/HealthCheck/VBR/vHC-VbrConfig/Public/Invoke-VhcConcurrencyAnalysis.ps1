#Requires -Version 5.1

function Invoke-VhcConcurrencyAnalysis {
    <#
    .Synopsis
        Calculates per-server CPU/RAM requirements and suggested task counts based on the
        aggregated host-role map produced by Get-VhcConcurrencyData. Exports the results to
        _AllServersRequirementsComparison.csv.
        Source: Get-VBRConfig.ps1 lines 719-823.

    .Notes
        Intentional fixes applied vs source:
        1. ($serverName -contains $BackupServerName) changed to (-eq).
           PowerShell -contains on a scalar string behaves identically to -eq, so no output change.
        2. $CDPProxyOSCPUReq / $CDPProxyOSRAMReq were undefined in the source (bug).
           Now read from $Config.Thresholds.CdpProxyOSCPU / CdpProxyOSRAM (both default to 0).
           CDPProxy OS overhead is now correctly included in $RequiredCores / $RequiredRAM,
           but the default value of 0 means no change in practice.
        3. BackupServer CPU/RAM requirement applied via Max(overhead, BS requirement) rather than
           additively - eliminates OS double-count on backup server rows (ADR 0010 extension).
        4. Cores-to-tasks multiplier is now role-aware: 1/CPUPerTask per active role type, taking
           the minimum across all active roles (most conservative wins). Fixes GP Proxy (2 cores/task)
           and CDP Proxy (4 cores/task) over-reporting suggested tasks (ADR 0011).

    .Parameter HostRoles
        Hashtable returned by Get-VhcConcurrencyData.
    .Parameter Config
        Parsed VbrConfig.json object. Provides all threshold values.
    .Parameter VBRVersion
        Major VBR version integer. Determines which BackupServer CPU/RAM thresholds to apply.
    .Parameter BackupServerName
        VBR server hostname string. Used to identify the backup server row and add its overhead.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [hashtable]    $HostRoles,
        [Parameter(Mandatory)] [PSCustomObject] $Config,
        [Parameter(Mandatory)] [int]          $VBRVersion,
        [Parameter(Mandatory)] [string]       $BackupServerName
    )

    $message = "Calculating Requirements based on aggregated resources for multi-role servers..."
    Write-LogFile $message

    try {
        $t = $Config.Thresholds

        # Per-task requirements
        $VPProxyRAMReq  = $t.VpProxyRAMPerTask
        $VPProxyCPUReq  = $t.VpProxyCPUPerTask
        $GPProxyRAMReq  = $t.GpProxyRAMPerTask
        $GPProxyCPUReq  = $t.GpProxyCPUPerTask
        $RepoGWRAMReq   = $t.RepoGwRAMPerTask
        $RepoGWCPUReq   = $t.RepoGwCPUPerTask
        $CDPProxyRAMReq = $t.CdpProxyRAM
        $CDPProxyCPUReq = $t.CdpProxyCPU

        # Backup Server thresholds vary by version
        $BSCPUReq = if ($VBRVersion -ge 13) { $t.BackupServerCPU_v13 } else { $t.BackupServerCPU_v12 }
        $BSRAMReq = if ($VBRVersion -ge 13) { $t.BackupServerRAM_v13 } else { $t.BackupServerRAM_v12 }

        $RequirementsComparison = [System.Collections.Generic.List[PSCustomObject]]::new()

        foreach ($server in $HostRoles.GetEnumerator()) {
            $SuggestedTasksByCores = 0
            $SuggestedTasksByRAM   = 0
            $serverName            = $server.Key

            $overhead = Get-VhciServerOsOverhead -Entry $server.Value -Thresholds $t

            # BackupServer requirement is a total figure (includes OS). Take max rather than
            # adding additively on top of the role OS floor - same max-of-roles principle as ADR 0010.
            $fixedCPU = $overhead.CPU
            $fixedRAM = $overhead.RAM
            if ($serverName -eq $BackupServerName) {
                $fixedCPU = [Math]::Max($fixedCPU, $BSCPUReq)
                $fixedRAM = [Math]::Max($fixedRAM, $BSRAMReq)
            }

            $RequiredCores = [Math]::Ceiling(
                (SafeValue $server.Value.TotalRepoTasks)     * $RepoGWCPUReq   +
                (SafeValue $server.Value.TotalGWTasks)       * $RepoGWCPUReq   +
                (SafeValue $server.Value.TotalVpProxyTasks)  * $VPProxyCPUReq  +
                (SafeValue $server.Value.TotalGPProxyTasks)  * $GPProxyCPUReq  +
                (SafeValue $server.Value.TotalCDPProxyTasks) * $CDPProxyCPUReq +
                $fixedCPU
            )

            $RequiredRAM = [Math]::Ceiling(
                (SafeValue $server.Value.TotalRepoTasks)     * $RepoGWRAMReq   +
                (SafeValue $server.Value.TotalGWTasks)       * $RepoGWRAMReq   +
                (SafeValue $server.Value.TotalVpProxyTasks)  * $VPProxyRAMReq  +
                (SafeValue $server.Value.TotalGPProxyTasks)  * $GPProxyRAMReq  +
                (SafeValue $server.Value.TotalCDPProxyTasks) * $CDPProxyRAMReq +
                $fixedRAM
            )

            $coresAvailable = $server.Value.Cores
            $ramAvailable   = $server.Value.RAM
            $totalTasks     = $server.Value.TotalTasks

            $SuggestedTasksByCores = [Math]::Floor((SafeValue $coresAvailable) - $fixedCPU)

            $SuggestedTasksByRAM = [Math]::Floor((SafeValue $ramAvailable) - $fixedRAM)

            # Use the most conservative (highest cores-per-task) ratio among active roles.
            # Default covers VP Proxy and Repo/GW (both 0.5 cores/task -> 2 tasks/core).
            $tasksPerCore = 1.0 / $VPProxyCPUReq
            if ((SafeValue $server.Value.TotalGPProxyTasks) -gt 0) {
                $tasksPerCore = [Math]::Min($tasksPerCore, 1.0 / $GPProxyCPUReq)   # 2 cores/task -> 0.5
            }
            if ((SafeValue $server.Value.TotalCDPProxyTasks) -gt 0) {
                $tasksPerCore = [Math]::Min($tasksPerCore, 1.0 / $CDPProxyCPUReq)  # 4 cores/task -> 0.25
            }
            $NonNegativeCores = EnsureNonNegative($SuggestedTasksByCores * $tasksPerCore)
            $NonNegativeRAM   = EnsureNonNegative($SuggestedTasksByRAM)
            $MaxSuggestedTasks = [Math]::Min($NonNegativeCores, $NonNegativeRAM)

            $RequirementComparison = [pscustomobject][ordered]@{
                'Server'             = $serverName
                'Type'               = ($server.Value.Roles -join '/ ')
                'Required Cores'     = $RequiredCores
                'Available Cores'    = $coresAvailable
                'Required RAM (GB)'  = $RequiredRAM
                'Available RAM (GB)' = $ramAvailable
                'Concurrent Tasks'   = $totalTasks
                'Suggested Tasks'    = $MaxSuggestedTasks
                'Names'              = ($server.Value.Names -join '/ ')
            }
            $RequirementsComparison.Add($RequirementComparison)
        }

        Write-LogFile ($message + "DONE")
        $RequirementsComparison | Export-VhciCsv -FileName '_AllServersRequirementsComparison.csv'
        Write-LogFile "Concurrency inspection files are exported."
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
    }
}
