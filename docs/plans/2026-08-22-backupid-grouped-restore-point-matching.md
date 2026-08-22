# BackupId-Grouped Tiered Matching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `Type=Snapshot` skip and the separate per-job Replica loop in `Get-VhcJob.ps1` with a single, unified restore-point matching pipeline that groups every restore point by `BackupId` before resolving ownership, per [ADR 0023](../../adr/0023-backupid-grouped-tiered-matching-for-all-job-types.md) and its [design doc](../specs/2026-08-22-restore-point-backupid-grouping-design.md).

**Architecture:** Group all swept restore points by `BackupId` (already confirmed scoped to one protected object's chain within one job). Resolve ownership once per group — Tier 1 (`GetSourceJob()` + `GetParentJob()` walk-up, validated against `$KnownJobIds`) across *all* groups first, then Tier 2 (`GetBackup().GetParentOrThis().Name`, gated on the complete Tier 1 result) across only the unresolved groups — and apply the outcome to every restore point in the group. `VMware Replication`/`Hyper-V Replication` join `$KnownSafeJobTypes`, collapsing `$NeedsSweep` to one check over `$Jobs` directly.

**Tech Stack:** PowerShell 7 (Pester v5 tests), no C# changes.

---

### Task 1: Add `BackupId` and call-counting to the restore-point test fixture

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1:262-303` (`New-FakeRestorePoint`)

This is pure test infrastructure — no assertions yet, so no failing-test step. The grouping tests in Task 3 need two things `New-FakeRestorePoint` doesn't have today: a settable `BackupId` (defaulting to a fresh unique guid, so every existing test that doesn't pass one keeps behaving exactly as it does today — one restore point, one group), and a way to prove a call was actually skipped, not just that the final numbers came out right.

- [x] **Step 1: Add the `-BackupId` parameter and call counters**

Replace lines 262-303 of `Get-VhcJob.Tests.ps1` (the whole `New-FakeRestorePoint` function, including its closing brace):

```powershell
    function script:New-FakeRestorePoint {
        param(
            [string]$Name = 'FakeRestorePoint',
            [string]$Type = 'Increment',
            [guid]$ObjectId = [guid]::NewGuid(),
            [guid]$BackupId = [guid]::NewGuid(),
            [datetime]$CreationTimeUtc = (Get-Date),
            [double]$ApproxSize = 1GB,
            [double]$BackupSize = 1GB,
            $SourceJob = $null,
            [switch]$ThrowOnGetSourceJob,
            [switch]$ThrowOnGetBackup,
            [string]$BackupParentOrThisName,
            [switch]$ThrowOnGetParentOrThis
        )
        $SourceJobCapture      = $SourceJob
        $ThrowSourceJobCapture = [bool]$ThrowOnGetSourceJob
        $ThrowGetBackupCapture = [bool]$ThrowOnGetBackup
        $FakeBackup            = if ($BackupParentOrThisName) {
            script:New-FakeBackup -ParentOrThisName $BackupParentOrThisName -ThrowOnGetParentOrThis:$ThrowOnGetParentOrThis
        } else { $null }
        # $CallCounts is a Hashtable (reference type), captured below as a
        # plain LOCAL alias before .GetNewClosure() runs, then mutated in
        # place from inside the closure. A scalar counter incremented via
        # $script: (e.g. `$script:GetSourceJobCallCount++`) does NOT work
        # here: .GetNewClosure() snapshots $script:-qualified variables into
        # a private copy at closure-creation time, so writes inside the
        # closure never propagate back to the real script-scope variable -
        # confirmed empirically, the assertion sees 0 forever. Mutating a
        # shared object's properties through a captured reference does
        # propagate, since the alias and the original point at the same
        # object.
        $CallCounts = if ($script:CallCounts) { $script:CallCounts } else { @{ GetSourceJob = 0; GetBackup = 0 } }

        $RestorePoint = [PSCustomObject]@{
            Name            = $Name
            Type            = $Type
            ObjectId        = $ObjectId
            BackupId        = $BackupId
            CreationTimeUtc = $CreationTimeUtc
            ApproxSize      = $ApproxSize
            BackupSizeValue = $BackupSize
        }
        $RestorePoint | Add-Member -MemberType ScriptMethod -Name GetSourceJob -Value {
            $CallCounts.GetSourceJob = $CallCounts.GetSourceJob + 1
            if ($ThrowSourceJobCapture) { throw 'GetSourceJob failed' }
            return $SourceJobCapture
        }.GetNewClosure()
        $RestorePoint | Add-Member -MemberType ScriptMethod -Name GetBackup -Value {
            $CallCounts.GetBackup = $CallCounts.GetBackup + 1
            if ($ThrowGetBackupCapture) { throw 'GetBackup failed' }
            return $FakeBackup
        }.GetNewClosure()
        $RestorePoint | Add-Member -MemberType ScriptMethod -Name GetStorage -Value {
            [PSCustomObject]@{ Stats = [PSCustomObject]@{ BackupSize = $this.BackupSizeValue } }
        }
        return $RestorePoint
    }
```

`$CallCounts` defaults to a fresh `@{ GetSourceJob = 0; GetBackup = 0 }` when `$script:CallCounts` hasn't been set, so every Describe block that doesn't care about call counts keeps working unmodified without initializing anything.

- [x] **Step 2: Run the full existing suite to confirm nothing broke**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1'"`
Expected: `Tests Passed: 44, Failed: 0` (current baseline, confirmed by actually running the suite — no behavior change from this task: every existing call site keeps its default unique `BackupId`, so grouping doesn't exist yet to reduce anything).

- [x] **Step 3: Commit**

```bash
git add "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1"
git commit -m "test(jobs): add BackupId and call counters to the restore-point fixture"
```

---

### Task 2: Add Replication types to the allowlist, simplify `$NeedsSweep`

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1:86-97`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1` (add one test to the `'Performance gate'` Describe block, around line 567)

This is independent of the grouping algorithm and safely separable — it only changes which job types trigger the sweep, not how the sweep resolves anything once it runs.

- [ ] **Step 1: Write the failing test**

Insert after the existing `It 'never calls Get-VBRRestorePoint without -Backup when every job is a known-safe type'` block (after line 567's closing `}`) in `Get-VhcJob.Tests.ps1`:

```powershell

    It 'never calls Get-VBRRestorePoint without -Backup when the only jobs are Replication and other known-safe types' {
        Mock Get-VBRJob -MockWith {
            @(
                (script:New-FakeJob -Name 'VMwareJob' -TypeToString 'VMware Backup'      -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })),
                (script:New-FakeJob -Name 'ReplicaJob' -TypeToString 'VMware Replication' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() }))
            )
        }
        Get-VhcJob
        $script:UnscopedCalls | Should -Be 0
        $script:ScopedCalls   | Should -Be 2
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed" 2>&1 | grep -A6 'only jobs are Replication'`
Expected: FAIL — `ReplicaJob` isn't on `$KnownSafeJobTypes` yet and isn't excluded from the gate check either (it *is* excluded from `$NonReplicaJobs`, so today it's actually invisible to `$NeedsSweep` too — but `VMwareJob` alone being safe means today's code *also* reports `$NeedsSweep = $false` here, i.e. this specific test might pass by coincidence today). Confirm by checking `$script:UnscopedCalls` is `0` and `$script:ScopedCalls` is `2` — if it already passes, that's fine, it means today's carve-out and tomorrow's allowlist entry produce the same gate outcome for this scenario; proceed to Step 3 regardless, since the test still documents intended post-change behavior and Task 4 removes the carve-out this test would otherwise silently depend on.

- [ ] **Step 3: Update `$KnownSafeJobTypes` and simplify `$NeedsSweep`**

Replace lines 86-97 of `Get-VhcJob.ps1`:

```powershell
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

    $NonReplicaJobs = @($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notlike '*Replication*' })
    $ReplicaJobs    = @($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -like '*Replication*' })
    $NeedsSweep     = [bool]($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)
```

**Important — do not remove the `$NonReplicaJobs`/`$ReplicaJobs` assignment lines in this task**, even though they're now otherwise only used by the sweep-gate comment. They are still read by name inside the not-yet-replaced Replica loop further down (`foreach ($Job in $ReplicaJobs)`). Deleting them here would leave that loop reading an undefined variable — `foreach` over `$null` runs zero times without erroring, so every Replica-loop test would silently start failing (their assertions about Replica-job sizing would see nothing happen) even though nothing about the Replica loop's own code changed. Only `$NeedsSweep`'s formula changes in this task (from `$NonReplicaJobs` to `$Jobs`). The two now-single-purpose-remaining variables come out in Task 4, together with the Replica loop that's their only remaining reader.

- [ ] **Step 4: Run the full suite to verify it passes**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1'"`
Expected: `Tests Passed: 45, Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1" "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1"
git commit -m "feat(jobs): add Replication job types to the sweep allowlist (ADR 0023)"
```

---

### Task 3: Write the failing tests for BackupId-grouped matching

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1` (new Describe block, inserted where the old Replica Describe block currently sits — Task 5 removes that block; for now, insert the new one immediately *before* it, at line 1098)

All five tests below must fail against the current (per-point) code before Task 4 changes anything. `BackupId grouping (ADR 0023)` is a new Describe block; `New-FakeRestorePoint`'s new `-BackupId` parameter (Task 1) drives every scenario.

- [ ] **Step 1: Insert the new Describe block**

Insert immediately before line 1098 (the `# ---------------------------------------------------------------------------` divider directly above `# Replica handling (ADR 0021): ...`) in `Get-VhcJob.Tests.ps1` — i.e. before the divider itself, not between it and the comment text below it.

```powershell
# ---------------------------------------------------------------------------
# BackupId grouping (ADR 0023): restore points sharing a BackupId are scoped
# to one protected object's chain within one job - resolve ownership once
# per group, apply the result to every point in it.
# ---------------------------------------------------------------------------
Describe 'BackupId grouping (ADR 0023): one lookup per group, applied to every restore point in it' {

    BeforeEach {
        $script:CapturedJobRows = @()
        $script:CallCounts      = @{ GetSourceJob = 0; GetBackup = 0 }
        Mock Write-LogFile                 -MockWith { }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors   -MockWith { }
        Mock Add-VhciModuleError           -MockWith { }
        Mock Get-VBRBackup                 -MockWith { @() }
        Mock Export-VhciCsv -MockWith {
            if ($FileName -eq '_Jobs.csv' -and $InputObject) {
                $script:CapturedJobRows += @($InputObject)
            }
        }
    }

    It 'reduces GetSourceJob() calls to one per BackupId group, applied to every restore point in the group' {
        $Job = script:New-FakeJob -Name 'RetentionChainJob' -TypeToString 'HPE Morpheus VME Backup'
        $SharedBackupId = [guid]'21212121-2121-2121-2121-212121212121'
        $SharedObjectId = [guid]'21212121-aaaa-aaaa-aaaa-212121212121'
        Mock Get-VBRJob -MockWith { @($Job) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'Full'  -BackupId $SharedBackupId -ObjectId $SharedObjectId -ApproxSize 10GB -BackupSize 3GB -SourceJob $Job),
                    (script:New-FakeRestorePoint -Name 'Incr1' -BackupId $SharedBackupId -ObjectId $SharedObjectId -ApproxSize 10GB -BackupSize 1GB -SourceJob $Job),
                    (script:New-FakeRestorePoint -Name 'Incr2' -BackupId $SharedBackupId -ObjectId $SharedObjectId -ApproxSize 10GB -BackupSize 1GB -SourceJob $Job)
                )
            } else { @() }
        }
        Get-VhcJob
        $script:CallCounts.GetSourceJob | Should -Be 1
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RetentionChainJob' }
        $Row.OnDiskGB | Should -Be 5   # 3 + 1 + 1 - all three points in the group bucketed, not just the representative
    }

    It 'a failed group''s single throw is credited to every restore point in the group, not just the representative' {
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        $FailingBackupId = [guid]'23232323-2323-2323-2323-232323232323'
        $FailingObjectId = [guid]'23232323-aaaa-aaaa-aaaa-232323232323'
        Mock Get-VBRJob -MockWith { @($RealJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'Orphan1' -BackupId $FailingBackupId -ObjectId $FailingObjectId -ApproxSize 5GB -BackupSize 2GB -ThrowOnGetSourceJob),
                    (script:New-FakeRestorePoint -Name 'Orphan2' -BackupId $FailingBackupId -ObjectId $FailingObjectId -ApproxSize 5GB -BackupSize 2GB -ThrowOnGetSourceJob),
                    (script:New-FakeRestorePoint -Name 'Real'    -ObjectId ([guid]'25252525-aaaa-aaaa-aaaa-252525252521') -ApproxSize 20GB -BackupSize 9GB -SourceJob $RealJob)
                )
            } else { @() }
        }
        Get-VhcJob
        $script:CallCounts.GetSourceJob | Should -Be 2   # one call per group (2 groups), not per point (3 points)
        $SweepLine = $script:LogMessages | Where-Object { $_ -match 'matched via tier 1' } | Select-Object -Last 1
        $SweepLine | Should -Match '\b2 tier-1 lookup failures\b'
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 9
    }

    It 'a group unresolved by tier 1 resolves via tier 2 with one GetBackup() call, applied to the whole group' {
        $CloudJob = script:New-FakeJob -Name 'Linux-01' -TypeToString 'Azure IaaS Backup'
        $SharedBackupId = [guid]'26262626-2626-2626-2626-262626262626'
        $SharedObjectId = [guid]'26262626-aaaa-aaaa-aaaa-262626262626'
        Mock Get-VBRJob -MockWith { @($CloudJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'chain1' -BackupId $SharedBackupId -ObjectId $SharedObjectId -ApproxSize 10GB -BackupSize 4GB -ThrowOnGetSourceJob -BackupParentOrThisName 'Linux-01'),
                    (script:New-FakeRestorePoint -Name 'chain2' -BackupId $SharedBackupId -ObjectId $SharedObjectId -ApproxSize 10GB -BackupSize 6GB -ThrowOnGetSourceJob -BackupParentOrThisName 'Linux-01')
                )
            } else { @() }
        }
        Get-VhcJob
        $script:CallCounts.GetBackup | Should -Be 1
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Linux-01' }
        $Row.OnDiskGB | Should -Be 10   # 4 + 6, both points in the group summed via one tier-2 lookup
    }

    It 'suppresses a tier-2 match for one group even when it is processed before the tier-1-matching group for the same job' {
        $HpeJob = script:New-FakeJob -Name 'HPE Morpheus - Windows - Linux' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($HpeJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    # Stale machine's group appears FIRST. If tier 1 -> tier 2
                    # were interleaved per group instead of two full passes,
                    # this would be accepted before the real tier-1 match
                    # (below) ever ran, since nothing has matched this job yet.
                    (script:New-FakeRestorePoint -Name 'Windows01-stale' -BackupId ([guid]'88888888-8888-8888-8888-888888888882') -ObjectId ([guid]'88888888-8888-8888-8888-888888888882') -ApproxSize 190GB -BackupSize 117GB -ThrowOnGetSourceJob -BackupParentOrThisName 'HPE Morpheus - Windows - Linux'),
                    # Current machine's group, tier-1-resolvable, appears SECOND.
                    (script:New-FakeRestorePoint -Name 'Windows01-current' -BackupId ([guid]'88888888-8888-8888-8888-888888888881') -ObjectId ([guid]'88888888-8888-8888-8888-888888888881') -ApproxSize 190GB -BackupSize 72.99GB -SourceJob $HpeJob)
                )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'HPE Morpheus - Windows - Linux' }
        $Row.OnDiskGB | Should -Be 72.99
    }

    It 'a Replication job resolves via tier 1 when the sweep runs, summing multiple chains like any other job type' {
        $ReplicaJob  = script:New-FakeJob -Name 'Replica_VC_NZGDC01' -TypeToString 'VMware Replication'
        # A non-allowlisted companion job forces $NeedsSweep to $true -
        # 'VMware Replication' is itself on $KnownSafeJobTypes now, so a
        # solo replica job would never trigger the sweep on its own.
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    # Two distinct chains (different BackupId) for the same
                    # replica job - e.g. before/after a repository retarget.
                    (script:New-FakeRestorePoint -Type 'Snapshot' -BackupId ([guid]'29292929-2929-2929-2929-292929292921') -ObjectId ([guid]'30303030-3030-3030-3030-303030303030') -ApproxSize 20GB -BackupSize 5GB -SourceJob $ReplicaJob),
                    (script:New-FakeRestorePoint -Type 'Snapshot' -BackupId ([guid]'29292929-2929-2929-2929-292929292922') -ObjectId ([guid]'30303030-3030-3030-3030-303030303030') -ApproxSize 25GB -BackupSize 4GB -SourceJob $ReplicaJob)
                )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Replica_VC_NZGDC01' }
        $Row.OnDiskGB     | Should -Be 9    # 5 + 4, both chains summed - previously impossible for Replication jobs
        $Row.OriginalSize | Should -Be 25GB # latest chain's ApproxSize, same rule as every other job type
    }
}

```

- [ ] **Step 2: Run all five new tests to verify they fail**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed" 2>&1 | grep -B1 -A8 '\[-\]'`
Expected: all five FAIL. The first three fail because grouping doesn't exist yet (`$script:CallCounts.GetSourceJob`/`$script:CallCounts.GetBackup` will show the current per-point call counts, e.g. `3` and `2` instead of `1`). The fourth fails or passes by chance depending on `Group-Object`'s absence — since there's no grouping yet, this exercises today's per-point two-pass logic, which should already handle this correctly (today's code already does two full passes over individual points); if it passes today, that's fine, it confirms the *invariant* already holds per-point and Task 4 must not break it when switching to groups. The fifth (Replication job) MUST fail today — `Type -eq 'Snapshot'` is still skipped unconditionally, so `OnDiskGB` will be `0`, not `9`.

- [ ] **Step 3: Commit the failing tests**

```bash
git add "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1"
git commit -m "test(jobs): add failing tests for BackupId-grouped tiered matching (ADR 0023)"
```

---

### Task 4: Implement the unified BackupId-grouped matching algorithm

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1:99-250` (the whole sweep body, from `$RestorePointsByJob = @{}` through the closing `if ($NeedsSweep) { ... }` brace)

This is one cohesive replacement — Tier 1 grouping, the `$Tier1MatchedJobIds` snapshot, Tier 2 grouping, and removal of the Replica loop are not meaningfully separable (an intermediate state with only Tier 1 grouped would leave Tier 2 iterating a `$Unresolved` variable that no longer exists).

- [ ] **Step 1: Replace the sweep body**

Replace lines 99-250 of `Get-VhcJob.ps1` (from `$RestorePointsByJob = @{}` through the sweep's closing brace) with:

```powershell
    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
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
            $Groups = $AllRestorePoints | Group-Object -Property BackupId

            # Tier 1: Id-based via GetSourceJob() (+ GetParentJob() walk-up),
            # one call per group, applied to every restore point in it.
            $UnresolvedGroups = [System.Collections.ArrayList]::new()
            foreach ($Group in $Groups) {
                $Representative = $Group.Group[0]

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
                    foreach ($RestorePoint in $Group.Group) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                    $Tier1Matched += $Group.Count
                } else {
                    [void]$UnresolvedGroups.Add($Group)
                    if ($LookupThrew) { $Tier1Failed += $Group.Count }
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
            foreach ($Group in $UnresolvedGroups) {
                $Representative = $Group.Group[0]

                $JobIdKey = $null
                try {
                    $ParentName = $Representative.GetBackup().GetParentOrThis().Name
                    if ($ParentName -and $JobIdByName.ContainsKey($ParentName)) { $JobIdKey = $JobIdByName[$ParentName] }
                } catch {}
                if (-not $JobIdKey) { continue }
                if ($Tier1MatchedJobIds.Contains($JobIdKey)) { continue }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                foreach ($RestorePoint in $Group.Group) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                $Tier2Matched += $Group.Count
            }

            try {
                Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2, $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched) unmatched/orphaned/snapshot"
            } catch {}
            try {
                Write-LogFile "BackupId grouping: $($AllRestorePoints.Count) restore points reduced to $($Groups.Count) groups ($($Groups.Count) tier-1 lookups + $($UnresolvedGroups.Count) tier-2 lookups attempted, vs. $($AllRestorePoints.Count) lookups pre-grouping)"
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
        }
    }
```

Also update the section header comment a few lines above (around line 82-85, unchanged by the block replaced above) to reference the new ADR:

Replace:
```powershell
    # ------------------------------------------------------------------
    # Restore-point matching: performance gate + global sweep
    # (ADR 0021: tiered sweep; ADR 0022: allowlist gate)
    # ------------------------------------------------------------------
```
with:
```powershell
    # ------------------------------------------------------------------
    # Restore-point matching: performance gate + global sweep
    # (ADR 0021: tiered sweep; ADR 0022: allowlist gate;
    #  ADR 0023: BackupId-grouped matching for all job types)
    # ------------------------------------------------------------------
```

Also remove the now-fully-unused `$NonReplicaJobs`/`$ReplicaJobs` assignment lines directly above `$KnownSafeJobTypes`'s closing `)` (Task 2 kept them because the Replica loop this task just deleted was their only remaining reader). Replace:

```powershell
    $NonReplicaJobs = @($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notlike '*Replication*' })
    $ReplicaJobs    = @($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -like '*Replication*' })
    $NeedsSweep     = [bool]($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)
```
with:
```powershell
    $NeedsSweep = [bool]($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)
```

Run: `grep -n 'NonReplicaJobs\|ReplicaJobs' "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1"`
Expected: no matches.

- [ ] **Step 2: Run the new grouping tests to verify they pass**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed" 2>&1 | grep -A2 'BackupId grouping'`
Expected: all five tests in the `BackupId grouping (ADR 0023)` Describe block PASS.

- [ ] **Step 3: Run the full suite**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1'"`
Expected: exactly 3 failures — `'sizes a Replica job via GetLastBackup() + Get-VBRRestorePoint -Backup, not via tier 1/2'` and `'logs a WARNING when a Replica job already carries a tier-1/2-matched restore point before the overwrite'` (both in the now-obsolete `Replica jobs are sized via their own lookup...` Describe block), plus `'never resolves a Type=Snapshot restore point via either tier'`. The other three tests in that same Describe block, and the Sweep resilience block's `'a throw from the Replica-loop "discarded" WARNING...'` test, keep PASSING at this checkpoint — not because the mechanism they were written to exercise still exists, but coincidentally (e.g. the WARNING message the latter checks for is simply never emitted anymore, so its simulated logging failure never triggers). All five new `BackupId grouping (ADR 0023)` tests from Task 3 must now PASS. Task 5 removes all 7 of these Replica/Snapshot-era tests regardless of which are currently failing vs. coincidentally passing, since their premises (the Replica loop, the Type=Snapshot skip) no longer exist in the codebase. Every other test (ISC-1 through ISC-10, Performance gate, Sweep resilience's other two tests, both Tier 1 Describe blocks, Tier 2, Multi-chain summing) must be green.

- [ ] **Step 4: Commit**

```bash
git add "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1"
git commit -m "feat(jobs): replace Type=Snapshot skip and Replica loop with BackupId-grouped matching (ADR 0023)"
```

---

### Task 5: Remove the obsolete Replica-loop tests and one invalidated test

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`

Three things to remove, each because the behavior they test no longer exists in the codebase (not because the tests are wrong to have existed).

- [ ] **Step 1: Remove the entire `'Replica jobs are sized via their own lookup...'` Describe block**

Delete lines 1098-1230 of `Get-VhcJob.Tests.ps1` (line numbers approximate — Task 3's insertion shifted everything below it down; match by content) — the header comment block:
```powershell
# ---------------------------------------------------------------------------
# Replica handling (ADR 0021): Replica jobs are sized via their own
# GetLastBackup() lookup by default, not tier 1/2. If that lookup fails
# outright and a tier-1/2 match already exists for the same Id, that match is
# preserved instead of being replaced with a zeroed-out result.
# ---------------------------------------------------------------------------
```
through the entire `Describe 'Replica jobs are sized via their own lookup, tier 1/2 only as a fallback on failure' { ... }` block and its closing `}`, plus the blank line immediately after it. Replication jobs are now covered by the `BackupId grouping (ADR 0023)` Describe block added in Task 3 (specifically `'a Replication job resolves via tier 1 when the sweep runs, summing multiple chains like any other job type'`), which replaces this block's coverage with the corrected behavior.

- [ ] **Step 2: Remove the obsolete "Replica-loop discarded WARNING" logging-resilience test**

In the `'Sweep resilience: a logging failure does not disable the sweep'` Describe block, delete this test (the message it checks for, `'discarded in favor'`, no longer exists anywhere in `Get-VhcJob.ps1`):

```powershell
    It 'a throw from the Replica-loop "discarded" WARNING log line still applies a real tier-1 match to other jobs' {
        $ReplicaJob  = script:New-FakeJob -Name 'Hyper-V - Replicas' -TypeToString 'Hyper-V Replication' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        Mock Write-LogFile -MockWith {
            if ($Message -match 'discarded in favor') { throw 'Simulated logging failure' }
        }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    # Resolves back to $ReplicaJob itself via tier 1, so the
                    # Replica loop's "had N ... discarded" WARNING fires.
                    (script:New-FakeRestorePoint -ObjectId ([guid]'71717171-7171-7171-7171-717171717171') -ApproxSize 5GB -BackupSize 3GB -SourceJob $ReplicaJob),
                    (script:New-FakeRestorePoint -ObjectId ([guid]'72727272-7272-7272-7272-727272727272') -ApproxSize 20GB -BackupSize 9GB -SourceJob $MorpheusJob)
                )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'MorpheusJob' }
        $Row.OnDiskGB | Should -Be 9
    }

```

(Leave the other two tests in this Describe block — the sweep-summary and sweep-failure logging-resilience tests — unchanged; both log lines still exist.)

- [ ] **Step 3: Remove the invalidated `'never resolves a Type=Snapshot restore point via either tier'` test**

In the `'Tier 2: gated name-based fallback'` Describe block, delete this test — its premise (a restore point with a real, resolvable `SourceJob` still reports `0` purely because `Type -eq 'Snapshot'`) is exactly what ADR 0023 corrects; this restore point now correctly resolves via Tier 1:

```powershell
    It 'never resolves a Type=Snapshot restore point via either tier' {
        # 'VMware Backup' alone would leave $NeedsSweep=$false and this test
        # would pass because the sweep never ran, not because the skip logic
        # under test works - add a non-allowlisted companion job (mirrors
        # Task 6's Replica test) to force the sweep on. The realistic risk
        # this guards is a VMware job in a mixed environment where a stray
        # Snapshot-type point's name happens to resolve to it.
        $VMwareJob   = script:New-FakeJob -Name 'VMware - Domain Controller' -TypeToString 'VMware Backup'
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($VMwareJob, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -Type 'Snapshot' -ObjectId ([guid]'99999999-9999-9999-9999-999999999999') -ApproxSize 100GB -BackupSize 50GB -SourceJob $VMwareJob -BackupParentOrThisName 'VMware - Domain Controller') )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMware - Domain Controller' }
        $Row.OnDiskGB | Should -Be 0
    }

```

- [ ] **Step 4: Run the full file**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1'"`
Expected: `Tests Passed: 43, Failed: 0, Skipped: 0` (45 from Task 2, +5 new from Task 3, -7 removed in this task = 43).

- [ ] **Step 5: Run the full directory-wide Pester tree**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig'"`
Expected: all tests pass, no stub-collision regressions (per the suite's own documented cross-file global-stub-leak risk — single-file green is not sufficient evidence on its own).

- [ ] **Step 6: Commit**

```bash
git add "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1"
git commit -m "test(jobs): remove Replica-loop and Type=Snapshot-skip tests superseded by ADR 0023"
```

---

### Task 6: Update the scratch validation script for live-lab proof

**Files:**
- Modify: `Test-JobSizingRestorePointMatching.ps1` (repo root — explicitly NOT part of the shipped module; this task updates it in place, does not move it)

This script is how ADR 0021 was originally validated, and per the design doc's Validation Plan, it must run against live VBR labs before this change lands in production `Get-VhcJob.ps1`. Its embedded "new approach" simulation (lines 166-424) currently mirrors the *old* per-point, Replica-special-cased algorithm — it must be updated to mirror the new BackupId-grouped, unified algorithm, or the script's own old-vs-new comparison won't actually validate ADR 0023's change. This task updates the script; running it against live infrastructure (Step 4 below) is a manual step for whoever has lab access, not something this plan can execute.

- [ ] **Step 1: Replace the Tier 1/Tier 2 loops with the grouped equivalent, and remove the Replica hybrid block**

Replace lines 166-424 of `Test-JobSizingRestorePointMatching.ps1` (from the `# NEW approach: one global restore-point sweep...` comment through the end of the `# Replica hybrid: ...` block) with:

```powershell
# ---------------------------------------------------------------------------
# NEW approach (ADR 0023): one global restore-point sweep, grouped by
# BackupId (confirmed scoped to one protected object's chain within one
# job), resolved in two full passes over GROUPS instead of individual
# points.
#   Pass 1 (Tier 1): GetSourceJob() (+ GetParentJob() walk-up for
#           per-machine child jobs under policy-driven platforms) on ONE
#           representative per group, applied to every point in the group.
#   Pass 2 (Tier 2): only for GROUPS tier 1 couldn't resolve at all -
#           GetBackup().GetParentOrThis().Name on the representative,
#           matched against $Jobs by name, gated the same way as before
#           (job must have zero tier-1 matches, checked only after the
#           FULL tier-1 pass over every group completes).
# Replica-type jobs are no longer special-cased - their restore points
# (Type=Snapshot) are grouped and resolved through this same pipeline.
# ---------------------------------------------------------------------------
Write-Host "Running global restore-point sweep (one-time)..." -ForegroundColor Cyan
$fetchStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$allRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)
$fetchStopwatch.Stop()
Write-Host "Found $($allRestorePoints.Count) restore points server-wide."

$restorePointsByJob = @{}
$tier1MatchedJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
$tier1Matched       = 0

$knownJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($j in @($Jobs)) { [void]$knownJobIds.Add($j.Id.ToString()) }
$tier1UnknownIdCount = 0

$groupStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$backupIdGroups = $allRestorePoints | Group-Object -Property BackupId
$groupStopwatch.Stop()
Write-Host ("Grouped {0} restore points into {1} BackupId groups in {2}s." -f $allRestorePoints.Count, $backupIdGroups.Count, [Math]::Round($groupStopwatch.Elapsed.TotalSeconds, 3))

$getSourceJobOkMs = 0.0;    $getSourceJobOkCount = 0
$getSourceJobNullMs = 0.0;  $getSourceJobNullCount = 0
$getSourceJobThrowMs = 0.0; $getSourceJobThrowCount = 0
$getParentJobMs = 0.0;      $getParentJobCount = 0

$tier1UnresolvedGroups = [System.Collections.ArrayList]::new()

$tier1LoopStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($group in $backupIdGroups) {
    $representative = $group.Group[0]
    $sourceJob = $null

    $callSw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $sourceJob = $representative.GetSourceJob()
        $callSw.Stop()
        if ($null -ne $sourceJob) {
            $getSourceJobOkMs += $callSw.Elapsed.TotalMilliseconds
            $getSourceJobOkCount++
        } else {
            $getSourceJobNullMs += $callSw.Elapsed.TotalMilliseconds
            $getSourceJobNullCount++
        }
    } catch {
        $callSw.Stop()
        $getSourceJobThrowMs += $callSw.Elapsed.TotalMilliseconds
        $getSourceJobThrowCount++
        $sourceJob = $null
    }

    $jobIdKey = $null
    if ($null -ne $sourceJob) {
        $parentSw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $parentJob = $sourceJob.GetParentJob()
            if ($null -ne $parentJob) { $sourceJob = $parentJob }
        } catch {}
        $parentSw.Stop()
        $getParentJobMs += $parentSw.Elapsed.TotalMilliseconds
        $getParentJobCount++

        try { $jobIdKey = $sourceJob.Id.ToString() } catch {}

        if ($jobIdKey -and -not $knownJobIds.Contains($jobIdKey)) {
            $tier1UnknownIdCount++
            $jobIdKey = $null
        }
    }

    if ($jobIdKey) {
        if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
            $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
        }
        foreach ($rp in $group.Group) { [void]$restorePointsByJob[$jobIdKey].Add($rp) }
        [void]$tier1MatchedJobIds.Add($jobIdKey)
        $tier1Matched += $group.Count
    } else {
        [void]$tier1UnresolvedGroups.Add($group)
    }
}
$tier1LoopStopwatch.Stop()

$tier2Matched         = 0
$tier2Suppressed      = 0
$unmatched            = 0
$getBackupMs          = 0.0
$getParentOrThisMs    = 0.0
$tier2SuppressedDetails = [System.Collections.ArrayList]::new()

$tier2LoopStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($group in $tier1UnresolvedGroups) {
    $representative = $group.Group[0]
    $jobIdKey = $null
    $backup = $null
    try {
        $backupSw = [System.Diagnostics.Stopwatch]::StartNew()
        $backup = $representative.GetBackup()
        $backupSw.Stop()
        $getBackupMs += $backupSw.Elapsed.TotalMilliseconds

        $parentSw = [System.Diagnostics.Stopwatch]::StartNew()
        $parentBackupName = $backup.GetParentOrThis().Name
        $parentSw.Stop()
        $getParentOrThisMs += $parentSw.Elapsed.TotalMilliseconds

        if ($parentBackupName -and $jobIdByName.ContainsKey($parentBackupName)) {
            $jobIdKey = $jobIdByName[$parentBackupName]
        }
    } catch {}

    if (-not $jobIdKey) {
        $unmatched += $group.Count
        continue
    }

    if ($tier1MatchedJobIds.Contains($jobIdKey)) {
        $tier2Suppressed += $group.Count
        $backupId = $null
        try { $backupId = $backup.Id } catch {}
        [void]$tier2SuppressedDetails.Add([PSCustomObject]@{
            RestorePointName = $representative.Name
            CreationTime     = $representative.CreationTimeUtc
            BackupId         = $backupId
            ResolvedJobId    = $jobIdKey
            ResolvedJobName  = if ($jobNameById.ContainsKey($jobIdKey)) { $jobNameById[$jobIdKey] } else { '(unknown)' }
        })
        continue
    }

    if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
        $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
    }
    foreach ($rp in $group.Group) { [void]$restorePointsByJob[$jobIdKey].Add($rp) }
    $tier2Matched += $group.Count
}
$tier2LoopStopwatch.Stop()

$sweepTotalSeconds = $fetchStopwatch.Elapsed.TotalSeconds + $groupStopwatch.Elapsed.TotalSeconds + $tier1LoopStopwatch.Elapsed.TotalSeconds + $tier2LoopStopwatch.Elapsed.TotalSeconds

Write-Host ""
Write-Host "=== Sweep timing breakdown ===" -ForegroundColor Magenta
Write-Host ("Fetch Get-VBRRestorePoint ({0} points):          {1}s" -f $allRestorePoints.Count, [Math]::Round($fetchStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("Group by BackupId ({0} points -> {1} groups):    {2}s" -f $allRestorePoints.Count, $backupIdGroups.Count, [Math]::Round($groupStopwatch.Elapsed.TotalSeconds, 3))
Write-Host ("Tier 1 loop total ({0} group lookups):            {1}s" -f $backupIdGroups.Count, [Math]::Round($tier1LoopStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("  GetSourceJob() succeeded ({0} calls):           {1}s" -f $getSourceJobOkCount, [Math]::Round($getSourceJobOkMs / 1000, 2))
Write-Host ("  GetSourceJob() returned null ({0} calls):       {1}s" -f $getSourceJobNullCount, [Math]::Round($getSourceJobNullMs / 1000, 2))
Write-Host ("  GetSourceJob() threw ({0} calls):               {1}s" -f $getSourceJobThrowCount, [Math]::Round($getSourceJobThrowMs / 1000, 2))
Write-Host ("  GetParentJob() walk-up ({0} calls):             {1}s" -f $getParentJobCount, [Math]::Round($getParentJobMs / 1000, 2))
Write-Host ("Tier 2 loop total ({0} group lookups):             {1}s" -f $tier1UnresolvedGroups.Count, [Math]::Round($tier2LoopStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("  GetBackup():                                    {0}s" -f [Math]::Round($getBackupMs / 1000, 2))
Write-Host ("  GetParentOrThis().Name:                         {0}s" -f [Math]::Round($getParentOrThisMs / 1000, 2))
Write-Host ("Sweep grand total:                                {0}s" -f [Math]::Round($sweepTotalSeconds, 2))
$sweepStopwatch = [PSCustomObject]@{ Elapsed = [TimeSpan]::FromSeconds($sweepTotalSeconds) }

Write-Host ""
Write-Host "Matched via tier 1 (GetSourceJob/GetParentJob):        $tier1Matched"
Write-Host ('Tier 1 resolved to an Id NOT in $Jobs (rerouted to tier 2): ' + $tier1UnknownIdCount)
Write-Host "Matched via tier 2 (GetParentOrThis().Name):           $tier2Matched"
Write-Host "Tier 2 suppressed (job already had tier-1 matches):    $tier2Suppressed"
Write-Host "Still unmatched (orphaned/imported/out-of-scope):       $unmatched"

if ($tier2SuppressedDetails.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Tier 2 suppressed restore points (job already had tier-1 matches) ===" -ForegroundColor Magenta
    Write-Host "Is this a stale/out-of-scope machine, or a second valid chain for the same job being discarded?"
    $tier2SuppressedDetails | Sort-Object ResolvedJobId, CreationTime | Format-Table -AutoSize

    Write-Host ""
    Write-Host "=== Tier 1 matches for those same jobs, for direct comparison ===" -ForegroundColor Magenta
    Write-Host "Same Name/BackupId as a suppressed row above => suppression is discarding a real extra chain for a machine already counted."
    Write-Host "Different Name/BackupId => suppression is correctly protecting against an unrelated/stale machine."
    $affectedJobIds = @($tier2SuppressedDetails.ResolvedJobId | Select-Object -Unique)
    foreach ($jid in $affectedJobIds) {
        $jobName = if ($jobNameById.ContainsKey($jid)) { $jobNameById[$jid] } else { $jid }
        Write-Host ""
        Write-Host ("  -- $jobName (tier-1 matched, currently kept) --")
        if ($restorePointsByJob.ContainsKey($jid)) {
            $restorePointsByJob[$jid] |
                Select-Object Name, CreationTimeUtc, BackupId |
                Sort-Object CreationTimeUtc |
                Format-Table -AutoSize
        } else {
            Write-Host "    (no tier-1 matches recorded for this job Id)"
        }
    }
}

# ---------------------------------------------------------------------------
# BackupId grouping-assumption audit (ADR 0023): the pipeline above only
# calls GetSourceJob()/GetBackup() on ONE representative per group and
# trusts every member resolves identically. This section is the empirical
# proof of that trust - for every multi-point group, call GetSourceJob() on
# EVERY member (not just the representative) and confirm they all agree.
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== BackupId grouping-assumption audit ===" -ForegroundColor Cyan
$groupingViolations = [System.Collections.Generic.List[string]]::new()
$multiPointGroups = @($backupIdGroups | Where-Object { $_.Count -gt 1 })
Write-Host "Total groups: $($backupIdGroups.Count), multi-point groups: $($multiPointGroups.Count)"
foreach ($group in $multiPointGroups) {
    $resolvedOutcomes = $group.Group | ForEach-Object {
        try {
            $sj = $_.GetSourceJob()
            if ($null -ne $sj) { $sj.Id.ToString() } else { '<null>' }
        } catch {
            '<throw>'
        }
    }
    $distinct = $resolvedOutcomes | Select-Object -Unique
    if ($distinct.Count -gt 1) {
        $groupingViolations.Add("BackupId $($group.Name): $($group.Count) points resolved to $($distinct.Count) DIFFERENT outcomes: $($distinct -join ', ')")
    }
}
if ($groupingViolations.Count -gt 0) {
    Write-Host "VIOLATIONS FOUND - grouping assumption does NOT hold in this environment:" -ForegroundColor Red
    $groupingViolations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} else {
    Write-Host "No violations - every multi-point BackupId group resolved identically ($($multiPointGroups.Count) groups checked)." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Grouping performance measurement ===" -ForegroundColor Cyan
Write-Host ("{0} restore points reduced to {1} BackupId groups ({2}% fewer GetSourceJob()/GetBackup() calls than the pre-grouping, one-call-per-point approach)." -f $allRestorePoints.Count, $backupIdGroups.Count, [math]::Round(100 - ($backupIdGroups.Count / [math]::Max($allRestorePoints.Count, 1) * 100), 1))
Write-Host "NOTE: run this against an environment with a Storage-Snapshot Backup population specifically (not just VM replication) to measure the reduction factor against the population that most likely caused ADR 0021's original 5,461-throw measurement (see ADR 0023)."
```

Note what this removes relative to the current script: the `$snapshotSkipped`/`$tier2SnapshotSkipped` counters and their "Skipped - Type=Snapshot" log lines (Type is no longer treated specially — Replica jobs' Snapshot-type points now flow through the same grouped Tier 1/2 pipeline), and the entire "Replica hybrid" block (the `$replicaJobs = @($Jobs | Where-Object { $_.TypeToString -like "*Replication*" })` loop and its `Write-Host "Routing N Replica-type job(s)..."` — no longer needed, since Replica jobs are no longer special-cased anywhere in this script either).

- [ ] **Step 2: Confirm the rest of the script (Get-NewJobSizing, the comparison loop) still works unmodified**

Run: `grep -n 'replicaJobs\|snapshotSkipped\|tier2SnapshotSkipped' Test-JobSizingRestorePointMatching.ps1`
Expected: no matches — confirms the removed variables aren't referenced anywhere later in the script (e.g. `Get-NewJobSizing` at line ~499 reads from `$restorePointsByJob`, which both the old and new code populate identically in shape, so it needs no changes).

- [ ] **Step 3: Add the note this task cannot execute itself**

At the top of the script's existing comment-based help (do not remove any existing content there), add:

```powershell
    .Note (2026-08-22, ADR 0023)
        Updated to validate BackupId-grouped matching before it lands in
        Get-VhcJob.ps1. MUST be run against live VBR labs by someone with
        access before this change is considered validated - in particular,
        against an environment with a Storage-Snapshot Backup population,
        which no lab used for ADR 0021's original validation had, and which
        is the population that most likely caused the original "100% throw
        rate" measurement (see ADR 0023).
```

- [ ] **Step 4: Commit**

```bash
git add Test-JobSizingRestorePointMatching.ps1
git commit -m "test(jobs): update scratch validation script for BackupId-grouped matching (ADR 0023)"
```

- [ ] **Step 5: Manual checkpoint — do not skip**

This step cannot be automated by whoever executes this plan unless they have live VBR lab access. Before merging this change:
1. Run the updated `Test-JobSizingRestorePointMatching.ps1` against at least one live VBR environment.
2. Confirm the grouping-assumption audit reports zero violations.
3. Confirm old-vs-new sizing comparison shows zero regressions, including for any Replication jobs present.
4. If possible, run it against an environment with Storage-Snapshot Backups specifically, and record the actual call-count reduction.

If this step surfaces a grouping violation (a `BackupId` group whose members resolve to different jobs), stop — do not proceed to Task 7. That would invalidate the core safety assumption this whole design rests on, and needs to go back through the design doc before any further code changes.

---

### Task 7: Final verification

**Files:** None (verification only).

- [ ] **Step 1: Full directory-wide Pester run**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig'"`
Expected: all tests pass.

- [ ] **Step 2: C# build sanity check**

This change is pure PowerShell — no C# files are touched by this plan — but `Get-VhcJob.ps1`'s output feeds the C# reporting pipeline, so confirm nothing else regressed:

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: `0 errors`. If the auto-increment version bump touches `vHC/HC_Reporting/VeeamHealthCheck.csproj`, revert it (`git checkout -- vHC/HC_Reporting/VeeamHealthCheck.csproj`) before committing anything further — that bump is a build side effect, not part of this change.

- [ ] **Step 3: Confirm no dangling references to removed identifiers**

Run: `grep -rn 'NonReplicaJobs\|ReplicaJobs\|HadPriorMatch\|LookupFailed' "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1" "vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1"`
Expected: no matches in either file.

- [ ] **Step 4: Confirm Task 6's manual live-lab checkpoint was completed**

Do not consider this plan complete until Task 6 Step 5 has been performed against at least one live environment and reported zero grouping violations. If it hasn't happened yet, stop here and flag it rather than proceeding to open/update a PR.
