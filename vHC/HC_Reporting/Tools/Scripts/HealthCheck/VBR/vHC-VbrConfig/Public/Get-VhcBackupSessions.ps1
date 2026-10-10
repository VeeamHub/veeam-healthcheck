#Requires -Version 5.1

function Get-VhcBackupSessions {
    <#
    .Synopsis
        Fetches VBR backup sessions created within the reporting window and returns
        them as pipeline output. The caller (orchestrator) captures the output and
        passes it explicitly to Get-VhcSessionReport via the -BackupSessions parameter.

        Returns a mixed array of two session object families:
        - VM and Backup Copy sessions (CBackupSession / CBackupCopySession)
        - Agent / computer backup sessions

        Both families are accepted by Get-VBRTaskSession, which Get-VhcSessionReport
        uses to resolve task-level detail. See ADR 0012 and ADR 0018.

        Path selection is delegated to the private Get-VhciJobSessions helper. On
        VBR versions that ship Veeam.Backup.Core.CBackupSession with the
        GetAllSessionsByPolicyJobAndTimeRange(Guid, DateTime, DateTime) overload,
        the helper uses an indexed DB query per job (the fast path), which
        returns the parent's session AND per-machine child sessions. On older
        versions or when reflection fails, it falls back to a single unfiltered
        cmdlet call followed by a client-side CreationTime filter (the pre-59e2621
        shape that works on v12).

        Agent jobs returned by Get-VBRJob (still surfaced on v13 with a
        deprecation warning) are removed from the VM/BackupCopy list so each
        agent job is only queried via the Agent path.
    .Parameter ReportInterval
        Number of days back to collect sessions for. Matches the -ReportInterval
        parameter passed to Get-VBRConfig.ps1.
    .Parameter JobDiscovery
        Result object from Get-VhcJobDiscovery (issue #224). Its VbrJobs and
        DiscoveredJobs are queried for sessions, so jobs Get-VBRJob does not
        return (e.g. Nutanix AHV on VBR 12.3.x) are no longer skipped.
        Standalone agent jobs in the result are deliberately not used here:
        they are not queried today and are left out to keep this path's
        behaviour unchanged. When omitted or $null, this function calls
        Get-VBRJob itself, as it did before the parameter existed.
    .Outputs
        [object[]] -- mixed array of Veeam backup session objects.
    .NOTES
        Pre-condition: the Veeam PowerShell snap-in (or Veeam.Backup.PowerShell
        module) must be loaded before calling. Get-VBRConfig.ps1 ensures this
        before invoking this function. If the snap-in is missing the function
        degrades to an empty result and logs a WARNING per missing cmdlet.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [int] $ReportInterval,

        [Parameter(Mandatory = $false)]
        [object] $JobDiscovery = $null
    )

    Write-LogFile "Fetching backup sessions for the last $ReportInterval days..."
    $cutoff = (Get-Date).AddDays(-$ReportInterval)

    # -ErrorAction SilentlyContinue does not suppress terminating errors
    # like CommandNotFoundException (raised when the Veeam PS snap-in is
    # not loaded). Wrap in try/catch so a missing dependency degrades to
    # an empty job list instead of throwing out of this function.
    $jobs = @()
    if ($null -ne $JobDiscovery) {
        # The shared discovery step already ran Get-VBRJob plus the tier B/C
        # fallbacks, so Get-VBRJob is not called a second time here.
        $jobs = @(@($JobDiscovery.VbrJobs) + @($JobDiscovery.DiscoveredJobs) | Where-Object { $null -ne $_ })
    } else {
        try {
            $jobs = @(Get-VBRJob -ErrorAction SilentlyContinue)
        } catch {
            Write-LogFile "Get-VBRJob unavailable: $($_.Exception.Message)" -LogLevel 'WARNING'
        }
    }

    $agentJobs = @()
    try {
        $agentJobs = @(Get-VBRComputerBackupJob -ErrorAction SilentlyContinue)
    } catch {
        Write-LogFile "Get-VBRComputerBackupJob unavailable: $($_.Exception.Message)" -LogLevel 'WARNING'
    }

    # Unmanaged/legacy endpoint backup jobs (job type EndpointBackup) come
    # from Get-VBREPJob - separate from both Get-VBRJob and
    # Get-VBRComputerBackupJob. Their sessions only appear in the fast-path
    # query if we know their job_id, so collect them here.
    $epJobs = @()
    try {
        $epJobs = @(Get-VBREPJob -ErrorAction SilentlyContinue)
    } catch {
        Write-LogFile "Get-VBREPJob unavailable: $($_.Exception.Message)" -LogLevel 'WARNING'
    }
    if ($epJobs.Count -gt 0) {
        $agentJobs = @($agentJobs) + @($epJobs)
    }

    # Get-VBRJob still returns agent jobs on v13 (with a deprecation warning).
    # Subtract them so each agent job is only queried via the Agent path -
    # otherwise the fast path runs against the same job_id twice and produces
    # duplicate sessions in the return.
    if ($agentJobs.Count -gt 0 -and $jobs.Count -gt 0) {
        $agentIdSet = @{}
        foreach ($aj in $agentJobs) { $agentIdSet[$aj.Id] = $true }
        $jobs = @($jobs | Where-Object { -not $agentIdSet.ContainsKey($_.Id) })
    }

    $vmSessions = @()
    try {
        $vmSessions = @(Get-VhciJobSessions `
            -Jobs $jobs `
            -Since $cutoff `
            -SlowPathCommand { Get-VBRBackupSession } `
            -PathLabel 'VM/BackupCopy')
    } catch {
        Write-LogFile "VM/BackupCopy session collection failed: $($_.Exception.Message)" -LogLevel 'WARNING'
    }

    $agentSessions = @()
    try {
        $agentSessions = @(Get-VhciJobSessions `
            -Jobs $agentJobs `
            -Since $cutoff `
            -SlowPathCommand { Get-VBRComputerBackupJobSession } `
            -PathLabel 'Agent')
    } catch {
        Write-LogFile "Agent session collection failed: $($_.Exception.Message)" -LogLevel 'WARNING'
    }

    # The fast path returns parent sessions AND per-machine child sessions
    # (e.g. 'Managed-WindowsAgents-Job - vtestvm01.foo'). For some job families
    # the parent's Get-VBRTaskSession output already exposes the per-machine
    # work under the parent name (ADR 0012), making the per-machine child
    # sessions duplicates. For other families the parent's tasks are
    # orchestration-only with zero data, and the children carry the actual
    # work. The discriminator is the job's Mode property:
    #   * Mode 'ManagedByBackupServer' - parent has data; drop children to
    #     avoid double-counting in jobSessionSummary.
    #   * Mode 'ManagedByAgent' (policy) - parent has zero-data orchestrator
    #     tasks; KEEP children so the rollup picks up the per-machine data.
    #   * EP jobs (Mode null/empty) - typically no children; default to keep.
    if ($agentSessions.Count -gt 0 -and $agentJobs.Count -gt 0) {
        $dedupParents = @{}
        foreach ($aj in $agentJobs) {
            if ($null -ne $aj.Name -and "$($aj.Mode)" -eq 'ManagedByBackupServer') {
                $dedupParents[$aj.Name] = $true
            }
        }

        if ($dedupParents.Count -gt 0) {
            $beforeCount = $agentSessions.Count
            $agentSessions = @($agentSessions | Where-Object {
                # Null JobName means the session is not a canonical agent-job
                # session (e.g. a test sentinel). Keep it.
                if ($null -eq $_.JobName) { return $true }
                # If JobName equals a known parent (canonical session), keep.
                if ($dedupParents.ContainsKey($_.JobName)) { return $true }
                # Per-machine pattern: "<Parent> - <Vm>" or "<Parent>\<Vm>".
                # Drop only when the parent prefix is in $dedupParents.
                $m = [regex]::Match($_.JobName, '^(.+?)(?:\s+-\s+|\\).+$')
                if (-not $m.Success) { return $true }
                return -not $dedupParents.ContainsKey($m.Groups[1].Value)
            })
            $dropped = $beforeCount - $agentSessions.Count
            if ($dropped -gt 0) {
                Write-LogFile "Dropped $dropped per-machine child session(s) for ManagedByBackupServer agent job(s) (data captured via parent task hierarchy)"
            }
        }
    }

    return $vmSessions + $agentSessions
}
