#Requires -Version 5.1

function Get-VhcJobDiscovery {
    <#
    .Synopsis
        Discovers every VBR job once, including jobs Get-VBRJob does not return,
        so the job inventory (Get-VhcJob) and the session collector
        (Get-VhcBackupSessions) work from the same job set (issue #224).

        Discovery runs in up to four steps, all additive:
        1. Get-VBRJob.
        2. Standalone (unmanaged) agent jobs, via the backups they own.
        3. Tier B (issue #222): jobs reconstructed from Get-VBRBackup + GetJob().
        4. Tier C (issue #222): the core job model, VBR versions below 13 only.
        Source: moved unchanged out of Get-VhcJob.ps1.
    .Parameter VBRVersion
        Major VBR version integer. Defaults to 0 when version detection
        fails. Gates tier C: Get-VBRJob's confirmed 12.3.x gap for Nutanix
        AHV/Proxmox/HPE Morpheus VME jobs does not reproduce on VBR 13, so
        tier C's internal [Veeam.Backup.Core.CBackupJob]::GetAll()
        enumeration only runs when VBRVersion -lt 13 - including the
        undetected/default value of 0, deliberately (see the tier C gate
        comment below for why).
    .Outputs
        [PSCustomObject] with:
        Jobs                All jobs, in the order Get-VhcJob has always used:
                            Get-VBRJob, standalone agent, tier B, tier C.
        VbrJobs             Only what Get-VBRJob returned.
        StandaloneAgentJobs Only the standalone agent jobs.
        DiscoveredJobs      Only tier B and tier C additions: the jobs
                            Get-VBRJob does not return.
        Jobs is a live-object array and is never serialised.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$VBRVersion = 0
    )

    # ------------------------------------------------------------------
    # Fetch primary job list
    # ------------------------------------------------------------------
    $Jobs                = $null
    $VbrJobs             = @()
    $StandaloneAgentJobs = @()
    $DiscoveredJobs      = [System.Collections.Generic.List[object]]::new()

    try {
        $Jobs = Get-VBRJob -WarningAction SilentlyContinue
    } catch {
        Write-LogFile "Main jobs collection failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }
    $VbrJobs = @($Jobs | Where-Object { $null -ne $_ })

    # Standalone (unmanaged) agent jobs are not returned by Get-VBRJob.
    # Enumerate them via the backup objects they own; .GetJob() returns
    # a CBackupJob with the same shape Get-VBRJob produces, so they flow
    # through the projection below unchanged.
    try {
        $standaloneBackups = @(Get-VBRBackup -WarningAction SilentlyContinue |
            Where-Object { $_.IsAgentStandaloneJob -eq $true })
        $standaloneJobs = @($standaloneBackups | ForEach-Object {
            $backup = $_
            try {
                $backup.GetJob()
            } catch {
                $msg = "Orphaned standalone backup skipped: Id={0} Name='{1}' Error={2}" -f $backup.Id, $backup.Name, $_.Exception.Message
                Write-LogFile $msg -LogLevel "WARNING"
                $null
            }
        } | Where-Object { $_ })
        Write-LogFile "Standalone agent jobs collected: $($standaloneJobs.Count)"
        if ($standaloneJobs.Count -gt 0) {
            $Jobs = @($Jobs) + $standaloneJobs
            $StandaloneAgentJobs = $standaloneJobs
        }
    } catch {
        Write-LogFile "Standalone agent job collection failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    # ------------------------------------------------------------------
    # Shared dedup guard for tiers B and C below (issue #222)
    # ------------------------------------------------------------------
    # One HashSet, not one per tier: a job either tier discovers must never
    # be merged twice, and a job tier B already found must not be
    # re-discovered by tier C. OrdinalIgnoreCase to stay consistent with
    # $KnownJobIds further down in this function, which performs the same
    # kind of Id membership check against the same underlying $Jobs Ids.
    $KnownJobIdSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($ExistingJob in @($Jobs)) {
        if ($null -ne $ExistingJob -and $null -ne $ExistingJob.Id) {
            [void]$KnownJobIdSet.Add($ExistingJob.Id.ToString())
        }
    }

    # ------------------------------------------------------------------
    # Tier B (issue #222): reconstruct jobs Get-VBRJob doesn't return, via
    # the backup objects they own.
    # ------------------------------------------------------------------
    # Confirmed live against a real VBR 12.3.x server (#222): Get-VBRJob
    # returns ZERO Nutanix AHV jobs in an environment with active AHV
    # protection, and Get-VBRPluginJob returns zero for the same jobs too
    # - not a degraded/environment-specific plugin state, a genuine gap in
    # both supported cmdlets on that version. Lab evidence (VBR 13.1)
    # further confirms Get-VBRBackup's TypeToString cleanly distinguishes
    # Nutanix AHV/Proxmox/HPE Morpheus VME backups from every other type,
    # and that .GetJob() on one of those backups returns a usable
    # CBackupJob with the same shape Get-VBRJob and the standalone-agent
    # block above already produce - so this block reuses that exact
    # pattern (rather than hardcoding a platform allowlist), self-
    # discovering whatever job type Get-VBRJob happens to miss on a given
    # VBR build instead of chasing one platform at a time.
    #
    # Deliberately NOT gated on .JobId (rejected during #222's design
    # review): Backup.JobId's "Guid.Empty means orphaned" behavior was
    # only ever confirmed on a VBR 13.1 lab, never on the 12.3.x builds
    # this fix targets. Treating an empty/default JobId as "definitely
    # orphaned, skip it" risks silently skipping exactly the backups this
    # fix needs to recover. try/catch around .GetJob() is the ONLY
    # discriminator this block trusts between "resolves to a real job" and
    # "genuinely orphaned" (lab-confirmed on 13.1: of 4 AHV backups, 1
    # resolved, 3 threw "Unable to get job for backup: <guid>" - both
    # outcomes are correct, not a partial failure, same as the standalone-
    # agent block's own orphan handling above). $Candidate.JobId is still
    # read below, but only as a cheap "already known, don't bother
    # re-resolving" pre-filter, nested inside its own non-empty check so a
    # Guid.Empty JobId can never itself cause a skip - being wrong about
    # JobId costs one harmless redundant .GetJob() call, never a missed
    # job.
    try {
        # Re-enumerates Get-VBRBackup independently of the standalone-agent
        # block above, rather than reusing its result - deliberate: this
        # tier's own outer catch needs to see a Get-VBRBackup failure even
        # if the standalone block's own call already succeeded, and a
        # purely-additive tier silently swallowing a real collection
        # failure is the wrong tradeoff on a code path whose real-world
        # (12.3.x) behavior nobody has been able to verify live yet.
        $TierBCandidates = @(Get-VBRBackup -WarningAction SilentlyContinue |
            Where-Object { $_.IsAgentStandaloneJob -ne $true })

        $TierBJobs = [System.Collections.Generic.List[object]]::new()
        foreach ($Candidate in $TierBCandidates) {
            if ($null -ne $Candidate.JobId -and $Candidate.JobId -ne [guid]::Empty) {
                if ($KnownJobIdSet.Contains($Candidate.JobId.ToString())) { continue }
            }
            try {
                $ResolvedJob = $Candidate.GetJob()
            } catch {
                $msg = "Orphaned backup skipped (tier B): Id={0} Name='{1}' Error={2}" -f $Candidate.Id, $Candidate.Name, $_.Exception.Message
                Write-LogFile $msg -LogLevel "WARNING"
                continue
            }
            if ($null -eq $ResolvedJob -or $null -eq $ResolvedJob.Id) { continue }

            # Walk up to the parent job, mirroring the sweep's own Tier 1
            # resolution further down in this file (GetSourceJob() then
            # GetParentJob()) - .GetJob() can resolve to a per-machine
            # child job object (an agent policy child, or a Backup Copy
            # per-object child), and without this walk-up tier B would add
            # one _Jobs.csv row per child instead of collapsing to the one
            # real job. A GetParentJob() throw means this job type doesn't
            # implement it - falling back to the child's own Id is a valid
            # outcome here too, same as Tier 1's handling.
            $ParentOfResolvedJob = $null
            try { $ParentOfResolvedJob = $ResolvedJob.GetParentJob() } catch {}
            if ($null -ne $ParentOfResolvedJob -and $null -ne $ParentOfResolvedJob.Id) {
                $ResolvedJob = $ParentOfResolvedJob
            }

            if ($KnownJobIdSet.Add($ResolvedJob.Id.ToString())) {
                $TierBJobs.Add($ResolvedJob)
            }
        }

        # This count is the only signal a real 12.3.x customer run can give
        # about whether tier B actually did anything - there is no lab
        # available to confirm .GetJob() behaves there the way it did on
        # the 13.1 lab this design was validated against.
        Write-LogFile "Jobs discovered via backup objects (tier B): $($TierBJobs.Count)"
        if ($TierBJobs.Count -gt 0) {
            $Jobs = @($Jobs) + @($TierBJobs)
            $DiscoveredJobs.AddRange($TierBJobs)
        }
    } catch {
        Write-LogFile "Tier B job discovery failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    # ------------------------------------------------------------------
    # Tier C (issue #222): enumerate the core job model directly, for VBR
    # versions below 13 only.
    # ------------------------------------------------------------------
    # #222's design review originally rejected the Veeam.Backup.Core.*
    # internal-reflection route entirely, on the reasoning that the
    # supported-cmdlet-based tier B above covers the confirmed gap. That
    # call is reversed here: tier B's behavior on the actual 12.3.x builds
    # this bug was reported against has never been verified live - the
    # only lab evidence available is VBR 13.1, where Get-VBRJob does not
    # exhibit the gap at all, so it cannot confirm what Get-VBRBackup does
    # on 12.3.x either. Tier C exists as an independent, version-gated
    # safety net that does not depend on Get-VBRBackup surfacing these
    # platform types, in case that also turns out to differ on 12.3.x.
    #
    # Uses .GetParent(), NOT .GetParentJob() - two different methods on
    # the same type. .GetParentJob() is what the sweep logic further down
    # in this file already calls (Tier 1 restore-point resolution) -
    # unrelated to this block, do not confuse the two. .GetParent()
    # resolves to this class's explicit IEpModeDetectable.GetParent()
    # implementation and was confirmed via a live Get-Member dump to
    # return a Veeam.Backup.Core.CBackupJob with .Id/.Name/
    # .TargetRepositoryId/.TypeToString all present - the same shape
    # Get-VBRJob/.GetJob() already produce. It was also empirically
    # confirmed in a lab to correctly collapse internal per-guest "agent
    # child" artifacts onto their real parent job (4 core job objects - 1
    # real parent + 3 internal children - all resolved to the same parent
    # identity via .GetParent()). .GetTopLvlJob()/.GetTopLvlJobId() were
    # also confirmed to behave similarly in a follow-up test, but were NOT
    # chosen here - weaker empirical trail than .GetParent()'s dedicated
    # live Get-Member confirmation.
    #
    # Gated on $VBRVersion -lt 13, not "-gt 0 -and -lt 13": $VBRVersion
    # defaults to 0 when detection fails, and running tier C on an
    # undetected-but-actually-12.3.x host costs nothing extra (the shared
    # $KnownJobIdSet dedup collapses anything already known via Get-VBRJob
    # or tier B), while skipping tier C there would leave the reported bug
    # in place on exactly the host that needs it most.
    if ($VBRVersion -lt 13) {
        $CoreJobRecords = $null
        try {
            $CoreJobRecords = Invoke-VhciCBackupJobFetch
        } catch {
            # The expected failure mode here is "the internal type isn't
            # loaded on this host" - not a collection failure of anything
            # this run was ever entitled to collect via a supported
            # cmdlet, so this is logged as WARNING/"tier unavailable"
            # rather than escalated via Add-VhciModuleError.
            Write-LogFile "Tier C job discovery unavailable: $($_.Exception.Message)" -LogLevel "WARNING"
            $CoreJobRecords = $null
        }

        $TierCJobs       = [System.Collections.Generic.List[object]]::new()
        $TierCEnumerated = 0

        # Item by item, not a single GetAll().GetParent() pipeline - one
        # bad object's property getter throwing must not abort the whole
        # tier, so every property/method touch on a single item is its own
        # isolated try/catch.
        foreach ($CoreJob in @($CoreJobRecords)) {
            if ($null -eq $CoreJob) { continue }
            $TierCEnumerated++

            $CoreJobId = $null
            try { $CoreJobId = $CoreJob.Id } catch { $CoreJobId = $null }
            $CoreJobHasNoRecord = $null
            try { $CoreJobHasNoRecord = $CoreJob.HasNoJobRecord } catch { $CoreJobHasNoRecord = $null }

            $ParentJob = $null
            try {
                $ParentJob = $CoreJob.GetParent()
            } catch {
                Write-LogFile ("Tier C core job object skipped: Id={0} HasNoJobRecord={1} GetParent() error={2}" -f $CoreJobId, $CoreJobHasNoRecord, $_.Exception.Message) -LogLevel "WARNING"
                continue
            }

            # HasNoJobRecord's meaning is unconfirmed - logged for every
            # entry so a real customer run's logs can eventually tell us
            # what it correlates with, but never filtered on.
            Write-LogFile ("Tier C core job object: Id={0} HasNoJobRecord={1}" -f $CoreJobId, $CoreJobHasNoRecord)

            $ParentJobId = $null
            if ($null -ne $ParentJob) {
                try { $ParentJobId = $ParentJob.Id } catch { $ParentJobId = $null }
            }
            if ($null -eq $ParentJobId) { continue }
            if ($KnownJobIdSet.Add($ParentJobId.ToString())) {
                $TierCJobs.Add($ParentJob)
            }
        }

        Write-LogFile "Jobs discovered via core job enumeration (tier C): $($TierCJobs.Count) (of $TierCEnumerated core job objects enumerated)"
        if ($TierCJobs.Count -gt 0) {
            $Jobs = @($Jobs) + @($TierCJobs)
            $DiscoveredJobs.AddRange($TierCJobs)
        }
    } else {
        Write-LogFile "Jobs discovered via core job enumeration (tier C): skipped - VBRVersion $VBRVersion already returns these jobs via Get-VBRJob."
    }

    return [PSCustomObject]@{
        Jobs                = $Jobs
        VbrJobs             = $VbrJobs
        StandaloneAgentJobs = $StandaloneAgentJobs
        DiscoveredJobs      = @($DiscoveredJobs)
    }
}
