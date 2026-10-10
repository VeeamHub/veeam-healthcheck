#Requires -Version 5.1

function Get-VhciJobSessions {
    <#
    .Synopsis
        Returns backup-session objects for a list of jobs within a time window,
        using the indexed Veeam.Backup.Core.CBackupSession fast path when
        available and falling back to a caller-supplied cmdlet when not.
        See docs/superpowers/specs/2026-05-27-vbr-session-fast-path-design.md
        and ADR 0018.
    .Parameter Jobs
        Array of job objects (CBackupJob / agent job). Must have an .Id (Guid)
        property in the fast path; unused in slow path. May be empty.
    .Parameter Since
        Earliest CreationTime to include. Sessions with CreationTime > $Since
        are returned.
    .Parameter SlowPathCommand
        Scriptblock invoked once when the fast path is not available. Should
        return all sessions of the relevant family; this function filters by
        $Since client-side. Typical values: { Get-VBRBackupSession } or
        { Get-VBRComputerBackupJobSession }.
    .Parameter PathLabel
        Short identifier used in log line prefixes (e.g. "VM/BackupCopy",
        "Agent"). Does not affect behaviour.
    .Outputs
        [object[]] -- array of session objects (CBackupSession on fast path;
                      whatever $SlowPathCommand returns on slow path).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [AllowEmptyCollection()] [object[]] $Jobs,
        [Parameter(Mandatory)] [datetime]    $Since,
        [Parameter(Mandatory)] [scriptblock] $SlowPathCommand,
        [Parameter(Mandatory)] [string]      $PathLabel
    )

    $useFastPath = Test-VhciCBackupSessionFastPath

    if ($useFastPath) {
        Write-LogFile "[$PathLabel] Using fast path (CBackupSession.GetAllSessionsByPolicyJobAndTimeRange)"

        # Upper bound for the time-range query. Captured once per call so all
        # per-job queries see the same horizon.
        $until = Get-Date

        $results = New-Object System.Collections.ArrayList

        # A session can come back for more than one queried job: the query
        # for a policy job already returns its per-machine child sessions
        # (ADR 0018), so a job that also owns them (a Backup Copy worker
        # found by discovery, issue #224) would return them a second time and
        # double-count them downstream. Keep the first copy by session Id.
        # A session without a usable Id cannot be compared and is always kept.
        $seenSessionIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($job in $Jobs) {
            try {
                $jobResults = @(Invoke-VhciCBackupSessionFetch -JobId $job.Id -Since $Since -Until $until)
                foreach ($session in $jobResults) {
                    if ($null -ne $session -and $null -ne $session.Id -and -not $seenSessionIds.Add($session.Id.ToString())) {
                        continue
                    }
                    [void]$results.Add($session)
                }
            } catch {
                Write-LogFile "[$PathLabel] Failed to fetch sessions for job '$($job.Name)': $($_.Exception.Message)" -LogLevel 'WARNING'
            }
        }
        Write-LogFile "[$PathLabel] Collected $($results.Count) sessions via fast path"
        return @($results)
    }

    # Slow path: single unfiltered cmdlet call, client-side cutoff filter.
    # Matches pre-59e2621 behaviour for v12 environments.
    $cmdText = $SlowPathCommand.ToString().Trim()
    Write-LogFile "[$PathLabel] Using slow path ($cmdText unfiltered)"

    $raw = @()
    try {
        $raw = @(& $SlowPathCommand)
    } catch {
        Write-LogFile "[$PathLabel] Slow path failed: $($_.Exception.Message)" -LogLevel 'WARNING'
        return @()
    }

    $filtered = @($raw | Where-Object { $_.CreationTime -gt $Since })
    Write-LogFile "[$PathLabel] Collected $($filtered.Count) sessions via slow path"
    return $filtered
}
