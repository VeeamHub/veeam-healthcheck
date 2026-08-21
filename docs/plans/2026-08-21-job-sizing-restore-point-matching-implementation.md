# Job Sizing: Tiered Restore-Point Matching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `Get-VhcJob.ps1`'s per-job `GetLastBackup()` sizing with the tiered, gated restore-point sweep from [ADR 0021](../../adr/0021-tiered-restore-point-sweep-for-job-sizing.md), gated behind the [ADR 0022](../../adr/0022-allowlist-gate-for-restore-point-sweep.md) allowlist, fixing the 0 MB / 0 GB Source Size / Est. On Disk GB bug for HPE Morpheus, Nutanix AHV, oVirt KVM, Proxmox, public cloud (AWS/Azure/GCE), and affected Backup Copy jobs, with zero behavior change for environments that don't need it.

**Architecture:** `Get-VhcJob.ps1` gains an allowlist-gated performance check before its main per-job loop. When triggered, a single global `Get-VBRRestorePoint` sweep resolves every restore point to its owning job through two tiers — Id-based via `GetSourceJob()`/`GetParentJob()` (validated against `$Jobs`'s own Ids), then a gated name-based fallback via `GetBackup().GetParentOrThis()` — bucketing results into a dictionary the main loop consults instead of calling `GetLastBackup()` per job. Replica-type jobs are sized via the original per-job method regardless of whether the sweep runs; environments where every job is a proven-safe type skip the sweep entirely and keep today's exact behavior.

**Tech Stack:** PowerShell 5.1/7, Veeam.Backup.PowerShell SDK (`Get-VBRJob`, `Get-VBRRestorePoint`, `Get-VBRBackup`), Pester v5 for tests.

---

## File Structure

- **Modify:** `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`
  Adds the allowlist gate, the tiered sweep (tier 1 Id-based + `$KnownJobIds` validation, tier 2 gated name-based, Replica handling), and switches the main loop's restore-point fetch to consult the sweep's dictionary when the gate says a sweep ran.
- **Modify:** `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
  Fixes the `Get-VBRRestorePoint` stub to tolerate `-WarningAction`, adds `New-FakeJob` / `New-FakeBackup` / `New-FakeRestorePoint` factories, and adds one `Describe` block per behavior (gate, tier 1, walk-up, `$KnownJobIds` validation, tier 2 + gating, Replica handling + multi-chain summing).
- **Modify:** `docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md`
  Flip the `Status:` header from `Proposed` to `Implemented` once every task below is committed (final step of Task 6).

No new files. ADRs 0021/0022 and `CONTEXT.md` already exist and are `Status: Accepted`.

---

## Shared Test Convention (read before Task 1)

Every task below extends the SAME `Get-VhcJob.Tests.ps1` file using the conventions the file already established (see `ISC-*` Describe blocks): stub Veeam cmdlets as `global:` functions in `BeforeAll` only if not already present, dot-source `Write-LogFile.ps1` then `Get-VhcJob.ps1`, and reference a mocked command's own parameter names directly inside `-MockWith` scriptblocks without declaring a `param()` block (Pester binds them automatically — see the existing `Mock Write-LogFile -MockWith { if ($LogLevel -eq 'WARNING') { ... } }` pattern at `Get-VhcJob.Tests.ps1:217-219`).

Sizing results are observed by mocking `Export-VhciCsv` (the function's final output step) to capture every row written to `_Jobs.csv`:

```powershell
$script:CapturedJobRows = @()
Mock Export-VhciCsv -MockWith {
    if ($FileName -eq '_Jobs.csv' -and $InputObject) {
        $script:CapturedJobRows += @($InputObject)
    }
}
```

Using `+=` (append, not overwrite) makes this correct regardless of whether Pester invokes the mock once per pipeline item or once with the whole array — either way `$script:CapturedJobRows` contains every row after `Get-VhcJob` returns.

**Byte vs. GB units, read carefully:** `New-FakeRestorePoint`'s `-ApproxSize`/`-BackupSize` parameters are raw byte counts (PowerShell's `5GB` literal *is* a byte count: `5368709120`). The exported row's `OnDiskGB` property is already divided by `1GB` (compare against a plain number, e.g. `2`); the `OriginalSize` property is **not** divided (compare against the raw byte literal, e.g. `5GB`). Getting this backwards is the single most likely source of a wrong-but-passing or right-but-failing test in this plan.

---

## Task 1: Performance gate — trigger the sweep for unrecognized job types, skip it for known-safe ones

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`

- [x] **Step 1: Add test infrastructure — fix the `Get-VBRRestorePoint` stub and add `New-FakeJob`**

In `Get-VhcJob.Tests.ps1`, replace the existing stub (lines 37-39):

```powershell
    if (-not (Get-Command Get-VBRRestorePoint -ErrorAction SilentlyContinue)) {
        function global:Get-VBRRestorePoint { param($Backup) }
    }
```

with:

```powershell
    if (-not (Get-Command Get-VBRRestorePoint -ErrorAction SilentlyContinue)) {
        function global:Get-VBRRestorePoint {
            [CmdletBinding()]
            param($Backup)
        }
    }
```

`[CmdletBinding()]` adds `-WarningAction` (and other common parameters) so the stub tolerates the calls the new sweep code will make — without this, any test that reaches the sweep throws `A parameter cannot be found that matches parameter name 'WarningAction'`.

Immediately after the existing `New-FakeStandaloneBackup` function (ends at line 136, before the `# Dot-source Write-LogFile...` comment at line 138), add:

```powershell
    # Fake-job factory for the restore-point matching tests. Produces a
    # CBackupJob-shaped object carrying every property the main loop's
    # Select-Object projection touches, plus GetParentJob()/GetLastBackup()
    # ScriptMethods parameterized for the sweep/tier/Replica test cases.
    function script:New-FakeJob {
        param(
            [string]$Name = 'FakeJob',
            [guid]$Id = [guid]::NewGuid(),
            [string]$TypeToString = 'VMware Backup',
            $ParentJob = $null,
            [switch]$ThrowOnGetParentJob,
            $LastBackup = $null,
            [switch]$ThrowOnGetLastBackup
        )
        $ParentJobCapture       = $ParentJob
        $ThrowParentJobCapture  = [bool]$ThrowOnGetParentJob
        $LastBackupCapture      = $LastBackup
        $ThrowLastBackupCapture = [bool]$ThrowOnGetLastBackup

        $Job = [PSCustomObject]@{
            Id                  = $Id
            Name                = $Name
            JobType             = 'Backup'
            SheduleEnabledTime  = $null
            ScheduleOptions     = $null
            IsScheduleEnabled   = $true
            TypeToString        = $TypeToString
            Info                = [PSCustomObject]@{
                PwdKeyId           = $null
                IncludedSize       = 0
                TargetRepositoryId = [PSCustomObject]@{ Guid = [guid]::Empty }
            }
            Options             = [PSCustomObject]@{
                BackupStorageOptions = [PSCustomObject]@{ RetainCycles = 7 }
                BackupTargetOptions  = [PSCustomObject]@{
                    Algorithm                       = 'Increment'
                    FullBackupScheduleKind          = $null
                    FullBackupDays                  = $null
                    TransformFullToSyntethic        = $false
                    TransformIncrementsToSyntethic  = $false
                    TransformToSyntethicDays        = $null
                }
                JobOptions  = [PSCustomObject]@{ RunManually = $false }
                gfspolicy   = [PSCustomObject]@{
                    weekly  = [PSCustomObject]@{ IsEnabled = $false; KeepBackupsForNumberOfWeeks  = 0 }
                    Monthly = [PSCustomObject]@{ IsEnabled = $false; KeepBackupsForNumberOfMonths = 0 }
                    yearly  = [PSCustomObject]@{ IsEnabled = $false; KeepBackupsForNumberOfYears  = 0 }
                }
            }
            BackupStorageOptions = [PSCustomObject]@{
                RetentionType                  = 'Cycles'
                RetainCycles                   = 7
                RetainDaysToKeep               = 7
                RetainDays                     = 14
                EnableDeletedVmDataRetention   = $false
                CompressionLevel               = 5
                EnableDeduplication            = $true
                StgBlockSize                   = 'KbBlockSize1024'
                EnableIntegrityChecks          = $false
                UseSpecificStorageEncryption   = $false
                StorageEncryptionEnabled       = $false
                KeepFirstFullBackup            = $false
                EnableFullBackup               = $false
                BackupIsAttached               = $true
            }
            VssOptions = [PSCustomObject]@{
                GuestFSIndexingType   = 'None'
                VssSnapshotOptions    = [PSCustomObject]@{
                    Enabled                       = $false
                    ApplicationProcessingEnabled  = $false
                    IgnoreErrors                  = $false
                }
                GuestFSIndexingOptions = [PSCustomObject]@{ IsEnabled = $false }
            }
        }
        $Job | Add-Member -MemberType ScriptMethod -Name GetParentJob -Value {
            if ($ThrowParentJobCapture) { throw 'GetParentJob failed' }
            return $ParentJobCapture
        }.GetNewClosure()
        $Job | Add-Member -MemberType ScriptMethod -Name GetLastBackup -Value {
            if ($ThrowLastBackupCapture) { throw 'GetLastBackup failed' }
            return $LastBackupCapture
        }.GetNewClosure()
        return $Job
    }
```

- [x] **Step 2: Write the failing test**

Append a new `Describe` block at the end of `Get-VhcJob.Tests.ps1`:

```powershell
# ---------------------------------------------------------------------------
# Performance gate (ADR 0022): sweep runs only when a job's TypeToString
# isn't on the proven-safe allowlist.
# ---------------------------------------------------------------------------
Describe 'Performance gate: sweep triggers on unrecognized job types' {

    BeforeEach {
        $script:UnscopedCalls   = 0
        $script:ScopedCalls     = 0
        $script:CapturedJobRows = @()
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
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) { $script:UnscopedCalls++ } else { $script:ScopedCalls++ }
            @()
        }
    }

    It 'calls Get-VBRRestorePoint without -Backup exactly once when a non-allowlisted job type is present' {
        Mock Get-VBRJob -MockWith {
            @( (script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup') )
        }
        Get-VhcJob
        $script:UnscopedCalls | Should -Be 1
    }

    It 'never calls Get-VBRRestorePoint without -Backup when every job is a known-safe type' {
        Mock Get-VBRJob -MockWith {
            @(
                (script:New-FakeJob -Name 'VMwareJob' -TypeToString 'VMware Backup' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })),
                (script:New-FakeJob -Name 'HyperVJob' -TypeToString 'Hyper-V Backup' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() }))
            )
        }
        Get-VhcJob
        $script:UnscopedCalls | Should -Be 0
        $script:ScopedCalls   | Should -Be 2
    }

    It 'produces correct OnDiskGB and OriginalSize via the old per-job method when the gate is off' {
        Mock Get-VBRJob -MockWith {
            @( (script:New-FakeJob -Name 'VMwareJob' -TypeToString 'VMware Backup' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })) )
        }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -ne $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'11111111-1111-1111-1111-111111111111') -ApproxSize 5GB -BackupSize 2GB) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMwareJob' }
        $Row.OnDiskGB     | Should -Be 2
        $Row.OriginalSize | Should -Be 5GB
    }
}
```

This references `New-FakeRestorePoint`, which does not exist yet — add it now (it's needed by every remaining task, so it belongs in this step alongside `New-FakeJob`):

```powershell
    # Fake-backup factory backing New-FakeRestorePoint's GetBackup() -
    # GetParentOrThis() chain (tier 2's resolution path).
    function script:New-FakeBackup {
        param(
            [string]$ParentOrThisName = 'FakeJob',
            [switch]$ThrowOnGetParentOrThis
        )
        $NameCapture  = $ParentOrThisName
        $ThrowCapture = [bool]$ThrowOnGetParentOrThis
        $Backup = [PSCustomObject]@{}
        $Backup | Add-Member -MemberType ScriptMethod -Name GetParentOrThis -Value {
            if ($ThrowCapture) { throw 'GetParentOrThis failed' }
            [PSCustomObject]@{ Name = $NameCapture }
        }.GetNewClosure()
        return $Backup
    }

    # Fake restore-point factory. GetSourceJob()/GetBackup() are
    # parameterized directly (no need to hand-build closures at each call
    # site) to cover every tier-1/tier-2 resolution path under test.
    function script:New-FakeRestorePoint {
        param(
            [string]$Name = 'FakeRestorePoint',
            [string]$Type = 'Increment',
            [guid]$ObjectId = [guid]::NewGuid(),
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

        $RestorePoint = [PSCustomObject]@{
            Name            = $Name
            Type            = $Type
            ObjectId        = $ObjectId
            CreationTimeUtc = $CreationTimeUtc
            ApproxSize      = $ApproxSize
            BackupSizeValue = $BackupSize
        }
        $RestorePoint | Add-Member -MemberType ScriptMethod -Name GetSourceJob -Value {
            if ($ThrowSourceJobCapture) { throw 'GetSourceJob failed' }
            return $SourceJobCapture
        }.GetNewClosure()
        $RestorePoint | Add-Member -MemberType ScriptMethod -Name GetBackup -Value {
            if ($ThrowGetBackupCapture) { throw 'GetBackup failed' }
            return $FakeBackup
        }.GetNewClosure()
        $RestorePoint | Add-Member -MemberType ScriptMethod -Name GetStorage -Value {
            [PSCustomObject]@{ Stats = [PSCustomObject]@{ BackupSize = $this.BackupSizeValue } }
        }
        return $RestorePoint
    }
```

Add both of these directly after `New-FakeJob` from Step 1.

- [x] **Step 3: Run the tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: The new `Describe 'Performance gate...'` block's three tests FAIL — `$script:UnscopedCalls` stays `0` in the first test (current code never calls `Get-VBRRestorePoint` without `-Backup`), and the third test's `$Row` may be `$null` or the assertions otherwise fail, because `$NeedsSweep` doesn't exist yet and `Export-VhciCsv` isn't being fed rows via the path this test expects. All existing `ISC-*` tests still PASS (untouched so far).

- [x] **Step 4: Implement the gate**

In `Get-VhcJob.ps1`, replace (current lines 80-94; the replace is driven by matched text, not line numbers, so this reference is for orientation only):

```powershell
    Invoke-VhciJobSubCollectors -Jobs @($Jobs)

    # ------------------------------------------------------------------
    # Main VBR job processing loop - restore point size calculation
    # ------------------------------------------------------------------
    [System.Collections.ArrayList]$AllJobs = @()

    foreach ($Job in @($Jobs)) {
        try {
            $LastBackup    = $Job.GetLastBackup()
            $RestorePoints = @()
            if ($null -ne $LastBackup) {
                $RestorePoints = Get-VBRRestorePoint -Backup $LastBackup
            }
            $TotalOnDiskGB = 0
```

with:

```powershell
    Invoke-VhciJobSubCollectors -Jobs @($Jobs)

    # ------------------------------------------------------------------
    # Restore-point matching: performance gate + global sweep
    # (ADR 0021: tiered sweep; ADR 0022: allowlist gate)
    # ------------------------------------------------------------------
    $KnownSafeJobTypes = @(
        'VMware Backup',
        'Hyper-V Backup',
        'Windows Agent Backup',
        'Windows Agent Policy',
        'Linux Agent Backup',
        'Cloud Director Backup'
    )

    $NonReplicaJobs = @($Jobs | Where-Object { $_.TypeToString -notlike '*Replication*' })
    $NeedsSweep     = [bool]($NonReplicaJobs | Where-Object { $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)

    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
        try {
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)
            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide"
        } catch {
            Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR"
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
        }
    }

    # ------------------------------------------------------------------
    # Main VBR job processing loop - restore point size calculation
    # ------------------------------------------------------------------
    [System.Collections.ArrayList]$AllJobs = @()

    foreach ($Job in @($Jobs)) {
        try {
            $RestorePoints = @()
            if ($NeedsSweep) {
                $JobIdKey = $Job.Id.ToString()
                if ($RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePoints = $RestorePointsByJob[$JobIdKey]
                }
            } else {
                $LastBackup = $Job.GetLastBackup()
                if ($null -ne $LastBackup) {
                    $RestorePoints = Get-VBRRestorePoint -Backup $LastBackup
                }
            }
            $TotalOnDiskGB = 0
```

- [x] **Step 5: Run the tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: All three new tests PASS.

- [x] **Step 6: Run the full existing suite to confirm no regressions**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: All `ISC-1` through `ISC-10` tests still PASS (their surviving standalone jobs carry `TypeToString = 'Agent'`, which is not on the allowlist, so `$NeedsSweep` becomes `$true` for those tests — confirm the sweep's empty body doesn't throw, and that these jobs' `Info.IncludedSize = 0` fallback still yields the same `0` On-Disk GB / Source Size these tests already implicitly rely on).

- [x] **Step 7: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "feat(jobs): add allowlist-gated performance check before restore-point sweep

ADR 0022: skip the sweep entirely when every job's TypeToString is a
proven-safe type; trigger it once and route through a new dictionary
lookup otherwise. Sweep body is currently a bare fetch - tier 1/2
matching logic lands in follow-up commits."
```

---

## Task 2: Tier 1 — direct Id-based match via `GetSourceJob()`

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`

- [x] **Step 1: Write the failing test**

Append to the `Describe 'Performance gate...'` block from Task 1 a new sibling `Describe`:

```powershell
# ---------------------------------------------------------------------------
# Tier 1 (ADR 0021): Id-based resolution via GetSourceJob().
# ---------------------------------------------------------------------------
Describe 'Tier 1: Id-based match via GetSourceJob()' {

    BeforeEach {
        $script:CapturedJobRows = @()
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

    It 'buckets a restore point under the job GetSourceJob() resolves to' {
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'22222222-2222-2222-2222-222222222222') -ApproxSize 190GB -BackupSize 72.99GB -SourceJob $MorpheusJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'MorpheusJob' }
        $Row.OnDiskGB     | Should -Be 72.99
        $Row.OriginalSize | Should -Be 190GB
    }
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: FAIL — `$Row.OnDiskGB` is `0` (or `$Row` is present but zeroed), because the sweep's `try` block only fetches and logs; nothing buckets restore points into `$RestorePointsByJob` yet.

- [x] **Step 3: Implement tier 1's direct match**

In `Get-VhcJob.ps1`, replace:

```powershell
    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
        try {
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)
            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide"
        } catch {
```

with:

```powershell
    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
        try {
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)

            $Tier1Matched = 0
            foreach ($RestorePoint in $AllRestorePoints) {
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch {}
                if ($null -eq $SourceJob) { continue }

                $JobIdKey = $SourceJob.Id.ToString()
                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier1Matched++
            }

            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide, $Tier1Matched matched via tier 1"
        } catch {
```

- [x] **Step 4: Run the test to verify it passes**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: PASS. Also re-confirm Task 1's three tests and all `ISC-*` tests still PASS.

- [x] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "feat(jobs): tier 1 - bucket restore points by GetSourceJob() Id

Fixes the encrypted/plug-in-backed platform 0/0 bug (HPE Morpheus,
Nutanix AHV, oVirt KVM, Proxmox) for jobs where GetSourceJob() resolves
directly to the job Get-VBRJob returns."
```

---

## Task 3: Tier 1 — `GetParentJob()` walk-up for per-machine child jobs

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`

- [x] **Step 1: Write the failing tests**

Add to `Describe 'Tier 1: Id-based match via GetSourceJob()'`:

```powershell
    It 'walks up to the parent job when GetSourceJob() resolves to a per-machine child job' {
        # 'HPE Morpheus VME Backup' - not on $KnownSafeJobTypes, so this alone
        # forces $NeedsSweep=$true. (Managed Agent jobs also use this
        # per-machine-child pattern, but Windows/Linux Agent types ARE on the
        # allowlist - using one here would leave the sweep untriggered and
        # this test would pass for the wrong reason, whatever it asserted.)
        $PolicyJob = script:New-FakeJob -Name 'HPE Morpheus - Windows - Linux' -TypeToString 'HPE Morpheus VME Backup'
        $ChildJob  = script:New-FakeJob -Name 'HPE Morpheus - Windows - Linux\Windows01' -TypeToString 'HPE Morpheus VME Backup' -ParentJob $PolicyJob
        Mock Get-VBRJob -MockWith { @($PolicyJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'33333333-3333-3333-3333-333333333333') -ApproxSize 30GB -BackupSize 18GB -SourceJob $ChildJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'HPE Morpheus - Windows - Linux' }
        $Row.OnDiskGB | Should -Be 18
    }

    It 'stays on the original Id when GetParentJob() returns null (already top-level)' {
        # 'Nutanix AHV Backup' - not on $KnownSafeJobTypes, for the same
        # reason as above: 'VMware Backup' being allowlisted would leave the
        # sweep untriggered and mask what this test is meant to prove.
        $TopLevelJob = script:New-FakeJob -Name 'Nutanix AHV - Windows - Linux' -TypeToString 'Nutanix AHV Backup'
        Mock Get-VBRJob -MockWith { @($TopLevelJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'44444444-4444-4444-4444-444444444444') -ApproxSize 10GB -BackupSize 5GB -SourceJob $TopLevelJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Nutanix AHV - Windows - Linux' }
        $Row.OnDiskGB | Should -Be 5
    }

    It 'falls back to the original Id when GetParentJob() throws' {
        $ThrowingChild = script:New-FakeJob -Name 'ChildThatThrows' -TypeToString 'HPE Morpheus VME Backup' -ThrowOnGetParentJob
        Mock Get-VBRJob -MockWith { @($ThrowingChild) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'55555555-5555-5555-5555-555555555555') -ApproxSize 8GB -BackupSize 4GB -SourceJob $ThrowingChild) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'ChildThatThrows' }
        $Row.OnDiskGB | Should -Be 4
    }
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: The first new test FAILS — `$NeedsSweep` is `$true` (HPE Morpheus VME Backup isn't allowlisted) so the sweep runs, but today's tier 1 (Task 2's code, no walk-up yet) buckets the restore point directly under `$ChildJob`'s Id, and `HPE Morpheus - Windows - Linux`'s row shows `OnDiskGB = 0`. The second and third tests already PASS — test 2 needs no walk-up at all (there's no child job in that scenario, so direct tier-1 matching from Task 2 already handles it), and test 3's `GetParentJob()` throw doesn't matter yet either, since without Step 3's code there's no walk-up attempt to throw from. Both are legitimate regression guards for the behavior Step 3 is about to add, not tests that happen to pass by accident.

- [x] **Step 3: Implement the walk-up**

In `Get-VhcJob.ps1`, replace:

```powershell
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch {}
                if ($null -eq $SourceJob) { continue }

                $JobIdKey = $SourceJob.Id.ToString()
```

with:

```powershell
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch {}
                if ($null -eq $SourceJob) { continue }

                try {
                    $ParentJob = $SourceJob.GetParentJob()
                    if ($null -ne $ParentJob) { $SourceJob = $ParentJob }
                } catch {}

                $JobIdKey = $SourceJob.Id.ToString()
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: All three new tests PASS. Re-confirm Tasks 1-2's tests and all `ISC-*` tests still PASS.

- [x] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "feat(jobs): tier 1 - GetParentJob() walk-up for per-machine child jobs

Policy-driven platforms (Managed Agents, Nutanix AHV, HPE Morpheus,
oVirt KVM) resolve GetSourceJob() to a per-machine child job, not the
policy job Get-VBRJob returns. Walk up once via GetParentJob(); safe
no-op for already-top-level job types."
```

---

## Task 4: Tier 1 — validate the resolved Id against `$Jobs` (the Backup Copy fix)

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`

- [x] **Step 1: Write the failing test**

Add a new `Describe` block:

```powershell
# ---------------------------------------------------------------------------
# Tier 1 (ADR 0021): a non-null GetSourceJob()/GetParentJob() result is not
# sufficient evidence of a match - validate the resolved Id against $Jobs.
# ---------------------------------------------------------------------------
Describe 'Tier 1: resolved Id must be a member of $Jobs' {

    BeforeEach {
        $script:CapturedJobRows = @()
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

    It 'does not attribute a restore point to a per-source child object whose Id is absent from $Jobs' {
        # Simulates Backup Copy: GetSourceJob() (even after the GetParentJob()
        # walk-up) resolves to a real object whose Id was never one of $Jobs.
        $CopyJob     = script:New-FakeJob -Name 'Backup Copy - VMware to Vault' -TypeToString 'Backup Copy'
        $ChildObject = script:New-FakeJob -Name 'Backup Copy - VMware to Vault\VMware - Backup to Vault Direct' -TypeToString 'Backup Copy'
        Mock Get-VBRJob -MockWith { @($CopyJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'66666666-6666-6666-6666-666666666666') -ApproxSize 23GB -BackupSize 27.63GB -SourceJob $ChildObject) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Backup Copy - VMware to Vault' }
        $Row.OnDiskGB | Should -Be 0
    }
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: FAIL — `$Row.OnDiskGB` is `27.63`, because today's tier 1 accepts `$ChildObject`'s `Id` as a match even though `$ChildObject` isn't one of `$Jobs`. (`$Row` for `'Backup Copy - VMware to Vault'` will actually show `0`, because the restore point is bucketed under `$ChildObject.Id`, a key `$CopyJob` never looks up — but this is the *wrong reason*: the data hasn't correctly attributed anywhere, it's silently lost. The assertion still fails to demonstrate this task is needed only if the row exists with a non-zero value; if it shows 0 for the wrong reason, tighten the test before proceeding — see the exact fix below.)

Since a silently-lost restore point and a correctly-rejected one both show `OnDiskGB = 0` on `$CopyJob`'s own row, this specific assertion doesn't actually distinguish "fixed" from "still broken" by itself. Strengthen Step 1's test before implementing, by asserting on the *sweep's log line* instead, which does distinguish the two states. Replace the test's assertion with:

```powershell
    It 'does not attribute a restore point to a per-source child object whose Id is absent from $Jobs' {
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }

        $CopyJob     = script:New-FakeJob -Name 'Backup Copy - VMware to Vault' -TypeToString 'Backup Copy'
        $ChildObject = script:New-FakeJob -Name 'Backup Copy - VMware to Vault\VMware - Backup to Vault Direct' -TypeToString 'Backup Copy'
        Mock Get-VBRJob -MockWith { @($CopyJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'66666666-6666-6666-6666-666666666666') -ApproxSize 23GB -BackupSize 27.63GB -SourceJob $ChildObject) )
            } else { @() }
        }
        Get-VhcJob
        $SweepLine = $script:LogMessages | Where-Object { $_ -match 'matched via tier 1' } | Select-Object -Last 1
        $SweepLine | Should -Match '\b0 matched via tier 1\b'
    }
```

Note the `Mock Write-LogFile` override inside the `It` block — Pester allows re-mocking within a test to layer a more specific behavior on top of the `BeforeEach` mock. Re-run Step 2 with this version: FAIL, because today's log line reads `1 matched via tier 1` (the invalid Id is being counted as a match).

(The regex is `\b`-anchored rather than a bare `'0 matched via tier 1'` substring, so it can't accidentally match `10 matched via tier 1` on a busier sweep — Task 3's code review flagged the unanchored form as a false-positive risk.)

- [x] **Step 3: Implement the `$KnownJobIds` validation**

**Amended against the actual code landed in Task 3 (commit `ea16fd4`):** Task 3 folded forward two fixes from Task 2's review — a `$Tier1Failed` counter (incremented when `GetSourceJob()` throws) and a null-Id guard (`if ($null -eq $SourceJob.Id) { continue }`) — that aren't reflected in the plan text above. The actual current code has both. Use the version below, which preserves them and additionally routes the null-Id case into `$Unresolved` (Task 3's own code review flagged that a null Id *is* an unresolved condition tier 2 has a genuine shot at via the name-based path — silently dropping it would foreclose a recoverable case once Task 5 lands).

In `Get-VhcJob.ps1`, replace:

```powershell
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)

            $Tier1Matched = 0
            $Tier1Failed  = 0
            foreach ($RestorePoint in $AllRestorePoints) {
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch { $Tier1Failed++ }
                if ($null -eq $SourceJob) { continue }

                try {
                    $ParentJob = $SourceJob.GetParentJob()
                    if ($null -ne $ParentJob) { $SourceJob = $ParentJob }
                } catch {}

                if ($null -eq $SourceJob.Id) { continue }
                $JobIdKey = $SourceJob.Id.ToString()
                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier1Matched++
            }

            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide, $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures"
```

with:

```powershell
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)

            $KnownJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
            foreach ($j in @($Jobs)) { [void]$KnownJobIds.Add($j.Id.ToString()) }

            $Unresolved   = [System.Collections.ArrayList]::new()
            $Tier1Matched = 0
            $Tier1Failed  = 0
            foreach ($RestorePoint in $AllRestorePoints) {
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch { $Tier1Failed++ }
                if ($null -eq $SourceJob) { [void]$Unresolved.Add($RestorePoint); continue }

                try {
                    $ParentJob = $SourceJob.GetParentJob()
                    if ($null -ne $ParentJob) { $SourceJob = $ParentJob }
                } catch {}

                if ($null -eq $SourceJob.Id) { [void]$Unresolved.Add($RestorePoint); continue }
                $JobIdKey = $SourceJob.Id.ToString()
                if (-not $KnownJobIds.Contains($JobIdKey)) { [void]$Unresolved.Add($RestorePoint); continue }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier1Matched++
            }

            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide, $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $($Unresolved.Count) unresolved"
```

(`$Unresolved` isn't consumed by anything yet — tier 2, added in Task 5, is what reads it. Building the list now and reading it later is intentional; it keeps this task's diff focused on the validation check alone. `$Tier1Failed` is preserved rather than folded into `$Unresolved`'s count — "GetSourceJob threw" and "resolved but rejected" are different support conversations and shouldn't collapse into one number.)

- [x] **Step 4: Run the test to verify it passes**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: PASS. Re-confirm Tasks 1-3's tests and all `ISC-*` tests still PASS — in particular, re-run Task 2's and Task 3's tests to confirm the `$KnownJobIds` check doesn't reject any of their (legitimate, in-`$Jobs`) resolutions.

- [x] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "feat(jobs): tier 1 - validate resolved Id against \$Jobs

A non-null GetSourceJob()/GetParentJob() result is not sufficient
evidence of a match - Backup Copy jobs resolve to a per-source/per-VM
child object whose Id was never a member of \$Jobs. Reject it and let
tier 2 (next commit) attempt the restore point instead of silently
losing it under a key nothing looks up."
```

---

## Task 5: Tier 2 — gated name-based fallback, and the `Snapshot`-type skip

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`

- [x] **Step 1: Write the failing tests**

Add a new `Describe` block:

```powershell
# ---------------------------------------------------------------------------
# Tier 2 (ADR 0021): gated name-based fallback via GetBackup().GetParentOrThis().Name.
# ---------------------------------------------------------------------------
Describe 'Tier 2: gated name-based fallback' {

    BeforeEach {
        $script:CapturedJobRows = @()
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

    It 'resolves via tier 2 when GetSourceJob() throws and the job has zero tier-1 matches' {
        $CloudJob = script:New-FakeJob -Name 'Linux-01' -TypeToString 'Azure IaaS Backup'
        Mock Get-VBRJob -MockWith { @($CloudJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'77777777-7777-7777-7777-777777777777') -ApproxSize 157GB -BackupSize 29.30GB -ThrowOnGetSourceJob -BackupParentOrThisName 'Linux-01') )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Linux-01' }
        $Row.OnDiskGB     | Should -Be 29.30
        $Row.OriginalSize | Should -Be 157GB
    }

    It 'resolves via tier 2 when GetSourceJob() returns $null (not throwing) and the job has zero tier-1 matches' {
        # Same fallthrough-to-tier-2 path as the throw case above, but via the
        # OTHER branch of tier 1's "if ($null -eq $SourceJob)" check - confirms
        # both routes into $Unresolved converge on the same tier-2 behavior.
        $CloudJob = script:New-FakeJob -Name 'web01-schedule-backups' -TypeToString 'Azure IaaS Backup'
        Mock Get-VBRJob -MockWith { @($CloudJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'87878787-8787-8787-8787-878787878787') -ApproxSize 81GB -BackupSize 36.43GB -SourceJob $null -BackupParentOrThisName 'web01-schedule-backups') )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'web01-schedule-backups' }
        $Row.OnDiskGB     | Should -Be 36.43
        $Row.OriginalSize | Should -Be 81GB
    }

    It 'suppresses a tier-2 name match when the resolved job already has a tier-1 match' {
        $HpeJob = script:New-FakeJob -Name 'HPE Morpheus - Windows - Linux' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($HpeJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    # Tier-1 match: resolves cleanly via GetSourceJob().
                    (script:New-FakeRestorePoint -Name 'Windows01-current' -ObjectId ([guid]'88888888-8888-8888-8888-888888888881') -ApproxSize 190GB -BackupSize 72.99GB -SourceJob $HpeJob),
                    # Stale machine sharing a display name with an active one -
                    # GetSourceJob() throws, and the name resolves to the SAME
                    # job, which already has a tier-1 match above - suppressed.
                    (script:New-FakeRestorePoint -Name 'Windows01-stale' -ObjectId ([guid]'88888888-8888-8888-8888-888888888882') -ApproxSize 190GB -BackupSize 117GB -ThrowOnGetSourceJob -BackupParentOrThisName 'HPE Morpheus - Windows - Linux')
                )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'HPE Morpheus - Windows - Linux' }
        $Row.OnDiskGB | Should -Be 72.99
    }

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
                @( (script:New-FakeRestorePoint -Type 'Snapshot' -ObjectId ([guid]'99999999-9999-9999-9999-999999999999') -ApproxSize 100GB -BackupSize 50GB -BackupParentOrThisName 'VMware - Domain Controller') )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMware - Domain Controller' }
        $Row.OnDiskGB | Should -Be 0
    }
}
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: The first two tests FAIL (`Linux-01`'s and `web01-schedule-backups`'s rows both show `OnDiskGB = 0` — no tier 2 exists yet, so both the throwing and the null-returning restore points are simply dropped into `$Unresolved` and never picked up). The third and fourth tests PASS trivially today (no tier 2 exists to wrongly include the stale/snapshot points either) — they're written now as regression guards for the gating and skip logic Step 3 adds.

- [x] **Step 3: Implement tier 2 and the `Snapshot` skip**

**Amended against the actual code landed in Tasks 3-4 (including the `f21f03b` regression fix):** the plan text below predates Task 3's `$Tier1Failed` fold-forward and Task 4's `$KnownJobIds`/null-guard work. Use the version here, which carries all of it through unchanged and keeps the final log line's `matched via tier 1` phrase intact — Task 4's own test (`Should -Match '\b0 matched via tier 1\b'`) greps for that exact phrase, and it must keep matching after this task's log-line rewrite. This version also folds in three Minor improvements from Task 4's code review: a WHY comment on the null-`$j` guard (its rationale previously lived only inside a test body), an ordinal-ignore-case comparer for `$KnownJobIds` so it can no longer be *stricter* than the case-insensitive `$RestorePointsByJob` hashtable it gates, and (see Step 1 below) discriminating log-line assertions replacing two decorative `Should -Not -Throw` checks.

In `Get-VhcJob.ps1`, replace:

```powershell
            $KnownJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
            foreach ($j in @($Jobs)) { if ($null -ne $j -and $null -ne $j.Id) { [void]$KnownJobIds.Add($j.Id.ToString()) } }

            $Unresolved   = [System.Collections.ArrayList]::new()
            $Tier1Matched = 0
            $Tier1Failed  = 0
            foreach ($RestorePoint in $AllRestorePoints) {
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch { $Tier1Failed++ }
                if ($null -eq $SourceJob) { [void]$Unresolved.Add($RestorePoint); continue }

                # A GetParentJob() throw means this job type doesn't implement
                # it - falling back to the child's own Id is a valid outcome,
                # not a failure, so it isn't counted alongside $Tier1Failed.
                $ParentJob = $null
                try { $ParentJob = $SourceJob.GetParentJob() } catch {}
                if ($null -ne $ParentJob) { $SourceJob = $ParentJob }

                if ($null -eq $SourceJob.Id) { [void]$Unresolved.Add($RestorePoint); continue }
                $JobIdKey = $SourceJob.Id.ToString()
                if (-not $KnownJobIds.Contains($JobIdKey)) { [void]$Unresolved.Add($RestorePoint); continue }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier1Matched++
            }

            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide, $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $($Unresolved.Count) unresolved"
```

with:

```powershell
            # Same null-placeholder risk $KnownJobIds guards against below - a
            # $Jobs element can be a non-null object with no populated Id (or,
            # per f21f03b, $Jobs itself can carry a null placeholder when
            # Get-VBRJob throws) - either way, .Id.ToString() on it aborts the
            # whole sweep via the outer catch, not just this one lookup.
            $JobIdByName = @{}
            foreach ($j in @($Jobs)) {
                if ($null -ne $j -and $null -ne $j.Id -and -not $JobIdByName.ContainsKey($j.Name)) { $JobIdByName[$j.Name] = $j.Id.ToString() }
            }

            # OrdinalIgnoreCase: $RestorePointsByJob (a Hashtable) looks up
            # keys case-insensitively, so this gate must not be stricter than
            # the thing it's gating - a case-sensitive HashSet here would risk
            # silently dropping a resolved Id that only ever mismatches on case.
            $KnownJobIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($j in @($Jobs)) { if ($null -ne $j -and $null -ne $j.Id) { [void]$KnownJobIds.Add($j.Id.ToString()) } }

            $Tier1MatchedJobIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            $Unresolved         = [System.Collections.ArrayList]::new()
            $Tier1Matched       = 0
            $Tier1Failed        = 0
            $Tier2Matched       = 0

            # Tier 1: Id-based via GetSourceJob() (+ GetParentJob() walk-up).
            # Snapshot-type (replication) restore points never resolve via
            # GetSourceJob() - skip the doomed call; they're sized via the
            # Replica loop added in a later change.
            foreach ($RestorePoint in $AllRestorePoints) {
                if ($RestorePoint.Type -eq 'Snapshot') { [void]$Unresolved.Add($RestorePoint); continue }

                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch { $Tier1Failed++ }
                if ($null -eq $SourceJob) { [void]$Unresolved.Add($RestorePoint); continue }

                # A GetParentJob() throw means this job type doesn't implement
                # it - falling back to the child's own Id is a valid outcome,
                # not a failure, so it isn't counted alongside $Tier1Failed.
                $ParentJob = $null
                try { $ParentJob = $SourceJob.GetParentJob() } catch {}
                if ($null -ne $ParentJob) { $SourceJob = $ParentJob }

                if ($null -eq $SourceJob.Id) { [void]$Unresolved.Add($RestorePoint); continue }
                $JobIdKey = $SourceJob.Id.ToString()
                if (-not $KnownJobIds.Contains($JobIdKey)) { [void]$Unresolved.Add($RestorePoint); continue }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                [void]$Tier1MatchedJobIds.Add($JobIdKey)
                $Tier1Matched++
            }

            # Tier 2: name-based fallback, GATED - only accepted if the
            # resolved job has zero tier-1 matches. Display names collide
            # across genuinely different backup objects, so this cannot be
            # trusted to override an existing Id-based match.
            foreach ($RestorePoint in $Unresolved) {
                if ($RestorePoint.Type -eq 'Snapshot') { continue }

                $JobIdKey = $null
                try {
                    $ParentName = $RestorePoint.GetBackup().GetParentOrThis().Name
                    if ($ParentName -and $JobIdByName.ContainsKey($ParentName)) { $JobIdKey = $JobIdByName[$ParentName] }
                } catch {}
                if (-not $JobIdKey) { continue }
                if ($Tier1MatchedJobIds.Contains($JobIdKey)) { continue }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier2Matched++
            }

            Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2, $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched) unmatched/orphaned/snapshot"
```

(This log line's phrasing keeps `matched via tier 1` intact — Task 4's own test greps for that exact phrase, and it would silently break here if the wording changed.)

- [x] **Step 4: Run the tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: All four new tests PASS. Re-confirm Tasks 1-4's tests and all `ISC-*` tests still PASS.

- [x] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "feat(jobs): tier 2 - gated name-based fallback, skip Type=Snapshot

Fixes the public cloud plug-in platform 0/0 bug (AWS/Azure/GCE), where
GetSourceJob() throws outright and there is no Id-yielding path back
to a job. Gated on zero tier-1 matches: display names collide across
genuinely distinct backup objects (confirmed live - HPE Morpheus), so
an ungated name match could misattribute a decommissioned machine's
data onto a currently-active job.

Snapshot-type restore points are skipped by both tiers - 100% throw on
GetSourceJob() and are sized separately (Replica handling, next
commit)."
```

---

## Task 6: Replica handling, multi-chain summing, and final regression pass

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1`
- Modify: `docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md`

- [ ] **Step 1: Write the failing tests**

Add a new `Describe` block:

```powershell
# ---------------------------------------------------------------------------
# Replica handling (ADR 0021): Snapshot-type restore points are routed
# around the sweep entirely, sized via the original per-job method.
# ---------------------------------------------------------------------------
Describe 'Replica jobs bypass tier 1/2 entirely' {

    BeforeEach {
        $script:CapturedJobRows = @()
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

    It 'sizes a Replica job via GetLastBackup() + Get-VBRRestorePoint -Backup, not via tier 1/2' {
        $ReplicaBackupRef = [PSCustomObject]@{ Id = [guid]::NewGuid() }
        $ReplicaJob       = script:New-FakeJob -Name 'Hyper-V - Replicas' -TypeToString 'Hyper-V Replication' -LastBackup $ReplicaBackupRef
        # A second, non-Replica job forces $NeedsSweep to $true so the sweep
        # (and thus the Replica-bypass branch) actually runs.
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        # $ReplicaJob is the only job with a non-null LastBackup in this test,
        # so $ReplicaJob's own scoped call is the only one that can ever pass
        # a non-null -Backup here - no need to depend on reference equality
        # surviving Pester's mock parameter binding.
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -ne $Backup) {
                @( (script:New-FakeRestorePoint -Type 'Snapshot' -ObjectId ([guid]'10101010-1010-1010-1010-101010101010') -ApproxSize 44GB -BackupSize 23.47GB) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Hyper-V - Replicas' }
        $Row.OnDiskGB     | Should -Be 23.47
        $Row.OriginalSize | Should -Be 44GB
    }

    It 'a Replica job whose GetLastBackup() throws falls back to Info.IncludedSize / 0' {
        $ReplicaJob  = script:New-FakeJob -Name 'VMware - Replicas' -TypeToString 'VMware Replication' -ThrowOnGetLastBackup
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith { @() }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMware - Replicas' }
        $Row.OnDiskGB | Should -Be 0
    }
}

# ---------------------------------------------------------------------------
# Multi-chain summing (ADR 0021): restore points sharing a tier-1-matched
# job Id but from different ObjectIds/chains all sum into OnDiskGB; only
# the latest per ObjectId feeds OriginalSize.
# ---------------------------------------------------------------------------
Describe 'Multi-chain summing for a single tier-1-matched job' {

    BeforeEach {
        $script:CapturedJobRows = @()
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

    It 'sums OnDiskGB across chains but takes only the latest ApproxSize per ObjectId for OriginalSize' {
        $Job          = script:New-FakeJob -Name 'RetargetedJob' -TypeToString 'HPE Morpheus VME Backup'
        $SharedObject = [guid]'20202020-2020-2020-2020-202020202020'
        Mock Get-VBRJob -MockWith { @($Job) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'OldChain' -ObjectId $SharedObject -CreationTimeUtc (Get-Date '2026-01-01') -ApproxSize 10GB -BackupSize 3GB -SourceJob $Job),
                    (script:New-FakeRestorePoint -Name 'NewChain' -ObjectId $SharedObject -CreationTimeUtc (Get-Date '2026-06-01') -ApproxSize 15GB -BackupSize 4GB -SourceJob $Job)
                )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RetargetedJob' }
        $Row.OnDiskGB     | Should -Be 7      # 3 + 4, both chains summed
        $Row.OriginalSize | Should -Be 15GB   # only the newer chain's ApproxSize
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: Both `Describe 'Replica jobs...'` tests FAIL (`Hyper-V - Replicas` never gets any restore points today — nothing populates its entry in `$RestorePointsByJob`, since the Replica loop doesn't exist yet, so both a live-chain Replica and a never-run one currently look identical: `OnDiskGB = 0`. The first test's assertion of `23.47` catches this). `Describe 'Multi-chain summing...'` already PASSES — that logic is pre-existing, unchanged code (lines 94-131 of the original file); it's included here as a regression guard proving tier 1's bucketing correctly feeds it, not as new behavior.

- [ ] **Step 3: Amend the sweep's outer catch — reset `$NeedsSweep` on failure (plan amendment, flagged by Task 1's code review; deliberately landed BEFORE the Replica loop below)**

**Ordering matters here, per Task 5's code review:** this step is sequenced ahead of the Replica loop (Step 3b, below) on purpose. Step 3b adds a new unguarded-ish dereference (`$Job.Id.ToString()`, guarded per the amendment in Step 3b itself, but still new code that runs inside the sweep's all-or-nothing `try`) into the exact block this step makes fail-safe. Landing the safety net first means the Replica loop is added into a sweep that already degrades gracefully instead of catastrophically; landing them in the other order leaves a window where the newest, least-proven code has no net.

Task 1's code review found that the sweep's outer `catch` (added in Task 1, untouched since) doesn't reset `$NeedsSweep` to `$false` on failure. Left as-is, a sweep exception (thrown at any point during tiers 1/2 or the Replica loop about to be added) leaves the main loop still branching on `$NeedsSweep = $true`, reading from a `$RestorePointsByJob` that's now permanently empty for every remaining job — including allowlisted VMware/Hyper-V jobs that were sized correctly before this branch existed, and including Replica jobs, which ADR 0022 requires to "always use the per-job path regardless of whether the sweep runs." Both guarantees currently break on a sweep failure.

The fix is one line, and it composes cleanly with the main loop's existing `if ($NeedsSweep) { ... } else { $Job.GetLastBackup() ... }` branch (added in Task 1): flipping `$NeedsSweep` to `$false` makes every remaining job — allowlisted or not, Replica or not — fall through to the original, already-correct per-job path, with no other code change required.

In `Get-VhcJob.ps1`, replace:

```powershell
        } catch {
            Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR"
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
        }
```

with:

```powershell
        } catch {
            Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR"
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
            $NeedsSweep = $false
        }
```

Add a test to `Describe 'Performance gate: sweep triggers on unrecognized job types'` (from Task 1):

```powershell
    It 'falls back to the per-job method for every job when the sweep itself throws' {
        Mock Get-VBRJob -MockWith {
            @(
                (script:New-FakeJob -Name 'VMwareJob' -TypeToString 'VMware Backup' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })),
                (script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() }))
            )
        }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                throw 'Simulated sweep failure'
            } else {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'12121212-1212-1212-1212-121212121212') -ApproxSize 9GB -BackupSize 6GB) )
            }
        }
        Get-VhcJob
        $VMwareRow   = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMwareJob' }
        $MorpheusRow = $script:CapturedJobRows | Where-Object { $_.Name -eq 'MorpheusJob' }
        $VMwareRow.OnDiskGB   | Should -Be 6
        $MorpheusRow.OnDiskGB | Should -Be 6
    }
```

This asserts BOTH jobs recover via the per-job fallback after the sweep throws — including `MorpheusJob`, which triggered the sweep in the first place and would otherwise be the job most starved of a fallback (it has no tier-1/2 result to fall back to; only `$NeedsSweep = $false` gives it one). Run the file-scoped Pester command; expect this test to FAIL before the fix (both rows show `0`, since `$NeedsSweep` stays `$true` and `$RestorePointsByJob` has nothing for either job — the sweep threw before either tier could populate it) and PASS after.

- [ ] **Step 3b: Implement Replica handling**

**Amended per Task 5's code review:** the plan's original Replica-loop text dereferenced `$Job.Id.ToString()` unguarded — the same bug class `f21f03b` and `d5aea23` each fixed once already in this same sweep (a non-null `$Job` with a null/absent `Id` aborts the entire sweep via the outer catch, not just this one job). The `Where-Object { $_.TypeToString -like '*Replication*' }` filter already screens out a null `$Job` element (`$null -like '*'` is `$false`), but not a real object whose `Id` happens to be null. Guarded here, matching the established pattern.

In `Get-VhcJob.ps1`, replace:

```powershell
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier2Matched++
            }

            Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2, $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched) unmatched/orphaned/snapshot"
```

with:

```powershell
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier2Matched++
            }

            # Replica jobs: sized via the original per-job method, unchanged -
            # already correct for replicas, never routed through tier 1/2.
            # Guard against a null Id for the same reason $KnownJobIds and
            # $JobIdByName do above - a non-null $Job with no populated Id
            # would otherwise abort the whole sweep via the outer catch.
            foreach ($Job in @($Jobs | Where-Object { $_.TypeToString -like '*Replication*' })) {
                if ($null -eq $Job.Id) { continue }
                $JobIdKey   = $Job.Id.ToString()
                $LastBackup = $null
                try { $LastBackup = $Job.GetLastBackup() } catch {}
                $ReplicaPoints = @()
                if ($null -ne $LastBackup) {
                    try { $ReplicaPoints = @(Get-VBRRestorePoint -Backup $LastBackup -WarningAction SilentlyContinue) } catch {}
                }
                $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                foreach ($RestorePoint in $ReplicaPoints) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
            }

            Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2, $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched) unmatched/orphaned/snapshot"
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: All tests PASS, including every `Describe` block from Tasks 1-6, Step 3b's sweep-failure-fallback test, and all `ISC-1` through `ISC-10` tests.

- [ ] **Step 5: Run the complete test file one more time as the final regression gate**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`

Expected: `Tests Passed: <total>, Failed: 0, Skipped: 0` — zero failures across the entire file.

- [ ] **Step 6: Flip the design spec's status to Implemented**

In `docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md`, replace:

```markdown
**Date:** 2026-08-21
**Status:** Proposed
```

with:

```markdown
**Date:** 2026-08-21
**Status:** Implemented
```

- [ ] **Step 7: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 \
        vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1 \
        docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md
git commit -m "feat(jobs): route Replica jobs around the sweep; mark design spec implemented

Replica-type jobs (VMware/Hyper-V Replication) are sized via the same
GetLastBackup() + Get-VBRRestorePoint -Backup call production already
uses for them - correct today, never routed through tier 1/2. This
completes the tiered sweep (ADR 0021) and its allowlist gate (ADR
0022); flips the design spec's status to Implemented.

Also resets \$NeedsSweep to \$false in the sweep's outer catch (flagged
by Task 1's code review): without it, a sweep exception left every
remaining job - allowlisted and Replica alike - reading from a
permanently-empty dictionary instead of falling back to the per-job
method ADR 0022 requires unconditionally for Replicas."
```

---

## Notes for whoever picks this up

- **Not covered by this plan, tracked separately:** the `VMware Cloud Director - vApp Backup` Source Size double-count ([#193](https://github.com/VeeamHub/veeam-healthcheck/issues/193)) is a pre-existing aggregation bug in the unchanged `Group-Object ObjectId` → latest → `ApproxSize` math (lines 94-131 of the original file, untouched by every task above). Don't fold a fix for it into this branch — it's an independent bug with its own issue.
- **Still open per the design spec's Open Items:** whether `Backup Copy Job 3`'s own single object has a copy chain that resolves anywhere at all (a live-lab question, not something a unit test can answer) — worth a quick `Get-VBRBackup | ?{ JobId -eq <id> }` check against a real VBR server before considering the Backup Copy path fully closed, but it doesn't block merging this plan's work.
- **The exact `$KnownSafeJobTypes` list** (`VMware Backup`, `Hyper-V Backup`, `Windows Agent Backup`, `Windows Agent Policy`, `Linux Agent Backup`, `Cloud Director Backup`) is deliberately narrow — see ADR 0022's evidence-tiering rule before adding to it. Anything without real, non-zero, exactly-matching validation data doesn't belong on the list; it gets the sweep instead, which is a performance cost, not a correctness risk.
