#Requires -Version 5.1

function Get-VhcJob {
    <#
    .Synopsis
        Collects all VBR job types and exports detailed job configuration data.
        Calls nine private sub-functions for each job family, then runs the main
        job loop (over the jobs from Get-VhcJobDiscovery) with restore-point
        size calculation.
        Exports _Jobs.csv, _configBackup.csv.
        Source: Get-VBRConfig.ps1 lines 1090-1587.
    .Parameter RepositoryDetails
        ArrayList of [pscustomobject]@{ID; Name} rows returned by Get-VhcRepository.
        Used to resolve TargetRepositoryId to a human-readable name in _Jobs.csv.
        May be $null - repo names will be blank in that case.
    .Parameter VBRVersion
        Major VBR version integer. Defaults to 0 when version detection
        fails. Only used when -JobDiscovery is not supplied: it is passed on
        to Get-VhcJobDiscovery, where it gates tier C job discovery (#222).
    .Parameter JobDiscovery
        Result of Get-VhcJobDiscovery. The orchestrator runs discovery once
        and passes it to this function and to Get-VhcBackupSessions. When
        omitted or $null, this function runs discovery itself.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [object]$RepositoryDetails = $null,

        [Parameter(Mandatory = $false)]
        [int]$VBRVersion = 0,

        [Parameter(Mandatory = $false)]
        [object]$JobDiscovery = $null
    )

    $message = "Collecting jobs info..."
    Write-LogFile $message

    # ------------------------------------------------------------------
    # Job discovery (Get-VBRJob + standalone agent + tier B/C, issues #222/#224)
    # ------------------------------------------------------------------
    # Normally supplied by the orchestrator, which runs discovery once and
    # shares it with the session collector. Runs here only when called
    # without it (standalone use, or the discovery collector failed).
    if ($null -eq $JobDiscovery) {
        $JobDiscovery = Get-VhcJobDiscovery -VBRVersion $VBRVersion
    }
    $Jobs         = $JobDiscovery.Jobs
    $configBackup = $null

    try {
        $configBackup = Get-VBRConfigurationBackupJob
    } catch {
        Write-LogFile "Configuration Backup Job collection failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    # ------------------------------------------------------------------
    # Sub-function collectors (each exports its own CSVs)
    # Each sub-collector is individually fault-isolated inside Invoke-VhciJobSubCollectors.
    # ------------------------------------------------------------------
    Invoke-VhciJobSubCollectors -Jobs @($Jobs)

    # ------------------------------------------------------------------
    # Restore-point matching: performance gate + global sweep
    # (ADR 0021: tiered sweep; ADR 0022: allowlist gate;
    #  ADR 0023: BackupId-grouped matching for all job types)
    # ------------------------------------------------------------------
    $KnownSafeJobTypes = @(
        'VMware Backup',
        'Hyper-V Backup',
        'Windows Agent Backup',
        'Windows Agent Policy',
        'Linux Agent Backup',
        'Cloud Director Backup',
        'VMware Replication',
        'Hyper-V Replication'
    )

    $NeedsSweep = [bool]($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)
    $NeedsSweepNatural = $NeedsSweep

    # TEMPORARY OVERRIDE (2026-08-26, Ben Thomas) - DO NOT LEAVE IN PLACE:
    # forces every collection run through the global sweep, bypassing the
    # ADR 0022 allowlist gate above, regardless of what it computed.
    # $KnownSafeJobTypes / $NeedsSweep are left intact (not deleted) so this
    # is a one-line revert - delete the line below and the gate returns to
    # normal ADR 0022 behavior.
    #
    # Why: an environment where every job is a $KnownSafeJobTypes type
    # (e.g. VMware-only) never triggers the sweep, so Get-VhcJob only ever
    # looks at each job's GetLastBackup() - its single CURRENT backup chain.
    # Restore points sitting in a PRIOR chain (e.g. after a repository
    # retarget) are never fetched at all in that case - not misclassified,
    # simply invisible - so the Orphaned & Superseded Backups report
    # silently under-reports for exactly the job types most customers run
    # (plain VMware/Hyper-V backup jobs). Only the global sweep's unscoped
    # Get-VBRRestorePoint call sees restore points outside a job's current
    # chain at all (see ADR 0021's "previously-invisible multi-chain disk
    # usage" note).
    #
    # A customer needs an accurate run in the next few minutes and complete
    # Orphaned/Superseded data matters more than the sweep's performance
    # cost for that run. Review after: if the perf hit is broadly
    # acceptable, consider making this permanent (supersedes ADR 0022); if
    # not, revert this line and find a cheaper way to close the same gap
    # (e.g. only sweep jobs with >1 backup chain, or cache across runs).
    $NeedsSweep = $true

    $BackupCopyJobTypes = @('SimpleBackupCopyPolicy', 'BackupSync')
    $RestorePointsByJob = @{}
    # SweepRan=false does not mean the cache is empty - the stale-ObjectId
    # guard below (in the main per-job loop) writes StaleObject entries
    # unconditionally, independent of the sweep. Unresolved/Tier2Suppressed
    # entries only ever come from inside the $NeedsSweep block below, so
    # those two Reasons are absent when SweepRan is false, but StaleObject
    # can still be present and trustworthy.
    $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
        SweepRan        = $false
        CandidateGroups = [System.Collections.Generic.List[object]]::new()
    }
    if ($NeedsSweep) {
        $SweepStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })

            # Same null-placeholder risk both structures below guard against -
            # a $Jobs element can be a non-null object with no populated Id
            # (or, per f21f03b, $Jobs itself can carry a null placeholder when
            # Get-VBRJob throws) - either way, .Id.ToString() on it aborts the
            # whole sweep via the outer catch, not just this one lookup. Built
            # in a single pass: $KnownJobIds only needs $j/$j.Id non-null,
            # $JobIdByName additionally requires a non-empty, first-seen Name -
            # keep that extra condition scoped to $JobIdByName's own branch,
            # not folded into the shared guard, or a job with an Id but no
            # Name would silently vanish from $KnownJobIds too.
            #
            # OrdinalIgnoreCase: $RestorePointsByJob (a Hashtable) looks up
            # keys case-insensitively, so this gate must not be stricter than
            # the thing it's gating - a case-sensitive HashSet here would risk
            # silently dropping a resolved Id that only ever mismatches on case.
            $JobIdByName = @{}
            $KnownJobIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($j in @($Jobs)) {
                if ($null -eq $j -or $null -eq $j.Id) { continue }
                $idStr = $j.Id.ToString()
                [void]$KnownJobIds.Add($idStr)
                if ($j.Name -and -not $JobIdByName.ContainsKey($j.Name)) { $JobIdByName[$j.Name] = $idStr }
            }

            $Tier1Matched = 0
            $Tier1Failed  = 0
            $Tier2Matched = 0

            # Group by BackupId (ADR 0023): confirmed live to be scoped to one
            # protected object's chain within one job, so every restore point
            # sharing a BackupId must resolve to the same owning job. Resolving
            # ownership once per group instead of once per point turns "one
            # GetSourceJob() call per restore point" into "one call per
            # protected object's retention chain" - the actual cost driver
            # the old Type=Snapshot skip existed to work around, addressed
            # directly instead of by excluding an entire Type value.
            #
            # Manual foreach + Dictionary instead of Group-Object: Group-Object
            # re-enters the pipeline and resolves -Property via
            # PSPropertyExpression per item, then wraps every group in a
            # GroupInfo/ArrayList. A single pass over the already-materialized
            # $AllRestorePoints array skips all three, which matters once this
            # runs against a large environment's full restore-point set.
            $Groups = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[object]]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($RestorePoint in $AllRestorePoints) {
                $BackupIdKey = $RestorePoint.BackupId.ToString()
                $GroupPoints = $null
                if (-not $Groups.TryGetValue($BackupIdKey, [ref]$GroupPoints)) {
                    $GroupPoints = [System.Collections.Generic.List[object]]::new()
                    $Groups[$BackupIdKey] = $GroupPoints
                }
                $GroupPoints.Add($RestorePoint)
            }

            # Tier 1: Id-based via GetSourceJob() (+ GetParentJob() walk-up),
            # one call per group, applied to every restore point in it.
            $UnresolvedGroups = [System.Collections.ArrayList]::new()
            foreach ($GroupPoints in $Groups.Values) {
                $Representative = $GroupPoints[0]

                $SourceJob   = $null
                $LookupThrew = $false
                try { $SourceJob = $Representative.GetSourceJob() } catch { $LookupThrew = $true }

                if ($null -ne $SourceJob) {
                    # A GetParentJob() throw means this job type doesn't
                    # implement it - falling back to the child's own Id is a
                    # valid outcome, not a failure, so it isn't counted
                    # alongside $Tier1Failed.
                    $ParentJob = $null
                    try { $ParentJob = $SourceJob.GetParentJob() } catch {}
                    if ($null -ne $ParentJob) { $SourceJob = $ParentJob }
                }

                $JobIdKey = $null
                if ($null -ne $SourceJob -and $null -ne $SourceJob.Id) {
                    $CandidateKey = $SourceJob.Id.ToString()
                    if ($KnownJobIds.Contains($CandidateKey)) { $JobIdKey = $CandidateKey }
                }

                if ($JobIdKey) {
                    if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                        $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                    }
                    foreach ($RestorePoint in $GroupPoints) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                    $Tier1Matched += $GroupPoints.Count
                } else {
                    [void]$UnresolvedGroups.Add($GroupPoints)
                    if ($LookupThrew) { $Tier1Failed += $GroupPoints.Count }
                }
            }

            # Tier 2: name-based fallback, GATED - only accepted if the
            # resolved job has zero tier-1 matches. Display names collide
            # across genuinely different backup objects, so this cannot be
            # trusted to override an existing Id-based match.
            #
            # Snapshot Tier 1's output once, here, after the full pass over
            # every group completes - not a live check during tier 2, and not
            # interleaved tier1->tier2 per group. Either of those would let a
            # stale-name group get accepted before a same-job Id-match group
            # (processed later) arrives, reintroducing exactly the
            # misattribution ADR 0021's gating was built to prevent.
            $Tier1MatchedJobIds = [System.Collections.Generic.HashSet[string]]::new(
                [string[]]$RestorePointsByJob.Keys,
                [System.StringComparer]::OrdinalIgnoreCase
            )

            # Resolution order for a group tier 1 left unresolved (#270): the
            # parent backup's own JobId first, then the name above. A name is
            # not an identity - it misses after a rename and can land on a
            # different job that now carries the old name - while
            # GetParentOrThis().JobId names the owning job directly and costs
            # no extra call (tier 2 already makes it). An all-zero or unknown
            # Id is not evidence against a name match (a chain whose job
            # record was replaced by a same-named job keeps an all-zero Id),
            # so it falls through to the name rather than blocking it; the
            # name is only skipped when the Id is a known job, because then
            # the Id route has already answered.
            #
            # Tier 3 (#268): a Backup Copy job's last backup, by BackupId.
            # A Backup Copy chain built from VMs resolves, in tier 1, to a
            # per-source child job whose Id is not in $Jobs (ADR 0021), and
            # Backup.JobId is per-child rather than the policy Id. What does
            # not change on a rename is which backup the job currently writes
            # to: $Job.GetLastBackup() returns it, and every restore point
            # under it carries the BackupId of one of its chains. Built
            # lazily, once, and only when a group needs it: one the Id and
            # name routes left unresolved, or one the Id route placed on a
            # Backup Copy job that already has a tier-1 match (the Id says
            # which job, not whether this is the job's current chain, and the
            # last backup is the only thing that tells a Backup Copy job's
            # current chains from its old ones). A run with nothing to rescue
            # pays nothing.
            $BackupCopyJobIdByBackupId = $null
            $Tier2IdMatched            = 0
            $Tier3Matched              = 0
            $ZeroJobId                 = [guid]::Empty.ToString()
            $BackupCopyJobIds          = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($j in @($Jobs)) {
                if ($null -ne $j -and $null -ne $j.Id -and [string]$j.JobType -in $BackupCopyJobTypes) { [void]$BackupCopyJobIds.Add($j.Id.ToString()) }
            }

            foreach ($GroupPoints in $UnresolvedGroups) {
                $Representative = $GroupPoints[0]

                $ParentName     = $null
                $ParentJobIdKey = $null
                try {
                    $ParentBackup = $Representative.GetBackup().GetParentOrThis()
                    $ParentName   = $ParentBackup.Name
                    # Separate try: a parent type without a JobId must not
                    # cost the name lookup that already succeeded.
                    try {
                        if ($null -ne $ParentBackup.JobId) {
                            $CandidateKey = $ParentBackup.JobId.ToString()
                            if ($CandidateKey -ne $ZeroJobId -and $KnownJobIds.Contains($CandidateKey)) { $ParentJobIdKey = $CandidateKey }
                        }
                    } catch {}
                } catch {}

                $JobIdKey      = $null
                $ResolvedViaId = $false
                if ($ParentJobIdKey) {
                    $JobIdKey      = $ParentJobIdKey
                    $ResolvedViaId = $true
                } elseif ($ParentName -and $JobIdByName.ContainsKey($ParentName)) {
                    $JobIdKey = $JobIdByName[$ParentName]
                }

                $CopyJobIdKey = $null
                if (-not $JobIdKey -or ($ResolvedViaId -and $Tier1MatchedJobIds.Contains($JobIdKey) -and $BackupCopyJobIds.Contains($JobIdKey))) {
                    if ($null -eq $BackupCopyJobIdByBackupId) {
                        $BackupCopyJobIdByBackupId = @{}
                        foreach ($CopyJob in @($Jobs)) {
                            if ($null -eq $CopyJob -or $null -eq $CopyJob.Id -or [string]$CopyJob.JobType -notin $BackupCopyJobTypes) { continue }
                            try {
                                $CopyBackup = $CopyJob.GetLastBackup()
                                if ($null -eq $CopyBackup) { continue }
                                foreach ($CopyPoint in @(Get-VBRRestorePoint -Backup $CopyBackup -WarningAction SilentlyContinue)) {
                                    if ($null -eq $CopyPoint -or $null -eq $CopyPoint.BackupId) { continue }
                                    $CopyBackupIdKey = $CopyPoint.BackupId.ToString()
                                    if (-not $BackupCopyJobIdByBackupId.ContainsKey($CopyBackupIdKey)) {
                                        $BackupCopyJobIdByBackupId[$CopyBackupIdKey] = $CopyJob.Id.ToString()
                                    }
                                }
                            } catch {
                                try { Write-LogFile "Backup Copy job '$($CopyJob.Name)': could not list its last backup's restore points for tier-3 matching - $($_.Exception.Message)" -LogLevel "WARNING" } catch {}
                            }
                        }
                    }
                    $GroupBackupIdKey = $null
                    try { $GroupBackupIdKey = $Representative.BackupId.ToString() } catch {}
                    if ($GroupBackupIdKey -and $BackupCopyJobIdByBackupId.ContainsKey($GroupBackupIdKey)) {
                        $CopyJobIdKey = $BackupCopyJobIdByBackupId[$GroupBackupIdKey]
                    }
                }

                # Tier 3 places a group no earlier route could, and confirms
                # one the Id route placed on a Backup Copy job that already
                # has a tier-1 match, which skips the gate below. A name
                # match is still gated as before. It never moves a group to
                # a different job than the Id or name chose.
                $ResolvedViaCopyBackup = $false
                if ($CopyJobIdKey -and (-not $JobIdKey -or $CopyJobIdKey -eq $JobIdKey)) {
                    $JobIdKey              = $CopyJobIdKey
                    $ResolvedViaCopyBackup = $true
                }
                if (-not $JobIdKey) {
                    # No route resolved this group to any current job -
                    # a #192 Orphaned candidate (or a Tape Backup - excluded
                    # downstream by Get-VhcOrphanedSupersededBackups.ps1,
                    # not here).
                    [void]$script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
                        Reason        = 'Unresolved'
                        CurrentJobId  = $null
                        RestorePoints = $GroupPoints
                    })
                    continue
                }
                if (-not $ResolvedViaCopyBackup -and $Tier1MatchedJobIds.Contains($JobIdKey)) {
                    # Named a real job, but that job already has a tier-1
                    # match elsewhere - a #192 Superseded candidate.
                    [void]$script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
                        Reason        = 'Tier2Suppressed'
                        CurrentJobId  = $JobIdKey
                        RestorePoints = $GroupPoints
                    })
                    continue
                }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                foreach ($RestorePoint in $GroupPoints) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                if ($ResolvedViaCopyBackup) { $Tier3Matched += $GroupPoints.Count }
                else {
                    $Tier2Matched += $GroupPoints.Count
                    if ($ResolvedViaId) { $Tier2IdMatched += $GroupPoints.Count }
                }
            }

            try {
                Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2 ($Tier2IdMatched by parent job Id), $Tier3Matched tier-3 (Backup Copy last backup), $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched - $Tier3Matched) unmatched/orphaned/snapshot"
            } catch {}
            try {
                Write-LogFile "BackupId grouping: $($AllRestorePoints.Count) restore points reduced to $($Groups.Count) groups ($($Groups.Count) tier-1 lookups + $($UnresolvedGroups.Count) tier-2 lookups attempted, vs. $($AllRestorePoints.Count) lookups pre-grouping)"
            } catch {}
            # Perf diagnostics for the temporary NeedsSweep override above: natural-gate
            # is what ADR 0022's allowlist would have decided on its own, so a future
            # perf complaint can be checked against whether the override actually
            # changed the outcome for that run (natural-gate=False) or the sweep would
            # have run anyway (natural-gate=True, reverting the override wouldn't help).
            try {
                $SweepStopwatch.Stop()
                Write-LogFile "Restore point sweep: $($SweepStopwatch.ElapsedMilliseconds)ms elapsed, $($AllRestorePoints.Count) restore points, $(@($Jobs).Count) jobs, natural-gate=$NeedsSweepNatural, effective-gate=$NeedsSweep"
            } catch {}
        } catch {
            # This is the sweep's outermost catch - unlike the Write-LogFile
            # calls inside the try block (which just fall through to here),
            # a throw from this one has nowhere left to go: it unwinds
            # Get-VhcJob entirely and _Jobs.csv/_configBackup.csv never
            # export at all, which is worse than merely disabling the sweep.
            try { Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR" } catch {}
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
            $NeedsSweep = $false

            # A throw partway through Tier 1/Tier 2 (above) can leave some
            # Unresolved/Tier2Suppressed entries already added to
            # CandidateGroups before the failure - Clear() them so
            # SweepRan=false actually means "no Unresolved/Tier2Suppressed
            # entries present" (the invariant this cache's own doc comment,
            # a few lines above, states as a hard fact). Otherwise
            # Get-VhcOrphanedSupersededBackups.ps1 - which doesn't gate its
            # CandidateGroups loop on SweepRan - would render a table of
            # Orphaned/Superseded rows sourced from a known-incomplete sweep
            # at the same time as the "not evaluated" banner: an internally
            # contradictory report. Safe to clear unconditionally here:
            # StaleObject entries are added later, in the separate per-job
            # loop below, which hasn't run yet at this point.
            $script:VhcOrphanedSupersededCache.CandidateGroups.Clear()
        }
    }
    $script:VhcOrphanedSupersededCache.SweepRan = $NeedsSweep

    # ------------------------------------------------------------------
    # Main VBR job processing loop - restore point size calculation
    # ------------------------------------------------------------------
    [System.Collections.ArrayList]$AllJobs = @()

    foreach ($Job in @($Jobs)) {
        if ($null -eq $Job) { continue }
        try {
            $RestorePoints = @()
            if ($NeedsSweep) {
                if ($null -ne $Job.Id) {
                    $JobIdKey = $Job.Id.ToString()
                    if ($RestorePointsByJob.ContainsKey($JobIdKey)) {
                        $RestorePoints = $RestorePointsByJob[$JobIdKey]
                    }
                }
            } else {
                $LastBackup = $Job.GetLastBackup()
                if ($null -ne $LastBackup) {
                    $RestorePoints = @(Get-VBRRestorePoint -Backup $LastBackup)
                }
            }

            # vApp-container exclusion (#193 double-count / #192 false-
            # Superseded): confirmed live (VBR v13, 'VMware Cloud Director -
            # vApp Backup') that GetObjectsInJob() returns a CObjectInJob
            # entry for the vApp container itself, whose .Object is a
            # Veeam.Backup.Core.CVcdHierarchyObject with Type='Vapp' - a
            # real, general discriminator, not a job-TypeToString guess. The
            # container's own restore-point chain exists in the backup too,
            # with ApproxSize equal to the SUM of all nested VMs' ApproxSize
            # (live: container 1,467,774,727,809 vs. 8 VMs summing to
            # 1,467,787,315,602 - same number), so leaving it in the
            # restore-point set double-counts CalculatedOriginalSize below.
            # Its presence also poisons the stale-ObjectId guard just below:
            # the container's own point trivially "matches" the container's
            # own GetObjectsInJob() entry, making $MatchedAny true and
            # flagging every real VM Superseded. Excluding it here (rather
            # than disabling the guard for the whole job) keeps genuine
            # per-VM stale detection working for any real VM ids
            # GetObjectsInJob() does return alongside the container. Only
            # 'Vapp' is matched - other dynamic-scope job types
            # (datastore/host/tag) are not known to exhibit this and are
            # deliberately left uncovered without live evidence.
            $CurrentJobObjects = $null
            try { $CurrentJobObjects = @($Job.GetObjectsInJob()) } catch { $CurrentJobObjects = $null }

            $VappContainerIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($JobObject in @($CurrentJobObjects)) {
                $ObjType = $null
                try { $ObjType = $JobObject.Object.Type } catch { $ObjType = $null }
                if ($ObjType -eq 'Vapp') {
                    try { [void]$VappContainerIds.Add($JobObject.ObjectId.ToString()) } catch {}
                }
            }

            if ($VappContainerIds.Count -gt 0 -and $RestorePoints.Count -gt 0) {
                $KeptPoints   = [System.Collections.Generic.List[object]]::new()
                $DroppedCount = 0
                foreach ($RestorePoint in $RestorePoints) {
                    $IsContainerPoint = $false
                    try { $IsContainerPoint = ($null -ne $RestorePoint.ObjectId -and $VappContainerIds.Contains($RestorePoint.ObjectId.ToString())) } catch { $IsContainerPoint = $false }
                    if ($IsContainerPoint) { $DroppedCount++ } else { $KeptPoints.Add($RestorePoint) }
                }
                if ($DroppedCount -gt 0) {
                    Write-LogFile "Job '$($Job.Name)': excluded $DroppedCount vApp-container restore point(s) from sizing (container ApproxSize duplicates nested VM totals; see ADR 0021 / issue #193)." -LogLevel "INFO"
                    $RestorePoints = @($KeptPoints)
                }
            }

            # Stale-ObjectId guard (#192 Superseded / #197 fix): runs for
            # every job regardless of $NeedsSweep, since GetObjectsInJob()
            # and the sweep-cache lookup above are both per-job already -
            # this isn't the expensive global sweep ADR 0022 gates. Zero
            # overlap between this job's restore points and its current
            # GetObjectsInJob() membership means the call isn't returning
            # per-object granularity for this job (confirmed live: VMware
            # Cloud Director Backup returns the vApp container, not its
            # nested VMs, per ADR 0021) - trust nothing, exclude nothing,
            # rather than flag every real object Superseded and zero the
            # job's size.
            if ($RestorePoints.Count -gt 0) {
                $CurrentObjectIds = $null
                try {
                    # @(...), not a bare pipeline: PowerShell collapses a
                    # zero-object pipeline result to $null rather than an
                    # empty array, which [string[]] then leaves as $null too
                    # - the HashSet constructor's `collection` parameter
                    # rejects null outright (ArgumentNullException), so a
                    # job with GetObjectsInJob() legitimately returning zero
                    # current objects hit the catch below and got treated
                    # identically to "GetObjectsInJob() itself threw", purely
                    # by accident of this cast rather than by intent.
                    # Vapp-typed entries are excluded here too (not just from
                    # $RestorePoints above) - otherwise the container's own
                    # id would still sit in $CurrentObjectIds with nothing
                    # left in $RestorePoints to match it, which is harmless,
                    # but leaving it out entirely keeps this set's meaning
                    # ("current, checkable, leaf objects") consistent with
                    # the filter just applied above.
                    $CurrentObjectIds = [System.Collections.Generic.HashSet[string]]::new(
                        [string[]]@(@($CurrentJobObjects) | Where-Object { -not $VappContainerIds.Contains($_.ObjectId.ToString()) } | ForEach-Object { $_.ObjectId.ToString() }),
                        [System.StringComparer]::OrdinalIgnoreCase
                    )
                } catch {
                    $CurrentObjectIds = $null
                }

                if ($null -ne $CurrentObjectIds) {
                    # A null RestorePoint.ObjectId can't be classified as
                    # either current or stale - .ToString() on it would
                    # throw, caught by this job's own outer try/catch, which
                    # would zero its ENTIRE size (not just the one point).
                    # Treated as a non-match here, and kept (not excluded)
                    # in the partition below, consistent with this guard's
                    # own "uncertain => don't exclude" rule.
                    $MatchedAny = $false
                    foreach ($RestorePoint in $RestorePoints) {
                        if ($null -ne $RestorePoint.ObjectId -and $CurrentObjectIds.Contains($RestorePoint.ObjectId.ToString())) { $MatchedAny = $true; break }
                    }

                    if ($MatchedAny) {
                        $ActiveRestorePoints = [System.Collections.Generic.List[object]]::new()
                        $StaleByObjectId     = @{}
                        foreach ($RestorePoint in $RestorePoints) {
                            if ($null -eq $RestorePoint.ObjectId) {
                                $ActiveRestorePoints.Add($RestorePoint)
                                continue
                            }
                            $ObjIdKey = $RestorePoint.ObjectId.ToString()
                            if ($CurrentObjectIds.Contains($ObjIdKey)) {
                                $ActiveRestorePoints.Add($RestorePoint)
                            } else {
                                if (-not $StaleByObjectId.ContainsKey($ObjIdKey)) {
                                    $StaleByObjectId[$ObjIdKey] = [System.Collections.Generic.List[object]]::new()
                                }
                                $StaleByObjectId[$ObjIdKey].Add($RestorePoint)
                            }
                        }
                        # Cast back to a plain array, not the List[object] itself:
                        # List[T] defines its own native ForEach(Action<T>)
                        # method, which silently shadows the PowerShell
                        # array-intrinsic .ForEach{} the main loop below uses
                        # to sum OnDiskGB - confirmed empirically to no-op
                        # (0 iterations, no error) rather than throw.
                        $RestorePoints = @($ActiveRestorePoints)

                        # $Job.Id.ToString() unguarded would throw on a null
                        # Id - the sweep path elsewhere in this function
                        # already checks ($null -ne $Job.Id) before use, but
                        # this non-sweep path never did. A throw here is
                        # caught by this job's own per-job catch and zeroes
                        # its ENTIRE size, same failure mode the null-
                        # RestorePoint.ObjectId guard above exists to avoid.
                        $CurrentJobIdStr = if ($null -ne $Job.Id) { $Job.Id.ToString() } else { $null }
                        foreach ($StalePoints in $StaleByObjectId.Values) {
                            [void]$script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
                                Reason        = 'StaleObject'
                                CurrentJobId  = $CurrentJobIdStr
                                RestorePoints = $StalePoints
                            })
                        }
                    }
                }
            }

            $TotalOnDiskGB = 0

            $RestorePoints.ForEach{
                $RestorePoint  = $PSItem
                $OnDiskGB      = $RestorePoint.GetStorage().Stats.BackupSize / 1GB
                $TotalOnDiskGB += $OnDiskGB
            }

            # A Backup Copy job's last backup spans every source it copies, but the
            # sweep hands the agent-sourced chains to the per-source worker job
            # ($Jobs row of type SimpleBackupCopyParentWorker) that owns them.
            # Their on-disk size is added to the parent when the report is built;
            # their source size has to be added here, where the restore points are
            # still available, so a protected object that appears in both chains
            # is counted once (latest point per ObjectId, below). Matched by
            # BackupId against this job's own last backup, not by job name, and
            # used for the source size only, so no restore point is counted twice
            # in OnDiskGB.
            $SizingPoints = $RestorePoints
            if ([string]$Job.JobType -in $BackupCopyJobTypes) {
                try {
                    # Only worker jobs that own restore points matter; with none
                    # (the common case) the last backup is not even listed.
                    $WorkerPointSets = @(foreach ($WorkerJob in @($Jobs)) {
                        if ($null -eq $WorkerJob -or $null -eq $WorkerJob.Id -or [string]$WorkerJob.JobType -ne 'SimpleBackupCopyParentWorker') { continue }
                        $WorkerKey = $WorkerJob.Id.ToString()
                        if ($RestorePointsByJob.ContainsKey($WorkerKey) -and $RestorePointsByJob[$WorkerKey].Count -gt 0) { , $RestorePointsByJob[$WorkerKey] }
                    })
                    $LastBackupForSizing = if ($WorkerPointSets.Count -gt 0) { $Job.GetLastBackup() } else { $null }
                    if ($null -ne $LastBackupForSizing) {
                        $LastBackupIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
                        foreach ($LastPoint in @(Get-VBRRestorePoint -Backup $LastBackupForSizing -WarningAction SilentlyContinue)) {
                            if ($null -ne $LastPoint -and $null -ne $LastPoint.BackupId) { [void]$LastBackupIds.Add($LastPoint.BackupId.ToString()) }
                        }
                        $WorkerPoints = [System.Collections.Generic.List[object]]::new()
                        foreach ($WorkerPointSet in $WorkerPointSets) {
                            foreach ($WorkerPoint in $WorkerPointSet) {
                                if ($null -ne $WorkerPoint.BackupId -and $LastBackupIds.Contains($WorkerPoint.BackupId.ToString())) { $WorkerPoints.Add($WorkerPoint) }
                            }
                        }
                        if ($WorkerPoints.Count -gt 0) { $SizingPoints = @($RestorePoints) + @($WorkerPoints) }
                    }
                } catch {
                    try { Write-LogFile "Backup Copy job '$($Job.Name)': could not add its worker chains to the source size - $($_.Exception.Message)" -LogLevel "WARNING" } catch {}
                }
            }

            # CalculatedOriginalSize: prefer ApproxSize from latest restore point per object;
            # fall back to IncludedSize for legacy backups or when no restore points exist.
            $CalculatedOriginalSize = 0
            try {
                if ($SizingPoints -and $SizingPoints.Count -gt 0) {
                    $LatestPoints = $SizingPoints |
                        Group-Object -Property { $_.ObjectId } |
                        ForEach-Object {
                            $_.Group | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1
                        }
                    $ApproxSum = ($LatestPoints |
                        Where-Object { $null -ne $_.ApproxSize } |
                        Measure-Object -Property ApproxSize -Sum).Sum

                    if ($ApproxSum -and $ApproxSum -gt 0) {
                        $CalculatedOriginalSize = $ApproxSum
                    } else {
                        $CalculatedOriginalSize = $Job.Info.IncludedSize
                    }
                } else {
                    $CalculatedOriginalSize = $Job.Info.IncludedSize
                }
            } catch {
                $CalculatedOriginalSize = $Job.Info.IncludedSize
            }
        } catch {
            Write-LogFile "Could not calculate restore point sizes for job: $($Job.Name) - $($_.Exception.Message)" -LogLevel "WARNING"
            $TotalOnDiskGB          = 0
            $CalculatedOriginalSize = $Job.Info.IncludedSize
        }

        try { Write-LogFile "Job: $($Job.Name) - Total OnDisk GB: $TotalOnDiskGB" } catch {}

        $JobDetails = $Job | Select-Object -Property 'Name', 'JobType',
            'SheduleEnabledTime', 'ScheduleOptions',
            @{n = 'RestorePoints';                  e = { $Job.Options.BackupStorageOptions.RetainCycles } },
            @{n = 'RepoName';                       e = { $RepositoryDetails | Where-Object { $_.Id -eq $job.Info.TargetRepositoryId.Guid } | Select-Object -ExpandProperty Name } },
            @{n = 'Algorithm';                      e = { $Job.Options.BackupTargetOptions.Algorithm } },
            @{n = 'FullBackupScheduleKind';         e = { $Job.Options.BackupTargetOptions.FullBackupScheduleKind } },
            @{n = 'FullBackupDays';                 e = { $Job.Options.BackupTargetOptions.FullBackupDays } },
            @{n = 'TransformFullToSyntethic';       e = { $Job.Options.BackupTargetOptions.TransformFullToSyntethic } },
            @{n = 'TransformIncrementsToSyntethic'; e = { $Job.Options.BackupTargetOptions.TransformIncrementsToSyntethic } },
            @{n = 'TransformToSyntethicDays';       e = { $Job.Options.BackupTargetOptions.TransformToSyntethicDays } },
            @{n = 'PwdKeyId';                       e = { $_.Info.PwdKeyId } },
            @{n = 'OriginalSize';                   e = { $CalculatedOriginalSize } },
            @{n = 'RetentionType';                  e = { $Job.BackupStorageOptions.RetentionType } },
            @{n = 'RetentionCount';                 e = { $Job.BackupStorageOptions.RetainCycles } },
            @{n = 'RetainDaysToKeep';               e = { $Job.BackupStorageOptions.RetainDaysToKeep } },
            @{n = 'DeletedVmRetentionDays';         e = { $Job.BackupStorageOptions.RetainDays } },
            @{n = 'DeletedVmRetention';             e = { $Job.BackupStorageOptions.EnableDeletedVmDataRetention } },
            @{n = 'CompressionLevel';               e = { $Job.BackupStorageOptions.CompressionLevel } },
            @{n = 'Deduplication';                  e = { $Job.BackupStorageOptions.EnableDeduplication } },
            @{n = 'BlockSize';                      e = { $Job.BackupStorageOptions.StgBlockSize } },
            @{n = 'IntegrityChecks';                e = { $Job.BackupStorageOptions.EnableIntegrityChecks } },
            @{n = 'SpecificStorageEncryption';      e = { $Job.BackupStorageOptions.UseSpecificStorageEncryption } },
            @{n = 'StgEncryptionEnabled';           e = { $Job.BackupStorageOptions.StorageEncryptionEnabled } },
            @{n = 'KeepFirstFullBackup';            e = { $Job.BackupStorageOptions.KeepFirstFullBackup } },
            @{n = 'EnableFullBackup';               e = { $Job.BackupStorageOptions.EnableFullBackup } },
            @{n = 'BackupIsAttached';               e = { $Job.BackupStorageOptions.BackupIsAttached } },
            @{n = 'GfsWeeklyIsEnabled';             e = { $Job.options.gfspolicy.weekly.IsEnabled } },
            @{n = 'GfsWeeklyCount';                 e = { $Job.options.gfspolicy.weekly.KeepBackupsForNumberOfWeeks } },
            @{n = 'GfsMonthlyEnabled';              e = { $Job.options.gfspolicy.Monthly.IsEnabled } },
            @{n = 'GfsMonthlyCount';                e = { $Job.options.gfspolicy.Monthly.KeepBackupsForNumberOfMonths } },
            @{n = 'GfsYearlyEnabled';               e = { $Job.options.gfspolicy.yearly.IsEnabled } },
            @{n = 'GfsYearlyCount';                 e = { $Job.options.gfspolicy.yearly.KeepBackupsForNumberOfYears } },
            @{n = 'IndexingType';                   e = { $Job.VssOptions.GuestFSIndexingType } },
            @{n = 'OnDiskGB';                       e = { $TotalOnDiskGB } },
            @{n = 'AAIPEnabled';                    e = { $Job.VssOptions.VssSnapshotOptions.Enabled } },
            @{n = 'VSSEnabled';                     e = { $Job.VssOptions.VssSnapshotOptions.ApplicationProcessingEnabled } },
            @{n = 'VSSIgnoreErrors';                e = { $Job.VssOptions.VssSnapshotOptions.IgnoreErrors } },
            @{n = 'GuestFSIndexingEnabled';         e = { $Job.VssOptions.GuestFSIndexingOptions.IsEnabled } },
            # IsScheduleEnabled reflects whether the job itself is active, not whether it has a schedule
            @{n = 'IsJobEnabled';                  e = { $Job.IsScheduleEnabled } },
            # RunManually = True means the job is enabled but has no schedule configured (runs on demand only)
            @{n = 'IsScheduleDisabled';            e = { $Job.Options.JobOptions.RunManually } },
            @{n = 'Platform';                      e = {
                $key = if ($Job.Name) { $Job.Name.ToLowerInvariant() } else { '' }
                if ($script:PlatformMap -and $script:PlatformMap.ContainsKey($key)) {
                    $script:PlatformMap[$key]
                } else { '' }
            }},
            @{n = 'TypeToString';                  e = { $Job.TypeToString } }

        $AllJobs.Add($JobDetails) | Out-Null
    }

    $AllJobs      | Export-VhciCsv -FileName '_Jobs.csv'
    $configBackup | Export-VhciCsv -FileName '_configBackup.csv'

    Write-LogFile ($message + "DONE")

    # Returned explicitly so Get-VBRConfig.ps1 can pass this same object to
    # Get-VhcOrphanedSupersededBackups -OrphanedSupersededCache, instead of
    # that function reaching for $script:VhcOrphanedSupersededCache as an
    # implicit side effect of this function having already run in the same
    # collection pass - an ordering dependency that used to be enforced only
    # by a comment on the caller. The script-scoped variable is still set
    # too (untouched above), so nothing here changes for direct/standalone
    # callers - including this function's own Pester tests, which read it
    # back via $script:VhcOrphanedSupersededCache after calling Get-VhcJob.
    return $script:VhcOrphanedSupersededCache
}
