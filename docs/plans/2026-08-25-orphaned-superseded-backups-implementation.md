# Orphaned & Superseded Backups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new VBR report section (HTML + JSON) surfacing restore points that don't count toward any job's active size — Orphaned (no job resolves) and Superseded (resolves to a real job but excluded from its active count) — grouped per repository with a job-level rollup and an expandable per-object breakdown. Bundles a fix for issue #197 (a sizing double-count bug).

**Architecture:** `Get-VhcJob.ps1` is extended to retain what it already discards during its restore-point sweep (into a new `$script:VhcOrphanedSupersededCache`) and to add a per-job, zero-overlap-guarded `GetObjectsInJob()` cross-reference that both fixes #197's double-count and feeds the same cache. A new sibling PowerShell script, `Get-VhcOrphanedSupersededBackups.ps1`, reads that cache, excludes Tape Backups, resolves job/repo/type once per `BackupId` group, splits into one row per `(BackupId, ObjectId)`, classifies Orphaned vs. Superseded, and exports a new CSV. On the C# side: a dynamic CSV read → a new `OrphanedSupersededBackupAggregator` (grouping object-level rows up to job-level) → a new HTML table renderer (reusing the existing accordion pattern, extended to row-level) → a new dedicated JSON property (the existing `HtmlSection`/`SetSection` path is flat-only and can't carry this feature's nested per-object detail).

**Tech Stack:** PowerShell 7 (Pester v5 tests), C# / .NET 8 (xUnit tests), plain JS/CSS (no framework) for the HTML report.

Design spec: `docs/superpowers/specs/2026-08-24-orphaned-superseded-backups-design.md`. Branch: `feat/issue-192-orphaned-superseded-backups` (already created, off `dev`). Fixes #192, fixes #197.

---

## File Structure

**PowerShell (collection):**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1` — retain sweep groups into a cache; add the unconditional per-job stale-`ObjectId` guard.
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1` — new tests for both changes above.
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.ps1` — reads the cache, excludes Tape, classifies, exports the new CSV.
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.Tests.ps1`.
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1` — add the new function to `FunctionsToExport`.
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1` — add the collector call site.
- Create: `vHC/HC_Reporting/Tools/GoldenBaselines/ObjectSchemas/_orphanedSupersededBackups.schema.json`.

**C# (processing + reporting):**
- Modify: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs` — new token field + dynamic-read wrapper.
- Create: `vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededObjectRecord.cs`
- Create: `vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededBackupRecord.cs`
- Create: `vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededBackupAggregator.cs`
- Create: `vHC/VhcXTests/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededBackupAggregatorTests.cs`
- Modify: `vHC/HC_Reporting/Functions/Reporting/DataTypes/CFullReportJson.cs` — new `OrphanedSupersededBackups` property.
- Create: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/OrphanedSupersededBackups/COrphanedSupersededBackupsTable.cs`
- Modify: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` — new `AddOrphanedSupersededBackupsTable(bool scrub)` method.
- Modify: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs` — new wrapper + call site.
- Modify: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs` — new sidebar nav link in `BuildSidebar()`.
- Modify: `vHC/HC_Reporting/ReportScript.js` — new row-level toggle function.
- Modify: `vHC/HC_Reporting/css.css` — new `.detail-row` rule + `@media print` extension.

---

## Task 1: Retain sweep groups instead of discarding them

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1:100,212-246`
- Test: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1`

The sweep's Tier 2 loop currently has two `continue` statements (lines 220-221 in the current file) that silently discard: (a) a group that never resolves to any job name, and (b) a group that resolves to a job name but gets suppressed because that job already has a Tier 1 match elsewhere. Both need to be retained into a new `$script:VhcOrphanedSupersededCache` instead of just discarded.

- [ ] **Step 1: Write the failing tests**

Add this new `Describe` block to `Get-VhcJob.Tests.ps1` (append after the last existing `Describe` block in the file — find the end of file and add here):

```powershell
# ---------------------------------------------------------------------------
# Orphaned & Superseded Backups (#192): sweep groups retained, not discarded
# ---------------------------------------------------------------------------
Describe 'Orphaned/Superseded cache: sweep group retention' {

    BeforeEach {
        Mock Write-LogFile                 -MockWith { }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors   -MockWith { }
        Mock Export-VhciCsv                -MockWith { }
        Mock Add-VhciModuleError           -MockWith { }
        Mock Get-VBRBackup                 -MockWith { @() }
        $script:VhcOrphanedSupersededCache = $null
    }

    It 'retains a group unresolved by either tier as Unresolved, tagged with no CurrentJobId' {
        $FakeJob = script:New-FakeJob -Name 'VMware - Malware' -TypeToString 'Nutanix AHV Backup'
        Mock Get-VBRJob -MockWith { @($FakeJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -Name 'Ghost' -BackupId ([guid]'11111111-1111-1111-1111-111111111111') -ThrowOnGetSourceJob -BackupParentOrThisName 'No Such Job') )
            } else { @() }
        }

        Get-VhcJob | Out-Null

        $script:VhcOrphanedSupersededCache | Should -Not -BeNullOrEmpty
        $unresolved = $script:VhcOrphanedSupersededCache.CandidateGroups | Where-Object { $_.Reason -eq 'Unresolved' }
        $unresolved | Should -HaveCount 1
        $unresolved[0].CurrentJobId | Should -BeNullOrEmpty
        $unresolved[0].RestorePoints[0].Name | Should -Be 'Ghost'
    }

    It 'retains a tier-2-suppressed group as Tier2Suppressed, tagged with the job it named' {
        $FakeJob = script:New-FakeJob -Name 'VBR Managed Agents - Windows' -TypeToString 'Nutanix AHV Backup'
        Mock Get-VBRJob -MockWith { @($FakeJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'WindowsAgent07' -BackupId ([guid]'22222222-2222-2222-2222-222222222222') -SourceJob $FakeJob),
                    (script:New-FakeRestorePoint -Name 'WindowsAgent08' -BackupId ([guid]'33333333-3333-3333-3333-333333333333') -ThrowOnGetSourceJob -BackupParentOrThisName 'VBR Managed Agents - Windows')
                )
            } else { @() }
        }

        Get-VhcJob | Out-Null

        $suppressed = $script:VhcOrphanedSupersededCache.CandidateGroups | Where-Object { $_.Reason -eq 'Tier2Suppressed' }
        $suppressed | Should -HaveCount 1
        $suppressed[0].CurrentJobId | Should -Be $FakeJob.Id.ToString()
        $suppressed[0].RestorePoints[0].Name | Should -Be 'WindowsAgent08'
    }

    It 'sets SweepRan to $false when the environment needs no sweep at all' {
        $FakeJob = script:New-FakeJob -Name 'VMware - Safe' -TypeToString 'VMware Backup' -LastBackup $null
        Mock Get-VBRJob -MockWith { @($FakeJob) }

        Get-VhcJob | Out-Null

        $script:VhcOrphanedSupersededCache.SweepRan | Should -BeFalse
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`
Expected: FAIL — `$script:VhcOrphanedSupersededCache` is `$null` in all three, since nothing writes to it yet.

- [ ] **Step 3: Implement — declare the cache and retain both suppression paths**

In `Get-VhcJob.ps1`, change:

```powershell
    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
```

to:

```powershell
    $RestorePointsByJob = @{}
    $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
        SweepRan        = $false
        CandidateGroups = [System.Collections.Generic.List[object]]::new()
    }
    if ($NeedsSweep) {
```

Then change the Tier 2 loop:

```powershell
            foreach ($GroupPoints in $UnresolvedGroups) {
                $Representative = $GroupPoints[0]

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
                foreach ($RestorePoint in $GroupPoints) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                $Tier2Matched += $GroupPoints.Count
            }
```

to:

```powershell
            foreach ($GroupPoints in $UnresolvedGroups) {
                $Representative = $GroupPoints[0]

                $JobIdKey = $null
                try {
                    $ParentName = $Representative.GetBackup().GetParentOrThis().Name
                    if ($ParentName -and $JobIdByName.ContainsKey($ParentName)) { $JobIdKey = $JobIdByName[$ParentName] }
                } catch {}
                if (-not $JobIdKey) {
                    # Neither tier resolved this group to any current job -
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
                if ($Tier1MatchedJobIds.Contains($JobIdKey)) {
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
                $Tier2Matched += $GroupPoints.Count
            }
```

Finally, right after the `if ($NeedsSweep) { ... }` block closes (the line `    }` that currently sits just before the `# Main VBR job processing loop` comment), add:

```powershell
    $script:VhcOrphanedSupersededCache.SweepRan = $NeedsSweep
```

placed so the full sequence reads:

```powershell
        } catch {
            try { Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR" } catch {}
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
            $NeedsSweep = $false
        }
    }
    $script:VhcOrphanedSupersededCache.SweepRan = $NeedsSweep

    # ------------------------------------------------------------------
    # Main VBR job processing loop - restore point size calculation
```

This reads `$NeedsSweep` *after* the catch block may have reset it to `$false`, so `SweepRan` correctly reflects whether the sweep actually completed, not just whether it was attempted.

- [ ] **Step 4: Run tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`
Expected: PASS (all tests in this file, including the 3 new ones and every pre-existing one — this change is additive only).

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "feat(jobs): retain sweep groups instead of discarding them (#192)"
```

---

## Task 2: Unconditional per-job stale-ObjectId guard (fixes #197)

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1` — extend `New-FakeJob` with a `GetObjectsInJob()` ScriptMethod; add tests.
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1:256-270` (current line numbers; may shift slightly after Task 1's edits, but this block is right after the `if ($NeedsSweep) {...} else {...}` that sets `$RestorePoints`).

This is Fix 1 + Fix 3 from the design spec: for every job regardless of `$NeedsSweep`, cross-reference `$RestorePoints`' `ObjectId`s against `$Job.GetObjectsInJob()`'s current membership (matched on `.ObjectId`, confirmed live to exist on both). Zero overlap → don't trust the call for this job, exclude nothing (this is what protects VMware Cloud Director Backup jobs, per ADR 0021's documented vApp-container behavior). At least one match → partition into active (kept for sizing) and stale (excluded from sizing, cached as Superseded candidates).

- [ ] **Step 1: Write the failing tests**

First, extend `New-FakeJob` in `Get-VhcJob.Tests.ps1` to support `GetObjectsInJob()`. Change:

```powershell
    function script:New-FakeJob {
        param(
            [string]$Name = 'FakeJob',
            [guid]$Id = [guid]::NewGuid(),
            [string]$TypeToString = 'VMware Backup',
            $ParentJob = $null,
            [switch]$ThrowOnGetParentJob,
            $LastBackup = $null,
            [switch]$ThrowOnGetLastBackup,
            [double]$IncludedSize = 0
        )
        $ParentJobCapture       = $ParentJob
        $ThrowParentJobCapture  = [bool]$ThrowOnGetParentJob
        $LastBackupCapture      = $LastBackup
        $ThrowLastBackupCapture = [bool]$ThrowOnGetLastBackup
```

to:

```powershell
    function script:New-FakeJob {
        param(
            [string]$Name = 'FakeJob',
            [guid]$Id = [guid]::NewGuid(),
            [string]$TypeToString = 'VMware Backup',
            $ParentJob = $null,
            [switch]$ThrowOnGetParentJob,
            $LastBackup = $null,
            [switch]$ThrowOnGetLastBackup,
            [double]$IncludedSize = 0,
            [guid[]]$ObjectsInJobIds = @(),
            [switch]$ThrowOnGetObjectsInJob
        )
        $ParentJobCapture         = $ParentJob
        $ThrowParentJobCapture    = [bool]$ThrowOnGetParentJob
        $LastBackupCapture        = $LastBackup
        $ThrowLastBackupCapture   = [bool]$ThrowOnGetLastBackup
        $ObjectsInJobCapture      = $ObjectsInJobIds
        $ThrowObjectsInJobCapture = [bool]$ThrowOnGetObjectsInJob
```

Then, right after the existing `GetLastBackup` `Add-Member` block (before `return $Job`), add:

```powershell
        $Job | Add-Member -MemberType ScriptMethod -Name GetObjectsInJob -Value {
            if ($ThrowObjectsInJobCapture) { throw 'GetObjectsInJob failed' }
            return @($ObjectsInJobCapture | ForEach-Object { [PSCustomObject]@{ ObjectId = $_ } })
        }.GetNewClosure()
```

Now add the test `Describe` block (append after the one from Task 1):

```powershell
# ---------------------------------------------------------------------------
# Orphaned & Superseded Backups (#192) / #197: stale-ObjectId guard
# ---------------------------------------------------------------------------
Describe 'Stale-ObjectId guard: GetObjectsInJob() cross-reference' {

    BeforeEach {
        Mock Write-LogFile                 -MockWith { }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors   -MockWith { }
        Mock Export-VhciCsv                -MockWith { }
        Mock Add-VhciModuleError           -MockWith { }
        Mock Get-VBRBackup                 -MockWith { @() }
        $script:VhcOrphanedSupersededCache = $null
    }

    It 'excludes a stale ObjectId from CalculatedOriginalSize and TotalOnDiskGB, caches it as StaleObject' {
        $CurrentId = [guid]'44444444-4444-4444-4444-444444444444'
        $StaleId   = [guid]'55555555-5555-5555-5555-555555555555'
        $FakeJob = script:New-FakeJob -Name 'VMware - Malware' -TypeToString 'VMware Backup' -ObjectsInJobIds @($CurrentId)
        Mock Get-VBRJob -MockWith { @($FakeJob) }
        $CurrentPoint = script:New-FakeRestorePoint -Name 'MALWARE' -ObjectId $CurrentId -ApproxSize 150GB -BackupSize 50GB
        $StalePoint   = script:New-FakeRestorePoint -Name 'MALWARE' -ObjectId $StaleId -ApproxSize 90GB -BackupSize 30GB
        $FakeJob | Add-Member -MemberType ScriptMethod -Name GetLastBackup -Value { @($CurrentPoint) } -Force
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -ne $Backup) { @($CurrentPoint, $StalePoint) } else { @() }
        }

        $Result = Get-VhcJob

        $job = $Result | Where-Object { $_.Name -eq 'VMware - Malware' }
        # OnDiskGB should reflect only the current object's 50GB, not 80GB combined.
        [double]$job.OnDiskGB | Should -BeGreaterThan 49
        [double]$job.OnDiskGB | Should -BeLessThan 51

        $stale = $script:VhcOrphanedSupersededCache.CandidateGroups | Where-Object { $_.Reason -eq 'StaleObject' }
        $stale | Should -HaveCount 1
        $stale[0].CurrentJobId | Should -Be $FakeJob.Id.ToString()
        $stale[0].RestorePoints[0].Name | Should -Be 'MALWARE'
        $stale[0].RestorePoints[0].ObjectId | Should -Be $StaleId
    }

    It 'excludes nothing when GetObjectsInJob() matches zero restore points (Cloud Director vApp-container signature)' {
        $VappContainerId = [guid]'66666666-6666-6666-6666-666666666666'
        $RealVm1 = [guid]'77777777-7777-7777-7777-777777777777'
        $RealVm2 = [guid]'88888888-8888-8888-8888-888888888888'
        $FakeJob = script:New-FakeJob -Name 'VCD - vApp' -TypeToString 'Cloud Director Backup' -ObjectsInJobIds @($VappContainerId)
        Mock Get-VBRJob -MockWith { @($FakeJob) }
        $Point1 = script:New-FakeRestorePoint -Name 'vm1' -ObjectId $RealVm1 -ApproxSize 10GB -BackupSize 5GB
        $Point2 = script:New-FakeRestorePoint -Name 'vm2' -ObjectId $RealVm2 -ApproxSize 10GB -BackupSize 5GB
        $FakeJob | Add-Member -MemberType ScriptMethod -Name GetLastBackup -Value { @($Point1) } -Force
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -ne $Backup) { @($Point1, $Point2) } else { @() }
        }

        $Result = Get-VhcJob

        $job = $Result | Where-Object { $_.Name -eq 'VCD - vApp' }
        # Both real VMs' data must still be counted - zero overlap means the
        # guard trusts nothing and excludes nothing.
        [double]$job.OnDiskGB | Should -BeGreaterThan 9
        $stale = $script:VhcOrphanedSupersededCache.CandidateGroups | Where-Object { $_.Reason -eq 'StaleObject' -and $_.CurrentJobId -eq $FakeJob.Id.ToString() }
        $stale | Should -BeNullOrEmpty
    }

    It 'excludes nothing and caches nothing when GetObjectsInJob() itself throws' {
        $FakeJob = script:New-FakeJob -Name 'VMware - Weird' -TypeToString 'VMware Backup' -ThrowOnGetObjectsInJob
        Mock Get-VBRJob -MockWith { @($FakeJob) }
        $Point1 = script:New-FakeRestorePoint -Name 'vm1' -ApproxSize 10GB -BackupSize 5GB
        $FakeJob | Add-Member -MemberType ScriptMethod -Name GetLastBackup -Value { @($Point1) } -Force
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -ne $Backup) { @($Point1) } else { @() }
        }

        { Get-VhcJob } | Should -Not -Throw
        $stale = $script:VhcOrphanedSupersededCache.CandidateGroups | Where-Object { $_.Reason -eq 'StaleObject' }
        $stale | Should -BeNullOrEmpty
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`
Expected: FAIL — `GetObjectsInJob` doesn't exist as a member yet (first two new tests fail with a missing-method-style error or wrong-size assertion), and no `StaleObject` entries are ever cached.

- [ ] **Step 3: Implement the guard**

In `Get-VhcJob.ps1`, change:

```powershell
            } else {
                $LastBackup = $Job.GetLastBackup()
                if ($null -ne $LastBackup) {
                    $RestorePoints = @(Get-VBRRestorePoint -Backup $LastBackup)
                }
            }
            $TotalOnDiskGB = 0
```

to:

```powershell
            } else {
                $LastBackup = $Job.GetLastBackup()
                if ($null -ne $LastBackup) {
                    $RestorePoints = @(Get-VBRRestorePoint -Backup $LastBackup)
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
                    $CurrentObjectIds = [System.Collections.Generic.HashSet[string]]::new(
                        [string[]]($Job.GetObjectsInJob() | ForEach-Object { $_.ObjectId.ToString() }),
                        [System.StringComparer]::OrdinalIgnoreCase
                    )
                } catch {
                    $CurrentObjectIds = $null
                }

                if ($null -ne $CurrentObjectIds) {
                    $MatchedAny = $false
                    foreach ($RestorePoint in $RestorePoints) {
                        if ($CurrentObjectIds.Contains($RestorePoint.ObjectId.ToString())) { $MatchedAny = $true; break }
                    }

                    if ($MatchedAny) {
                        $ActiveRestorePoints = [System.Collections.Generic.List[object]]::new()
                        $StaleByObjectId     = @{}
                        foreach ($RestorePoint in $RestorePoints) {
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
                        $RestorePoints = $ActiveRestorePoints

                        foreach ($StalePoints in $StaleByObjectId.Values) {
                            [void]$script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
                                Reason        = 'StaleObject'
                                CurrentJobId  = $Job.Id.ToString()
                                RestorePoints = $StalePoints
                            })
                        }
                    }
                }
            }

            $TotalOnDiskGB = 0
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1' -Output Detailed"`
Expected: PASS (all tests, including every pre-existing one — verify no regression, since this touches the shared `$RestorePoints`/sizing path used by every job).

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.ps1 vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcJob.Tests.ps1
git commit -m "fix(jobs): exclude stale ObjectId restore points from sizing (fixes #197)"
```

---

## Task 3: New script `Get-VhcOrphanedSupersededBackups.ps1`

**Files:**
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.ps1`
- Create: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.Tests.ps1`

This script reads `$script:VhcOrphanedSupersededCache` (written by `Get-VhcJob.ps1` in Tasks 1-2), drops any `BackupId` group where `.GetBackup().IsTapeBackup` is `True`, resolves `JobName`/`OriginalJobType`/`RepositoryId` once per `BackupId` group via `.GetBackup().GetParentOrThis()`, splits into one row per `(BackupId, ObjectId)`, classifies each row Orphaned (`CurrentJobId` empty) or Superseded (`CurrentJobId` populated), and exports `_orphanedSupersededBackups.csv`.

- [ ] **Step 1: Write the failing tests**

Create `Get-VhcOrphanedSupersededBackups.Tests.ps1`:

```powershell
#Requires -Version 7.0
# Pester v5 tests for Get-VhcOrphanedSupersededBackups (#192).
#
# Unlike Get-VhcJob.Tests.ps1, this script's input is the
# $script:VhcOrphanedSupersededCache object Get-VhcJob.ps1 populates, not
# live VBR cmdlets - tests build that cache directly as a fixture rather
# than mocking Get-VBRJob/Get-VBRRestorePoint.

BeforeAll {
    if (-not (Get-Command Export-VhciCsv -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Export-VhciCsv { param([Parameter(ValueFromPipeline=$true)]$InputObject, [string]$FileName) process {} }
    }
    if (-not (Get-Command Add-VhciModuleError -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }
    }

    # Fake restore point carrying just what this script reads: Type,
    # CreationTimeUtc, ApproxSize, ObjectId, BackupId, Name, and a
    # GetBackup() chain to a fake Backup object exposing IsTapeBackup,
    # GetParentOrThis(), TypeToString, RepositoryId.
    function script:New-FakeBackupObject {
        param(
            [string]$Name = 'FakeJob',
            [string]$TypeToString = 'VMware Backup',
            [guid]$RepositoryId = [guid]::NewGuid(),
            [switch]$IsTapeBackup
        )
        $b = [PSCustomObject]@{
            Name          = $Name
            TypeToString  = $TypeToString
            RepositoryId  = $RepositoryId
            IsTapeBackup  = [bool]$IsTapeBackup
        }
        $b | Add-Member -MemberType ScriptMethod -Name GetParentOrThis -Value { return $this }
        return $b
    }

    function script:New-FakeCandidateRestorePoint {
        param(
            [string]$Name = 'FakeObject',
            [string]$Type = 'Increment',
            [guid]$ObjectId = [guid]::NewGuid(),
            [guid]$BackupId = [guid]::NewGuid(),
            [datetime]$CreationTimeUtc = (Get-Date),
            [double]$ApproxSize = 1GB,
            $BackupObject
        )
        $rp = [PSCustomObject]@{
            Name            = $Name
            Type            = $Type
            ObjectId        = $ObjectId
            BackupId        = $BackupId
            CreationTimeUtc = $CreationTimeUtc
            ApproxSize      = $ApproxSize
        }
        $rp | Add-Member -MemberType ScriptMethod -Name GetBackup -Value { return $BackupObject }.GetNewClosure()
        return $rp
    }

    $moduleRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $moduleRoot 'Public/Write-LogFile.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
}

Describe 'Get-VhcOrphanedSupersededBackups' {

    BeforeEach {
        Mock Write-LogFile       -MockWith { }
        Mock Add-VhciModuleError -MockWith { }
        # Two separate files get exported (data rows + a 1-row SweepRan
        # meta file, so C# can tell "sweep never ran" apart from "ran,
        # found nothing" - both look like an empty/missing CSV otherwise,
        # since Export-VhciCsv skips writing entirely when there are zero
        # rows). Discriminate by -FileName so the meta row never corrupts
        # $CapturedRows assertions below.
        Mock Export-VhciCsv -MockWith {
            if ($FileName -eq '_orphanedSupersededBackups.csv') {
                $script:CapturedRows = @($Input | ForEach-Object { $_ })
            } elseif ($FileName -eq '_orphanedSupersededBackupsMeta.csv') {
                $script:CapturedMeta = @($Input | ForEach-Object { $_ })
            }
        }
        $script:CapturedRows = $null
        $script:CapturedMeta = $null
    }

    It 'exports an Orphaned row for an Unresolved group, using the backup''s own retained name/type/repo' {
        $Backup = script:New-FakeBackupObject -Name 'Proxmox - Malware Lab' -TypeToString 'Proxmox Backup' -RepositoryId ([guid]'11111111-1111-1111-1111-111111111111')
        $Point  = script:New-FakeCandidateRestorePoint -Name 'pve-vm-201' -Type 'Full' -ApproxSize 10GB -BackupObject $Backup
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($Point)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -HaveCount 1
        $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        $script:CapturedRows[0].JobName | Should -Be 'Proxmox - Malware Lab'
        $script:CapturedRows[0].OriginalJobType | Should -Be 'Proxmox Backup'
        $script:CapturedRows[0].CurrentJobId | Should -Be ([guid]::Empty).ToString()
    }

    It 'exports a Superseded row for a Tier2Suppressed/StaleObject group, using the given CurrentJobId' {
        $Backup = script:New-FakeBackupObject -Name 'VBR Managed Agents - Windows' -TypeToString 'Windows Agent Policy'
        $Point  = script:New-FakeCandidateRestorePoint -Name 'WindowsAgent08' -Type 'Increment' -ApproxSize 8GB -BackupObject $Backup
        $RealJobId = [guid]::NewGuid()
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Tier2Suppressed'
            CurrentJobId  = $RealJobId.ToString()
            RestorePoints = @($Point)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -HaveCount 1
        $script:CapturedRows[0].Category | Should -Be 'Superseded'
        $script:CapturedRows[0].CurrentJobId | Should -Be $RealJobId.ToString()
    }

    It 'excludes a Tape Backup group entirely, regardless of Reason' {
        $Backup = script:New-FakeBackupObject -Name 'vm-vot-web02 Backup on Tape' -TypeToString 'Proxmox' -IsTapeBackup
        $Point  = script:New-FakeCandidateRestorePoint -Name 'vm-vot-web02' -BackupObject $Backup
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($Point)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -BeNullOrEmpty
    }

    It 'splits one BackupId group spanning multiple ObjectIds into one row per ObjectId' {
        $Backup = script:New-FakeBackupObject -Name 'Hyper-V - Test multiple VMs' -TypeToString 'Hyper-V Backup'
        $SharedBackupId = [guid]::NewGuid()
        $Point1 = script:New-FakeCandidateRestorePoint -Name 'vtestvm01' -Type 'Full' -ApproxSize 10GB -BackupId $SharedBackupId -BackupObject $Backup
        $Point2 = script:New-FakeCandidateRestorePoint -Name 'vtestvm02' -Type 'Full' -ApproxSize 12GB -BackupId $SharedBackupId -BackupObject $Backup
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($Point1, $Point2)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -HaveCount 2
        ($script:CapturedRows | Where-Object { $_.ObjectName -eq 'vtestvm01' }) | Should -HaveCount 1
        ($script:CapturedRows | Where-Object { $_.ObjectName -eq 'vtestvm02' }) | Should -HaveCount 1
        # Both rows share the same BackupId - it is not a unique key on its own.
        ($script:CapturedRows | Select-Object -ExpandProperty BackupId -Unique) | Should -HaveCount 1
    }

    It 'computes FullCount/IncrementalCount/AvgFullSizeBytes/AvgIncrementalSizeBytes/TotalSizeBytes correctly for one object' {
        $Backup = script:New-FakeBackupObject -Name 'VMware - Malware' -TypeToString 'VMware Backup'
        $ObjId = [guid]::NewGuid()
        $BkId  = [guid]::NewGuid()
        $Points = @(
            (script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Full' -ObjectId $ObjId -BackupId $BkId -ApproxSize 100GB -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-01-01')),
            (script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Full' -ObjectId $ObjId -BackupId $BkId -ApproxSize 120GB -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-03-01')),
            (script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Increment' -ObjectId $ObjId -BackupId $BkId -ApproxSize 10GB -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-02-01'))
        )
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = $Points
        })

        Get-VhcOrphanedSupersededBackups

        $row = $script:CapturedRows[0]
        [int]$row.FullCount | Should -Be 2
        [int]$row.IncrementalCount | Should -Be 1
        [double]$row.AvgFullSizeBytes | Should -Be 118111600640    # (100GB+120GB)/2 = 236223201280/2
        [double]$row.AvgIncrementalSizeBytes | Should -Be 10737418240   # 10GB
        [double]$row.TotalSizeBytes | Should -Be 246960619520    # 100GB+120GB+10GB = 230GB
        [datetime]$row.OldestRestorePoint | Should -Be (Get-Date '2026-01-01')
        [datetime]$row.NewestRestorePoint | Should -Be (Get-Date '2026-03-01')
    }

    It 'resolves RepositoryName from RepositoryId via -RepositoryDetails, matching Get-VhcJob''s own RepoName pattern' {
        $RepoId = [guid]::NewGuid()
        $Backup = script:New-FakeBackupObject -Name 'VMware - Malware' -TypeToString 'VMware Backup' -RepositoryId $RepoId
        $Point  = script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Full' -ApproxSize 10GB -BackupObject $Backup
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($Point)
        })
        $RepositoryDetails = @([PSCustomObject]@{ Id = $RepoId; Name = 'Repo01 (Local ReFS)' })

        Get-VhcOrphanedSupersededBackups -RepositoryDetails $RepositoryDetails

        $script:CapturedRows[0].RepositoryName | Should -Be 'Repo01 (Local ReFS)'
    }

    It 'leaves RepositoryName blank when -RepositoryDetails is not supplied' {
        $Backup = script:New-FakeBackupObject -Name 'VMware - Malware' -TypeToString 'VMware Backup'
        $Point  = script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Full' -ApproxSize 10GB -BackupObject $Backup
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($Point)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows[0].RepositoryName | Should -BeNullOrEmpty
    }

    It 'always exports a one-row meta CSV carrying SweepRan, even when there are zero data rows' {
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $false
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -BeNullOrEmpty
        $script:CapturedMeta | Should -HaveCount 1
        $script:CapturedMeta[0].SweepRan | Should -Be $false
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.Tests.ps1' -Output Detailed"`
Expected: FAIL — `Get-VhcOrphanedSupersededBackups` doesn't exist yet.

- [ ] **Step 3: Implement `Get-VhcOrphanedSupersededBackups.ps1`**

```powershell
#Requires -Version 5.1

function Get-VhcOrphanedSupersededBackups {
    <#
    .Synopsis
        Reads the $script:VhcOrphanedSupersededCache Get-VhcJob populates,
        excludes Tape Backups, classifies each remaining BackupId group's
        restore points as Orphaned or Superseded, splits into one row per
        (BackupId, ObjectId), and exports _orphanedSupersededBackups.csv.
    .Parameter RepositoryDetails
        ArrayList of [pscustomobject]@{ID; Name} rows returned by
        Get-VhcRepository - the same object Get-VhcJob takes, used the same
        way: resolving RepositoryId to a human-readable RepositoryName for
        the report's per-repo grouping. May be $null - RepositoryName will
        be blank in that case, matching Get-VhcJob's own RepoName behavior.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [object]$RepositoryDetails = $null
    )

    $message = "Collecting orphaned and superseded backups..."
    Write-LogFile $message

    $Rows = [System.Collections.Generic.List[object]]::new()

    if ($null -eq $script:VhcOrphanedSupersededCache) {
        Write-LogFile "No sweep cache available - Get-VhcJob did not run first, or found nothing to retain." -LogLevel "WARNING"
        return
    }

    if (-not $script:VhcOrphanedSupersededCache.SweepRan) {
        # Orphaned detection needs the global sweep; an environment made
        # entirely of ADR 0022 "safe" allowlist job types never triggers it.
        # This is an accepted gap (design doc, Q6/Q10) - Superseded/#197
        # coverage from the per-job stale-ObjectId guard is unaffected,
        # since that guard doesn't depend on the sweep having run, so
        # StaleObject candidates (if any) still export normally below.
        Write-LogFile "Global restore-point sweep did not run for this environment - Orphaned Backup detection not evaluated." -LogLevel "INFO"
    }

    foreach ($Candidate in $script:VhcOrphanedSupersededCache.CandidateGroups) {
        try {
            $RestorePoints = @($Candidate.RestorePoints)
            if ($RestorePoints.Count -eq 0) { continue }

            $Representative = $RestorePoints[0]
            $Backup = $null
            try { $Backup = $Representative.GetBackup() } catch { $Backup = $null }
            if ($null -eq $Backup) { continue }

            $ParentOrThis = $null
            try { $ParentOrThis = $Backup.GetParentOrThis() } catch { $ParentOrThis = $Backup }
            if ($null -eq $ParentOrThis) { $ParentOrThis = $Backup }

            # Tape exclusion (#192): checked on the immediate GetBackup()
            # object, not GetParentOrThis(), which walks past the tape-copy
            # relationship. Confirmed live: TypeToString is unreliable across
            # platforms for this (a Proxmox-to-tape copy reads "Proxmox", no
            # "Tape" substring), but IsTapeBackup is a consistent boolean at
            # this level.
            $IsTape = $false
            try { $IsTape = [bool]$Backup.IsTapeBackup } catch { $IsTape = $false }
            if ($IsTape) { continue }

            $JobName        = $null
            $OriginalType   = $null
            $RepositoryId   = $null
            try { $JobName      = $ParentOrThis.Name } catch {}
            try { $OriginalType = $ParentOrThis.TypeToString } catch {}
            try { $RepositoryId = $ParentOrThis.RepositoryId } catch {}

            # Same resolution Get-VhcJob.ps1 already does for its own
            # RepoName column - RepositoryId alone is a bare Guid with no
            # human-readable meaning to a report reader.
            $RepositoryName = $null
            if ($RepositoryDetails -and $RepositoryId) {
                $RepositoryName = $RepositoryDetails |
                    Where-Object { $_.Id -eq $RepositoryId } |
                    Select-Object -First 1 -ExpandProperty Name
            }

            $CurrentJobId = if ($Candidate.CurrentJobId) { $Candidate.CurrentJobId } else { [guid]::Empty.ToString() }
            $Category     = if ($Candidate.CurrentJobId) { 'Superseded' } else { 'Orphaned' }

            $ByObjectId = @{}
            foreach ($RestorePoint in $RestorePoints) {
                $ObjKey = $RestorePoint.ObjectId.ToString()
                if (-not $ByObjectId.ContainsKey($ObjKey)) {
                    $ByObjectId[$ObjKey] = [System.Collections.Generic.List[object]]::new()
                }
                $ByObjectId[$ObjKey].Add($RestorePoint)
            }

            foreach ($ObjKey in $ByObjectId.Keys) {
                $ObjectPoints = $ByObjectId[$ObjKey]
                $Fulls        = @($ObjectPoints | Where-Object { $_.Type -eq 'Full' })
                $Increments   = @($ObjectPoints | Where-Object { $_.Type -eq 'Increment' })
                $Sorted       = $ObjectPoints | Sort-Object CreationTimeUtc

                $AvgFullSize = if ($Fulls.Count -gt 0) {
                    ($Fulls | Measure-Object -Property ApproxSize -Average).Average
                } else { 0 }
                $AvgIncrementalSize = if ($Increments.Count -gt 0) {
                    ($Increments | Measure-Object -Property ApproxSize -Average).Average
                } else { 0 }
                $TotalSize = ($ObjectPoints | Measure-Object -Property ApproxSize -Sum).Sum

                $Rows.Add([PSCustomObject]@{
                    RepositoryId             = $RepositoryId
                    RepositoryName           = $RepositoryName
                    JobName                  = $JobName
                    CurrentJobId             = $CurrentJobId
                    Category                 = $Category
                    OriginalJobType          = $OriginalType
                    ObjectId                 = $ObjectPoints[0].ObjectId
                    BackupId                 = $ObjectPoints[0].BackupId
                    ObjectName               = $ObjectPoints[0].Name
                    FullCount                = $Fulls.Count
                    IncrementalCount         = $Increments.Count
                    AvgFullSizeBytes         = $AvgFullSize
                    AvgIncrementalSizeBytes  = $AvgIncrementalSize
                    TotalSizeBytes           = $TotalSize
                    OldestRestorePoint       = $Sorted[0].CreationTimeUtc
                    NewestRestorePoint       = $Sorted[-1].CreationTimeUtc
                })
            }
        } catch {
            Write-LogFile "Could not process an orphaned/superseded candidate group: $($_.Exception.Message)" -LogLevel "WARNING"
            Add-VhciModuleError -CollectorName 'OrphanedSupersededBackups' -ErrorMessage $_.Exception.Message
        }
    }

    $Rows | Export-VhciCsv -FileName '_orphanedSupersededBackups.csv'

    # Meta file, always exactly one row: Export-VhciCsv skips writing
    # entirely when there are zero rows to export, so an empty/missing
    # _orphanedSupersededBackups.csv can't distinguish "sweep never ran"
    # from "ran, found nothing." This one-row file always exports (never
    # zero rows), giving the C# side a real signal to tell them apart.
    [PSCustomObject]@{
        SweepRan = $script:VhcOrphanedSupersededCache.SweepRan
    } | Export-VhciCsv -FileName '_orphanedSupersededBackupsMeta.csv'
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.Tests.ps1' -Output Detailed"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.ps1 vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcOrphanedSupersededBackups.Tests.ps1
git commit -m "feat(jobs): add Get-VhcOrphanedSupersededBackups collector (#192)"
```

---

## Task 4: Module wiring + golden baseline schema

**Files:**
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1`
- Modify: `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1:273` (current line — call site right after `Get-VhcJob`'s)
- Create: `vHC/HC_Reporting/Tools/GoldenBaselines/ObjectSchemas/_orphanedSupersededBackups.schema.json`

Dropping a new file in `Public/` is not enough by itself — `vHC-VbrConfig.Manifest.Tests.ps1` (ISC-8/9) fails red if a `Public/*.ps1` basename is missing from `FunctionsToExport` in the `.psd1`, or vice versa.

- [ ] **Step 1: Run the manifest test to confirm it currently fails red for the missing export**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.Manifest.Tests.ps1' -Output Detailed"`
Expected: FAIL — the ISC-8-style test reports `Get-VhcOrphanedSupersededBackups` present in `Public/` but missing from `FunctionsToExport`.

- [ ] **Step 2: Add the new function to `FunctionsToExport`**

Open `vHC-VbrConfig.psd1`, find the `FunctionsToExport` array (contains `'Get-VhcJob'` among ~24 entries), and add `'Get-VhcOrphanedSupersededBackups'` as a new entry, keeping the existing alphabetical-ish ordering — insert it immediately after `'Get-VhcJob'`:

```powershell
        'Get-VhcJob',
        'Get-VhcOrphanedSupersededBackups',
```

- [ ] **Step 3: Run the manifest test again to confirm it passes**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.Manifest.Tests.ps1' -Output Detailed"`
Expected: PASS.

- [ ] **Step 4: Add the collector call site in `Get-VBRConfig.ps1`**

Find (line 273 currently):

```powershell
# Task 7: Job collectors (require $RepositoryDetails from Task 6)
$collectorResults.Add((Invoke-VhcCollector -Name 'Jobs' -Action {
    Get-VhcJob -RepositoryDetails $RepositoryDetails -VBRVersion $VBRVersion
}))
```

This block is followed by a `# ---...` separator comment marking the end
of the "Task 7" section in the file's existing numbering. Add the new
call site **after that separator** (as its own clearly-delimited block,
not squeezed between the `Jobs` collector and its trailing separator):

```powershell
# Orphaned/Superseded backups (#192) - depends on the $script:VhcOrphanedSupersededCache
# Get-VhcJob populates above; must run after it in the same collection pass.
# -RepositoryDetails is the same variable Get-VhcJob uses above, resolving
# RepositoryId -> a human-readable name for the report's per-repo grouping.
$collectorResults.Add((Invoke-VhcCollector -Name 'OrphanedSupersededBackups' -Action {
    Get-VhcOrphanedSupersededBackups -RepositoryDetails $RepositoryDetails
}))
```

Note: `-CollectorName 'OrphanedSupersededBackups'` in the new script's `Add-VhciModuleError` calls (Task 3) must exactly match this `-Name 'OrphanedSupersededBackups'` string — it already does.

- [ ] **Step 5: Create the golden baseline schema**

Create `vHC/HC_Reporting/Tools/GoldenBaselines/ObjectSchemas/_orphanedSupersededBackups.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Orphaned & Superseded Backups CSV Schema",
  "description": "Maps _orphanedSupersededBackups.csv to the dynamic-CSV read path (CCsvParser.GetDynamicOrphanedSupersededBackups)",
  "csvFile": "_orphanedSupersededBackups.csv",
  "csharpClass": "VeeamHealthCheck.Functions.Reporting.CsvHandlers.CCsvParser",
  "csharpFile": "Functions/Reporting/CsvHandlers/CCsvParser.cs",
  "version": "1.0.0",
  "columns": [
    { "index": 0, "csvName": "RepositoryId", "csharpProperty": "RepositoryId", "csharpType": "string", "nullable": true, "description": "Repository Id the backup lives on" },
    { "index": 1, "csvName": "RepositoryName", "csharpProperty": "RepositoryName", "csharpType": "string", "nullable": true, "description": "Repository display name, resolved from RepositoryId via -RepositoryDetails; blank if resolution failed" },
    { "index": 2, "csvName": "JobName", "csharpProperty": "JobName", "csharpType": "string", "nullable": false, "description": "Job name, from the backup's own retained metadata" },
    { "index": 3, "csvName": "CurrentJobId", "csharpProperty": "CurrentJobId", "csharpType": "string", "nullable": false, "description": "Zeroed Guid for Orphaned rows; a real current Job Id for Superseded rows" },
    { "index": 4, "csvName": "Category", "csharpProperty": "Category", "csharpType": "string", "nullable": false, "description": "Orphaned or Superseded" },
    { "index": 5, "csvName": "OriginalJobType", "csharpProperty": "OriginalJobType", "csharpType": "string", "nullable": true, "description": "TypeToString, e.g. Proxmox Backup, VMware Backup" },
    { "index": 6, "csvName": "ObjectId", "csharpProperty": "ObjectId", "csharpType": "string", "nullable": false, "description": "Protected object Id - the report-facing identifier" },
    { "index": 7, "csvName": "BackupId", "csharpProperty": "BackupId", "csharpType": "string", "nullable": false, "description": "Not unique on its own - multiple rows can share one when per-VM chains are disabled" },
    { "index": 8, "csvName": "ObjectName", "csharpProperty": "ObjectName", "csharpType": "string", "nullable": true, "description": "Source VM/machine name" },
    { "index": 9, "csvName": "FullCount", "csharpProperty": "FullCount", "csharpType": "int", "nullable": false, "description": "Full restore point count for this ObjectId" },
    { "index": 10, "csvName": "IncrementalCount", "csharpProperty": "IncrementalCount", "csharpType": "int", "nullable": false, "description": "Incremental restore point count for this ObjectId" },
    { "index": 11, "csvName": "AvgFullSizeBytes", "csharpProperty": "AvgFullSizeBytes", "csharpType": "double", "nullable": false, "description": "Average size of full restore points, bytes" },
    { "index": 12, "csvName": "AvgIncrementalSizeBytes", "csharpProperty": "AvgIncrementalSizeBytes", "csharpType": "double", "nullable": false, "description": "Average size of incremental restore points, bytes" },
    { "index": 13, "csvName": "TotalSizeBytes", "csharpProperty": "TotalSizeBytes", "csharpType": "double", "nullable": false, "description": "Sum of all retained restore points for this ObjectId, bytes" },
    { "index": 14, "csvName": "OldestRestorePoint", "csharpProperty": "OldestRestorePoint", "csharpType": "DateTime", "nullable": false, "description": "Oldest restore point CreationTimeUtc for this ObjectId" },
    { "index": 15, "csvName": "NewestRestorePoint", "csharpProperty": "NewestRestorePoint", "csharpType": "DateTime", "nullable": false, "description": "Newest restore point CreationTimeUtc for this ObjectId" }
  ],
  "validationRules": {
    "requiredColumns": ["JobName", "Category", "ObjectId", "BackupId"],
    "guidColumns": ["RepositoryId", "CurrentJobId", "ObjectId", "BackupId"],
    "numericColumns": ["FullCount", "IncrementalCount", "AvgFullSizeBytes", "AvgIncrementalSizeBytes", "TotalSizeBytes"]
  }
}
```

- [ ] **Step 6: Commit**

```bash
git add vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/vHC-VbrConfig.psd1 vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1 vHC/HC_Reporting/Tools/GoldenBaselines/ObjectSchemas/_orphanedSupersededBackups.schema.json
git commit -m "chore(jobs): wire Get-VhcOrphanedSupersededBackups into the module and collection pipeline (#192)"
```

---

## Task 5: C# dynamic CSV read

**Files:**
- Modify: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs`

Follows the exact `nasBackup`/`GetDynamicNasBackup()` pattern already in this file — a bare token field plus a one-line wrapper around the existing generic `VbrGetDynamicCsvRecs`.

- [ ] **Step 1: Add the token field and wrapper**

Near the existing `public readonly string nasBackup = "nasBackup";` field, add:

```csharp
public readonly string orphanedSupersededBackups = "orphanedSupersededBackups";
```

Near the existing `GetDynamicNasBackup()` method, add:

```csharp
public IEnumerable<dynamic> GetDynamicOrphanedSupersededBackups()
{
    return this.VbrGetDynamicCsvRecs(this.orphanedSupersededBackups, CVariables.vbrDir);
}
```

Add a second token field and wrapper for the meta file (Task 3's `_orphanedSupersededBackupsMeta.csv`, which carries the `SweepRan` flag so the HTML/JSON layer can distinguish "not evaluated" from "evaluated, found nothing" — an empty/missing data CSV alone can't tell those apart):

```csharp
public readonly string orphanedSupersededBackupsMeta = "orphanedSupersededBackupsMeta";

public IEnumerable<dynamic> GetDynamicOrphanedSupersededBackupsMeta()
{
    return this.VbrGetDynamicCsvRecs(this.orphanedSupersededBackupsMeta, CVariables.vbrDir);
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: 0 errors. There's no dedicated unit test for either method alone (matching `GetDynamicNasBackup`'s own precedent, which has no dedicated test either) — `GetDynamicOrphanedSupersededBackups` is exercised indirectly in Task 8's manual smoke test once wired into the renderer; `OrphanedSupersededBackupAggregator`'s own tests (Task 6) construct `dynamic` rows directly rather than going through this parser, since the Aggregator's contract is "given dynamic rows shaped like this CSV," not "given a live CSV file."

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs
git commit -m "feat(reporting): add dynamic CSV read for orphaned/superseded backups (#192)"
```

---

## Task 6: DTOs + Aggregator + xUnit tests

**Files:**
- Create: `vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededObjectRecord.cs`
- Create: `vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededBackupRecord.cs`
- Create: `vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededBackupAggregator.cs`
- Test: `vHC/VhcXTests/Functions/Reporting/DataFormers/OrphanedSupersededBackups/OrphanedSupersededBackupAggregatorTests.cs`

`OrphanedSupersededBackupAggregator.Build(IEnumerable<dynamic> rows)` groups the CSV's `(BackupId, ObjectId)`-grain rows first by `(RepositoryId, JobName, CurrentJobId, Category)` into one `OrphanedSupersededBackupRecord` per job, with each row becoming a nested `OrphanedSupersededObjectRecord`. Job-level `FullCount`/`IncrementalCount`/`TotalSizeBytes` are sums across objects; `OldestRestorePoint`/`NewestRestorePoint` are min/max. This mirrors `AgentJobAggregator`'s grouping/rollup shape, adapted to consume `dynamic` CSV rows (per the design's dynamic-path decision) instead of a strongly-typed CSV DTO.

- [ ] **Step 1: Write the failing tests**

Create `OrphanedSupersededBackupAggregatorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using Xunit;

namespace VhcXTests.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    [Trait("Category", "OrphanedSupersededBackups")]
    public class OrphanedSupersededBackupAggregatorTests
    {
        private static dynamic Row(
            string repositoryId, string repositoryName, string jobName, string currentJobId, string category,
            string originalJobType, string objectId, string backupId, string objectName,
            int fullCount, int incrementalCount, double avgFull, double avgIncremental,
            double totalSize, DateTime oldest, DateTime newest)
        {
            dynamic row = new ExpandoObject();
            row.RepositoryId = repositoryId;
            row.RepositoryName = repositoryName;
            row.JobName = jobName;
            row.CurrentJobId = currentJobId;
            row.Category = category;
            row.OriginalJobType = originalJobType;
            row.ObjectId = objectId;
            row.BackupId = backupId;
            row.ObjectName = objectName;
            row.FullCount = fullCount.ToString();
            row.IncrementalCount = incrementalCount.ToString();
            row.AvgFullSizeBytes = avgFull.ToString();
            row.AvgIncrementalSizeBytes = avgIncremental.ToString();
            row.TotalSizeBytes = totalSize.ToString();
            row.OldestRestorePoint = oldest.ToString("O");
            row.NewestRestorePoint = newest.ToString("O");
            return row;
        }

        [Fact]
        public void Build_SingleObjectRow_ProducesOneJobRecordWithOneObject()
        {
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", Guid.Empty.ToString(), "Orphaned",
                    "Proxmox Backup", "obj-1", "backup-1", "pve-vm-201",
                    3, 42, 12_000_000_000, 500_000_000, 540_000_000_000,
                    new DateTime(2025, 11, 2), new DateTime(2026, 3, 15))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Single(result);
            Assert.Equal("Proxmox - Malware Lab", result[0].JobName);
            Assert.Equal("Repo01 (Local ReFS)", result[0].RepositoryName);
            Assert.Equal("Orphaned", result[0].Category);
            Assert.Single(result[0].Objects);
            Assert.Equal("pve-vm-201", result[0].Objects[0].ObjectName);
        }

        [Fact]
        public void Build_TwoObjectsSharingOneJob_RollsUpToOneJobRecordWithTwoObjects()
        {
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "job-1", "Superseded",
                    "Windows Agent Policy", "obj-7", "backup-7", "WindowsAgent07",
                    1, 5, 8_000_000_000, 100_000_000, 48_000_000_000,
                    new DateTime(2026, 3, 1), new DateTime(2026, 3, 6)),
                Row("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "job-1", "Superseded",
                    "Windows Agent Policy", "obj-8", "backup-7", "WindowsAgent08",
                    1, 2, 9_000_000_000, 90_000_000, 12_000_000_000,
                    new DateTime(2026, 1, 1), new DateTime(2026, 1, 10))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Single(result);
            var job = result[0];
            Assert.Equal(2, job.FullCount);
            Assert.Equal(7, job.IncrementalCount);
            Assert.Equal(60_000_000_000, job.TotalSizeBytes);
            Assert.Equal(new DateTime(2026, 1, 1), job.OldestRestorePoint);
            Assert.Equal(new DateTime(2026, 3, 6), job.NewestRestorePoint);
            Assert.Equal(2, job.Objects.Count);
        }

        [Fact]
        public void Build_DifferentCategoriesForSameJobName_ProducesSeparateRecords()
        {
            // Same BackupId group could in principle produce an Orphaned row
            // (no current job) and a different group could separately name-match
            // a real job of the same display name after a rebuild - Category is
            // part of the grouping key so these never silently merge.
            var rows = new List<dynamic>
            {
                Row("repo-1", "Repo01 (Local ReFS)", "Windows01", Guid.Empty.ToString(), "Orphaned",
                    "VMware Backup", "obj-old", "backup-old", "Windows01",
                    1, 0, 50_000_000_000, 0, 50_000_000_000,
                    new DateTime(2026, 3, 1), new DateTime(2026, 3, 1)),
                Row("repo-1", "Repo01 (Local ReFS)", "Windows01", "job-new", "Superseded",
                    "VMware Backup", "obj-new", "backup-new", "Windows01",
                    1, 0, 55_000_000_000, 0, 55_000_000_000,
                    new DateTime(2026, 7, 1), new DateTime(2026, 7, 1))
            };

            var result = OrphanedSupersededBackupAggregator.Build(rows);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Category == "Orphaned");
            Assert.Contains(result, r => r.Category == "Superseded");
        }

        [Fact]
        public void Build_EmptyInput_ReturnsEmptyList()
        {
            var result = OrphanedSupersededBackupAggregator.Build(new List<dynamic>());

            Assert.Empty(result);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "Category=OrphanedSupersededBackups"`
Expected: FAIL to compile — `OrphanedSupersededBackupAggregator` doesn't exist yet.

- [ ] **Step 3: Implement the DTOs**

Create `OrphanedSupersededObjectRecord.cs`:

```csharp
using System;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    public class OrphanedSupersededObjectRecord
    {
        public string ObjectId { get; set; }
        public string BackupId { get; set; }
        public string ObjectName { get; set; }
        public int FullCount { get; set; }
        public int IncrementalCount { get; set; }
        public double AvgFullSizeBytes { get; set; }
        public double AvgIncrementalSizeBytes { get; set; }
        public double TotalSizeBytes { get; set; }
        public DateTime OldestRestorePoint { get; set; }
        public DateTime NewestRestorePoint { get; set; }
    }
}
```

Create `OrphanedSupersededBackupRecord.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    public class OrphanedSupersededBackupRecord
    {
        public string RepositoryId { get; set; }
        public string RepositoryName { get; set; }
        public string JobName { get; set; }
        public string CurrentJobId { get; set; }
        public string Category { get; set; }
        public string OriginalJobType { get; set; }
        public int FullCount { get; set; }
        public int IncrementalCount { get; set; }
        public double TotalSizeBytes { get; set; }
        public DateTime OldestRestorePoint { get; set; }
        public DateTime NewestRestorePoint { get; set; }
        public List<OrphanedSupersededObjectRecord> Objects { get; set; } = new();
    }
}
```

- [ ] **Step 4: Implement the Aggregator**

Create `OrphanedSupersededBackupAggregator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups
{
    public static class OrphanedSupersededBackupAggregator
    {
        public static List<OrphanedSupersededBackupRecord> Build(IEnumerable<dynamic> rows)
        {
            var result = new List<OrphanedSupersededBackupRecord>();
            if (rows == null)
            {
                return result;
            }

            var groups = rows
                .Select(MapRow)
                .Where(r => r != null)
                .GroupBy(r => (r.RepositoryId, r.JobName, r.CurrentJobId, r.Category));

            foreach (var group in groups)
            {
                var objects = group.Select(r => r.ObjectRecord).ToList();

                var record = new OrphanedSupersededBackupRecord
                {
                    RepositoryId = group.Key.RepositoryId,
                    RepositoryName = group.First().RepositoryName,
                    JobName = group.Key.JobName,
                    CurrentJobId = group.Key.CurrentJobId,
                    Category = group.Key.Category,
                    OriginalJobType = group.First().OriginalJobType,
                    FullCount = objects.Sum(o => o.FullCount),
                    IncrementalCount = objects.Sum(o => o.IncrementalCount),
                    TotalSizeBytes = objects.Sum(o => o.TotalSizeBytes),
                    OldestRestorePoint = objects.Min(o => o.OldestRestorePoint),
                    NewestRestorePoint = objects.Max(o => o.NewestRestorePoint),
                    Objects = objects,
                };

                result.Add(record);
            }

            return result;
        }

        private static MappedRow MapRow(dynamic row)
        {
            try
            {
                var obj = new OrphanedSupersededObjectRecord
                {
                    ObjectId = row.ObjectId,
                    BackupId = row.BackupId,
                    ObjectName = row.ObjectName,
                    FullCount = int.Parse((string)row.FullCount, CultureInfo.InvariantCulture),
                    IncrementalCount = int.Parse((string)row.IncrementalCount, CultureInfo.InvariantCulture),
                    AvgFullSizeBytes = double.Parse((string)row.AvgFullSizeBytes, CultureInfo.InvariantCulture),
                    AvgIncrementalSizeBytes = double.Parse((string)row.AvgIncrementalSizeBytes, CultureInfo.InvariantCulture),
                    TotalSizeBytes = double.Parse((string)row.TotalSizeBytes, CultureInfo.InvariantCulture),
                    OldestRestorePoint = DateTime.Parse((string)row.OldestRestorePoint, CultureInfo.InvariantCulture),
                    NewestRestorePoint = DateTime.Parse((string)row.NewestRestorePoint, CultureInfo.InvariantCulture),
                };

                return new MappedRow
                {
                    RepositoryId = row.RepositoryId,
                    RepositoryName = row.RepositoryName,
                    JobName = row.JobName,
                    CurrentJobId = row.CurrentJobId,
                    Category = row.Category,
                    OriginalJobType = row.OriginalJobType,
                    ObjectRecord = obj,
                };
            }
            catch
            {
                return null;
            }
        }

        private class MappedRow
        {
            public string RepositoryId { get; set; }
            public string RepositoryName { get; set; }
            public string JobName { get; set; }
            public string CurrentJobId { get; set; }
            public string Category { get; set; }
            public string OriginalJobType { get; set; }
            public OrphanedSupersededObjectRecord ObjectRecord { get; set; }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "Category=OrphanedSupersededBackups"`
Expected: PASS (4/4).

- [ ] **Step 6: Commit**

```bash
git add vHC/HC_Reporting/Functions/Reporting/DataFormers/OrphanedSupersededBackups/ vHC/VhcXTests/Functions/Reporting/DataFormers/OrphanedSupersededBackups/
git commit -m "feat(reporting): add OrphanedSupersededBackupAggregator (#192)"
```

---

## Task 7: JSON export property

**Files:**
- Modify: `vHC/HC_Reporting/Functions/Reporting/DataTypes/CFullReportJson.cs`

`HtmlSection`/`SetSection` is hard-typed to a flat `List<List<string>>` — there's no precedent anywhere in this codebase for a nested array inside a JSON section row, and extending `HtmlSection` itself would change the contract every other section relies on. Instead, add a dedicated property to `CFullReportJson`, alongside the existing (currently-unused) `cProtectedWorkloads` property — this reuses an existing, if dormant, pattern (a typed property sitting next to the generic `Sections` dictionary) rather than inventing a new one.

A second, sibling property carries whether Orphaned detection was actually evaluated (ADR 0025) — the bare `OrphanedSupersededBackups` list has no way to represent "not evaluated" versus "evaluated, found nothing" on its own, and a JSON consumer needs to tell those apart exactly as much as an HTML reader does.

- [ ] **Step 1: Add the properties**

In `CFullReportJson.cs`, find:

```csharp
internal class CFullReportJson
{
    public CProtectedWorkloads cProtectedWorkloads { get; set; }
```

Change to:

```csharp
internal class CFullReportJson
{
    public CProtectedWorkloads cProtectedWorkloads { get; set; }
    public System.Collections.Generic.List<VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups.OrphanedSupersededBackupRecord> OrphanedSupersededBackups { get; set; } = new();
    public bool OrphanedBackupsSweepEvaluated { get; set; } = true;
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add vHC/HC_Reporting/Functions/Reporting/DataTypes/CFullReportJson.cs
git commit -m "feat(reporting): add OrphanedSupersededBackups JSON property (#192)"
```

---

## Task 8: HTML renderer + wiring

**Files:**
- Create: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/OrphanedSupersededBackups/COrphanedSupersededBackupsTable.cs`
- Test: `vHC/VhcXTests/Functions/Reporting/Html/VBR/VbrTables/OrphanedSupersededBackups/COrphanedSupersededBackupsTableTests.cs`
- Modify: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` — new `AddOrphanedSupersededBackupsTable(bool scrub)` method.
- Modify: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs` — new wrapper + call site.
- Modify: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:530-535` — new sidebar nav link.

Verified: `BuildSidebar()` (not `MakeNavTable()`, which has no live caller in the VBR report path) is the actual sidebar-rendering method. Verified: no per-row expand/collapse exists anywhere in this codebase — this genuinely needs new JS/CSS (Task 9), reusing only the *idiom* (inline-style toggling, matching `ReportScript.js`'s "Legacy Collapsible Toggle" pattern for bare tables) rather than any specific existing function.

This codebase has an extensive per-renderer test convention (`CCloudGatewaysTableTests`, `CComplianceTableJsonTests`, `CUserRolesTableScrubTests`, etc.) — `Render(records, sweepEvaluated, scrub, out summary)` is a pure function taking already-loaded data, so it's testable with in-memory fixtures directly, no CSV-writing test harness needed. Written test-first below.

- [ ] **Step 1: Write the failing tests**

Create `COrphanedSupersededBackupsTableTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups
{
    /// <summary>
    /// Render(records, sweepEvaluated, scrub, out summary) is a pure function -
    /// no CSV or CGlobals involved - so these build fixtures directly in memory
    /// rather than following VbrTableScrubTestBase's CSV-writing pattern.
    /// </summary>
    [Trait("Category", "OrphanedSupersededBackups")]
    public class COrphanedSupersededBackupsTableTests
    {
        private static OrphanedSupersededBackupRecord JobRecord(
            string repositoryId, string repositoryName, string jobName, string category)
        {
            return new OrphanedSupersededBackupRecord
            {
                RepositoryId = repositoryId,
                RepositoryName = repositoryName,
                JobName = jobName,
                CurrentJobId = category == "Orphaned" ? Guid.Empty.ToString() : Guid.NewGuid().ToString(),
                Category = category,
                OriginalJobType = "VMware Backup",
                FullCount = 1,
                IncrementalCount = 1,
                TotalSizeBytes = 1_000_000_000,
                OldestRestorePoint = new DateTime(2026, 1, 1),
                NewestRestorePoint = new DateTime(2026, 2, 1),
                Objects = new List<OrphanedSupersededObjectRecord>
                {
                    new OrphanedSupersededObjectRecord
                    {
                        ObjectId = Guid.NewGuid().ToString(),
                        BackupId = Guid.NewGuid().ToString(),
                        ObjectName = "pve-vm-201",
                        FullCount = 1,
                        IncrementalCount = 1,
                        AvgFullSizeBytes = 500_000_000,
                        AvgIncrementalSizeBytes = 500_000_000,
                        TotalSizeBytes = 1_000_000_000,
                        OldestRestorePoint = new DateTime(2026, 1, 1),
                        NewestRestorePoint = new DateTime(2026, 2, 1),
                    }
                }
            };
        }

        [Fact]
        public void Render_NoRecordsAndSweepEvaluated_ShowsNoDataMessage()
        {
            var table = new COrphanedSupersededBackupsTable();

            string html = table.Render(new List<OrphanedSupersededBackupRecord>(), sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("No orphaned or superseded backups detected", html);
            Assert.DoesNotContain("not evaluated", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Render_NoRecordsAndSweepNotEvaluated_ShowsNotEvaluatedMessage()
        {
            var table = new COrphanedSupersededBackupsTable();

            string html = table.Render(new List<OrphanedSupersededBackupRecord>(), sweepEvaluated: false, scrub: false, out string summary);

            Assert.Contains("not evaluated", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Render_RecordsPresentAndSweepNotEvaluated_StillShowsNotEvaluatedNotice()
        {
            // Regression test for the exact gap review caught: a pure
            // safe-allowlist environment with a rebuilt machine has
            // Superseded rows (the stale-ObjectId guard runs unconditionally,
            // Task 2) even though SweepRan is false, so Orphaned coverage was
            // never evaluated. The notice must not get skipped just because
            // there happens to be other data to show.
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "VBR Managed Agents - Windows", "Superseded")
            };

            string html = table.Render(records, sweepEvaluated: false, scrub: false, out string summary);

            Assert.Contains("not evaluated", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("VBR Managed Agents - Windows", html);
        }

        [Fact]
        public void Render_RecordsPresentAndSweepEvaluated_DoesNotShowNotEvaluatedNotice()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            Assert.DoesNotContain("not evaluated", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Proxmox - Malware Lab", html);
        }

        [Fact]
        public void Render_GroupsByRepository_DisplaysRepositoryNameNotRawGuid()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("11111111-1111-1111-1111-111111111111", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("Repo01 (Local ReFS)", html);
            Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", html);
        }

        [Fact]
        public void Render_RepositoryNameMissing_FallsBackToRepositoryId()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("11111111-1111-1111-1111-111111111111", null, "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: false, out string summary);

            Assert.Contains("11111111-1111-1111-1111-111111111111", html);
        }

        [Fact]
        public void Render_ScrubTrue_AnonymizesJobAndObjectNames()
        {
            var table = new COrphanedSupersededBackupsTable();
            var records = new List<OrphanedSupersededBackupRecord>
            {
                JobRecord("repo-1", "Repo01 (Local ReFS)", "Proxmox - Malware Lab", "Orphaned")
            };

            string html = table.Render(records, sweepEvaluated: true, scrub: true, out string summary);

            Assert.DoesNotContain("Proxmox - Malware Lab", html);
            Assert.DoesNotContain("pve-vm-201", html);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "Category=OrphanedSupersededBackups"`
Expected: FAIL to compile — `COrphanedSupersededBackupsTable` doesn't exist yet.

- [ ] **Step 3: Implement the renderer class**

Create `COrphanedSupersededBackupsTable.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups
{
    internal class COrphanedSupersededBackupsTable
    {
        // No try/catch here: a real parse failure must propagate to the
        // caller (AddOrphanedSupersededBackupsTable), which already logs
        // via this.log.Error and sets a fallback summary. A previous draft
        // swallowed exceptions here too, which meant a genuine CSV-corruption
        // error rendered as "No orphaned or superseded backups detected"
        // with nothing in the logs to explain why - defeating the caller's
        // own error handling two lines away.
        public List<OrphanedSupersededBackupRecord> LoadRecords()
        {
            CCsvParser parser = new();
            var rows = parser.GetDynamicOrphanedSupersededBackups();
            return OrphanedSupersededBackupAggregator.Build(rows);
        }

        // Reads the 1-row meta CSV Task 3's script always exports, so an
        // empty/missing data CSV can be told apart from "the global sweep
        // never ran for this environment" rather than assumed to mean
        // "evaluated, nothing found." Defaults to true (assume evaluated)
        // if the meta file itself is missing or malformed, since that's
        // the more common/expected state and avoids a false "not
        // evaluated" banner on every report if this file has an issue.
        public bool WasSweepEvaluated()
        {
            try
            {
                CCsvParser parser = new();
                var metaRows = parser.GetDynamicOrphanedSupersededBackupsMeta().ToList();
                if (metaRows.Count == 0)
                {
                    return true;
                }
                return bool.TryParse((string)metaRows[0].SweepRan.ToString(), out bool sweepRan) && sweepRan;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public string Render(List<OrphanedSupersededBackupRecord> records, bool sweepEvaluated, bool scrub, out string summary)
        {
            // Computed once, prepended regardless of whether there's other
            // data to show - a previous draft only consulted sweepEvaluated
            // inside the records.Count == 0 branch, so a pure-safe-allowlist
            // environment with Superseded rows (the stale-ObjectId guard runs
            // unconditionally, Task 2) rendered a normal-looking table with
            // no mention that Orphaned coverage was never evaluated. See the
            // regression test Render_RecordsPresentAndSweepNotEvaluated_StillShowsNotEvaluatedNotice.
            string notEvaluatedNotice = sweepEvaluated
                ? ""
                : "<p class=\"label\">Orphaned Backup detection was not evaluated for this environment " +
                  "(no job types required the global restore-point sweep). Superseded Backup detection " +
                  "is unaffected and runs regardless.</p>";

            if (records == null || records.Count == 0)
            {
                summary = sweepEvaluated
                    ? "No orphaned or superseded backups detected."
                    : "Orphaned Backups: not evaluated for this environment. No Superseded backups detected.";
                return notEvaluatedNotice + "<p>No orphaned or superseded backups detected for this environment.</p>";
            }

            string s = notEvaluatedNotice;
            var byRepo = records.GroupBy(r => r.RepositoryId ?? "unknown");
            long grandTotalBytes = 0;
            int grandTotalCount = 0;

            foreach (var repoGroup in byRepo)
            {
                var repoRecords = repoGroup.ToList();
                double repoTotalGb = repoRecords.Sum(r => r.TotalSizeBytes) / 1073741824d;
                grandTotalBytes += (long)repoRecords.Sum(r => r.TotalSizeBytes);
                grandTotalCount += repoRecords.Count;

                // Group by RepositoryId (stable, always present) but display
                // RepositoryName - a bare Guid is meaningless as a section
                // header. Falls back to the Guid if resolution failed for
                // every record in the group (Get-VhcOrphanedSupersededBackups
                // couldn't resolve it via -RepositoryDetails).
                string repoLabel;
                if (scrub)
                {
                    repoLabel = "Repository (scrubbed)";
                }
                else
                {
                    var resolvedName = repoRecords.Find(r => !string.IsNullOrEmpty(r.RepositoryName))?.RepositoryName;
                    repoLabel = resolvedName ?? repoGroup.Key;
                }

                s += $"<div class=\"orphaned-repo-group\">";
                s += $"<div class=\"orphaned-repo-header\"><strong>{repoLabel}</strong>";
                s += $"<span class=\"label\">{repoRecords.Count} backups flagged &middot; ~{repoTotalGb:N0} GB potentially reclaimable</span></div>";
                s += "<table><thead><tr>";
                s += "<th></th><th>Job Name</th><th>Status</th><th>Original Job Type</th><th>Fulls</th><th>Incrementals</th><th>Total Size</th><th>Oldest RP</th><th>Newest RP</th>";
                s += "</tr></thead><tbody>";

                foreach (var job in repoRecords.OrderBy(r => r.OldestRestorePoint))
                {
                    string jobName = scrub ? "Job (scrubbed)" : job.JobName;
                    double totalGb = job.TotalSizeBytes / 1073741824d;
                    string badgeClass = job.Category == "Orphaned" ? "badge-orphaned" : "badge-superseded";

                    s += "<tr class=\"detail-toggle\" onclick=\"toggleDetailRow(this)\">";
                    s += "<td>&#9656;</td>";
                    s += $"<td>{jobName}</td>";
                    s += $"<td><span class=\"badge {badgeClass}\">{job.Category}</span></td>";
                    s += $"<td>{job.OriginalJobType}</td>";
                    s += $"<td>{job.FullCount}</td>";
                    s += $"<td>{job.IncrementalCount}</td>";
                    s += $"<td>{totalGb:N1} GB</td>";
                    s += $"<td>{job.OldestRestorePoint:yyyy-MM-dd}</td>";
                    s += $"<td>{job.NewestRestorePoint:yyyy-MM-dd}</td>";
                    s += "</tr>";

                    s += "<tr class=\"detail-row\"><td colspan=\"9\">";
                    s += job.Category == "Orphaned"
                        ? "<p class=\"label\">No live job - this name/type came from the backup's own retained metadata, not a current VBR job.</p>"
                        : "<p class=\"label\">Still a live job - these points belong to an object no longer part of its currently-active membership.</p>";
                    s += "<table><thead><tr><th>Object</th><th>ObjectId</th><th>Fulls</th><th>Incrementals</th><th>Avg Full Size</th><th>Avg Incremental Size</th><th>Total Size</th><th>Oldest</th><th>Newest</th></tr></thead><tbody>";
                    foreach (var obj in job.Objects.OrderBy(o => o.OldestRestorePoint))
                    {
                        string objName = scrub ? "Object (scrubbed)" : obj.ObjectName;
                        s += "<tr>";
                        s += $"<td>{objName}</td>";
                        s += $"<td>{obj.ObjectId}</td>";
                        s += $"<td>{obj.FullCount}</td>";
                        s += $"<td>{obj.IncrementalCount}</td>";
                        s += $"<td>{obj.AvgFullSizeBytes / 1073741824d:N1} GB</td>";
                        s += $"<td>{obj.AvgIncrementalSizeBytes / 1073741824d:N1} GB</td>";
                        s += $"<td>{obj.TotalSizeBytes / 1073741824d:N1} GB</td>";
                        s += $"<td>{obj.OldestRestorePoint:yyyy-MM-dd}</td>";
                        s += $"<td>{obj.NewestRestorePoint:yyyy-MM-dd}</td>";
                        s += "</tr>";
                    }
                    s += "</tbody></table></td></tr>";
                }

                s += "</tbody></table></div>";
            }

            summary = (sweepEvaluated ? "" : "Orphaned Backups: not evaluated for this environment. ")
                + $"{grandTotalCount} orphaned/superseded backups found, ~{grandTotalBytes / 1073741824d:N0} GB potentially reclaimable.";
            return s;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj --filter "Category=OrphanedSupersededBackups"`
Expected: PASS (all `OrphanedSupersededBackupAggregatorTests` from Task 6 plus the 7 new `COrphanedSupersededBackupsTableTests` above — same trait, same filter).

- [ ] **Step 5: Add `AddOrphanedSupersededBackupsTable` to `CHtmlTables.cs`**

Add this new public method (placed near `AddProtectedWorkLoadsTable`, following the same `SectionStartWithButtonNoTable`/`SectionEndNoTable` shape):

```csharp
public string AddOrphanedSupersededBackupsTable(bool scrub)
{
    string s = this.form.SectionStartWithButtonNoTable("orphanedsupersededbackups", "Orphaned & Superseded Backups", "Show/Hide");
    string summary;
    try
    {
        var table = new VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.OrphanedSupersededBackups.COrphanedSupersededBackupsTable();
        var records = table.LoadRecords();
        bool sweepEvaluated = table.WasSweepEvaluated();
        s += table.Render(records, sweepEvaluated, scrub, out summary);

        // CGlobals.FullReportJson is initialized inline at declaration
        // (Common/CGlobals.cs:150) - never null, no ??= guard needed.
        CGlobals.FullReportJson.OrphanedSupersededBackups = records;
        CGlobals.FullReportJson.OrphanedBackupsSweepEvaluated = sweepEvaluated;
    }
    catch (Exception e)
    {
        this.log.Error("Failed to build Orphaned/Superseded Backups table: " + e.Message);
        summary = "Orphaned/Superseded Backups table failed to build.";
    }
    s += this.form.SectionEndNoTable(summary);
    return s;
}
```

- [ ] **Step 6: Wire the wrapper into `CHtmlBodyHelper.cs`**

Add a new private method, following the exact shape of `ProtectedWorkloadsTable()`:

```csharp
private void OrphanedSupersededBackupsTable()
{
    this.HTMLSTRING += this.tables.AddOrphanedSupersededBackupsTable(this.SCRUB);
}
```

Then in `RepositoryInfoSection()`, add the call as the **last statement in that method's body**, whatever that statement currently is (not confirmed during planning which one it is — possibly `RepoTable()`, possibly `AddRepositoryInfoFooter()`, possibly something else) — this places the new section immediately after repository info, matching its per-repository framing, regardless of what precedes it:

```csharp
this.OrphanedSupersededBackupsTable();
```

- [ ] **Step 7: Add the sidebar nav link in `CHtmlCompiler.cs`**

Change:

```csharp
            // Infrastructure
            nav += this.form.NavSection("Infrastructure",
                this.form.NavLink("vbrserver", VbrLocalizationHelper.NavBkpSrvLink) +
                this.form.NavLink("serversummary", "Infrastructure Types") +
                this.form.NavLink("managedServerInfo", VbrLocalizationHelper.NavSrvInfoLink) +
                this.form.NavLink("proxies", VbrLocalizationHelper.NavProxyInfoLink) +
                this.form.NavLink("repos", VbrLocalizationHelper.NavRepoInfoLink) +
                this.form.NavLink("sobr", VbrLocalizationHelper.NavSobrInfoLink));
```

to:

```csharp
            // Infrastructure
            nav += this.form.NavSection("Infrastructure",
                this.form.NavLink("vbrserver", VbrLocalizationHelper.NavBkpSrvLink) +
                this.form.NavLink("serversummary", "Infrastructure Types") +
                this.form.NavLink("managedServerInfo", VbrLocalizationHelper.NavSrvInfoLink) +
                this.form.NavLink("proxies", VbrLocalizationHelper.NavProxyInfoLink) +
                this.form.NavLink("repos", VbrLocalizationHelper.NavRepoInfoLink) +
                this.form.NavLink("sobr", VbrLocalizationHelper.NavSobrInfoLink) +
                this.form.NavLink("orphanedsupersededbackups", "Orphaned & Superseded Backups"));
```

(Placed under "Infrastructure" alongside "repos"/"sobr" since the new anchor lives in `RepositoryInfoSection()`, not `ConfigurationTablesSection()` — the nav section a link sits under must match where its anchor actually renders in `CHtmlBodyHelper.FormVbrFullReport()`'s call sequence.)

- [ ] **Step 8: Build**

Run: `dotnet build vHC/HC.sln --configuration Debug`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/OrphanedSupersededBackups/ vHC/VhcXTests/Functions/Reporting/Html/VBR/VbrTables/OrphanedSupersededBackups/ vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs
git commit -m "feat(reporting): add Orphaned & Superseded Backups HTML section, with tests (#192)"
```

---

## Task 9: Row-level expand/collapse (JS/CSS)

**Files:**
- Modify: `vHC/HC_Reporting/ReportScript.js`
- Modify: `vHC/HC_Reporting/css.css`

No per-row toggle exists anywhere in this codebase today — this is new code, following the *idiom* already used for bare-table toggling (inline `style.display` manipulation, clearing to the element's natural value rather than forcing `block`, per the existing "Legacy Collapsible Toggle" comment's own reasoning about `<table>` layout).

- [ ] **Step 1: Add the JS function**

In `ReportScript.js`, add after the existing `toggleSection`/`toggleAll` block:

```javascript
// ===== Row-Level Detail Toggle (Orphaned & Superseded Backups) =====
// Each job row is immediately followed by a sibling <tr class="detail-row">
// holding the per-object breakdown, hidden by default. Toggle it directly
// via inline style rather than a class, matching the existing Legacy
// Collapsible Toggle's approach for <table>-shaped content below a button.
function toggleDetailRow(rowElement) {
  var detailRow = rowElement.nextElementSibling;
  if (detailRow && detailRow.classList.contains('detail-row')) {
    detailRow.style.display = (detailRow.style.display === 'table-row') ? 'none' : 'table-row';
  }
}
```

- [ ] **Step 2: Add the CSS rules**

In `css.css`, add near the `.section-card`/`.section-body` block:

```css
/* ===== Orphaned & Superseded Backups: row-level detail ===== */
.detail-toggle {
  cursor: pointer;
}

.detail-row {
  display: none;
}

.badge-orphaned {
  background: var(--amber-light, #5a3a1a);
  color: var(--amber, #ffb74d);
}

.badge-superseded {
  background: var(--blue-light, #1a3a5a);
  color: var(--blue, #64b5f6);
}
```

Then extend the existing `@media print` block — change:

```css
@media print {
  .sidebar { display: none; }
  .main { margin-left: 0; padding: 16px; }
  .section-card.open .section-body, .section-body { display: block !important; }
  .content { display: block !important; }
  .toolbar { display: none; }
  #myBtn { display: none !important; }
  footer { margin-left: 0; }
}
```

to:

```css
@media print {
  .sidebar { display: none; }
  .main { margin-left: 0; padding: 16px; }
  .section-card.open .section-body, .section-body { display: block !important; }
  .content { display: block !important; }
  .detail-row { display: table-row !important; }
  .toolbar { display: none; }
  #myBtn { display: none !important; }
  footer { margin-left: 0; }
}
```

so PDF/PPTX exports render every object-level detail row expanded, matching how `.section-body` is already force-opened for print.

- [ ] **Step 3: Manual verification**

There's no automated test harness for the JS/CSS in this codebase (confirmed — no JS test runner configured). Verify manually: build the solution, run a report against a test/lab VBR server with at least one Orphaned or Superseded row, open the generated HTML in a browser, and confirm:
- Clicking a job row toggles its detail row open/closed.
- The badge colors render distinctly for Orphaned vs. Superseded.
- Printing to PDF (browser print preview) shows every detail row expanded.

- [ ] **Step 4: Commit**

```bash
git add vHC/HC_Reporting/ReportScript.js vHC/HC_Reporting/css.css
git commit -m "feat(reporting): add row-level detail toggle for Orphaned & Superseded Backups (#192)"
```

---

## Task 10: Full-suite verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full PowerShell test suite**

Run: `pwsh -Command "Invoke-Pester -Path 'vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig' -Output Detailed"`
Expected: PASS, 0 failures — including every pre-existing `Get-VhcJob.Tests.ps1` test (Tasks 1-2 touch shared code paths used by every job) and the manifest test (Task 4).

- [ ] **Step 2: Run the full .NET test suite**

Run: `dotnet test vHC/VhcXTests/VhcXTests.csproj`
Expected: PASS, 0 failures.

- [ ] **Step 3: Build both configurations**

Run: `dotnet build vHC/HC.sln --configuration Debug` then `dotnet build vHC/HC.sln --configuration Release`
Expected: 0 errors in both.

- [ ] **Step 4: Manual smoke test against a real/lab VBR server**

Per the design spec's Testing and Validation Plan, before this branch is raised as a PR:
- Confirm the accepted Category A gap (pure-safe-allowlist environments show "not evaluated") in a real all-safe-allowlist lab.
- Confirm the zero-overlap guard neutralizes a real VMware Cloud Director Backup job (if available) rather than flagging it.
- Confirm `IsTapeBackup` exclusion against real tape-copied restore points on at least two source platforms.
- Confirm the `(BackupId, ObjectId)` grain against a real multi-VM job on a per-VM-chains-disabled repository.
- Confirm the added per-job `GetObjectsInJob()` call's cost is negligible in a large, real all-safe-allowlist environment.

This step has no pass/fail command — it's the multi-lab validation the design spec calls for, owned by the repo maintainer, to be done before opening the PR.

- [ ] **Step 5: No commit for this task** — it's verification only, nothing to stage.
