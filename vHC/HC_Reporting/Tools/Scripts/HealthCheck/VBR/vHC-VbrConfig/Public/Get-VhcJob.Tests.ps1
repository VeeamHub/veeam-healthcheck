#Requires -Version 7.0
# Pester v5 tests for Get-VhcJob standalone-backup resilience (ISC-1 through ISC-5, ISC-10).
#
# Background:
#   v3.0.1.169 introduced standalone agent job collection via
#   Get-VBRBackup | Where IsAgentStandaloneJob | ForEach { $_.GetJob() }.
#   When even one backup's GetJob() throws ("Object reference not set" - an
#   orphaned standalone backup whose owning job was deleted), the entire
#   pipeline aborts. The current outer try/catch then logs ERROR and routes
#   to Add-VhciModuleError, which the manifest writer treats as a Jobs
#   collector failure - exit code 2, even though every collector ran.
#
#   Reference customer scenario: backup GUID 186c0756-5c8f-4e65-a855-1e0f91c175f2
#   triggered this exact failure mode against an otherwise-healthy 20/20 run.
#
# Fix:
#   Per-item try/catch inside the ForEach-Object so a single bad GetJob() call
#   logs a WARNING with the orphaned backup Id and returns $null instead of
#   propagating. The existing Where-Object { $_ } filters the nulls out and
#   surviving siblings flow through unchanged.
#
# Convention follows Get-VhcBackupSessions.Tests.ps1 - stub Veeam cmdlets,
# dot-source Write-LogFile then the function under test, mock Write-LogFile
# per test to capture WARNING messages.

BeforeAll {
    # Stub Veeam cmdlets - none of these exist outside a real VBR install.
    #
    # Guard against more than mere existence: vHC-VbrConfig.Manifest.Tests.ps1
    # installs its OWN parameterless global stubs for several of these same
    # names (e.g. `function global:Get-VBRRestorePoint { }`) and never tears
    # them down. When the whole test directory runs and that file loads
    # first, a bare `Get-Command X` guard here sees that leftover stub,
    # concludes a stub already exists, and skips installing the correctly
    # SHAPED one below - so `Mock Get-VBRRestorePoint -MockWith { if ($null
    # -eq $Backup) ... }` has no -Backup parameter to bind against, $Backup
    # is always $null, and every scoped call gets the unscoped payload.
    # Excluding CommandType 'Cmdlet' only screens out a leftover function
    # stub - a real Veeam cmdlet (CommandType 'Cmdlet') still short-circuits
    # this and is left alone, same as before.
    if (-not (Get-Command Get-VBRJob -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRJob { param([string]$WarningAction) }
    }
    if (-not (Get-Command Get-VBRBackup -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRBackup { param([string]$WarningAction) }
    }
    if (-not (Get-Command Get-VBRConfigurationBackupJob -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRConfigurationBackupJob { }
    }
    if (-not (Get-Command Get-VBRRestorePoint -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRRestorePoint {
            [CmdletBinding()]
            param($Backup)
        }
    }
    if (-not (Get-Command Invoke-VhciJobSubCollectors -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Invoke-VhciJobSubCollectors { param($Jobs) }
    }
    if (-not (Get-Command Export-VhciCsv -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Export-VhciCsv { param([Parameter(ValueFromPipeline=$true)]$InputObject, [string]$FileName) process {} }
    }
    if (-not (Get-Command Add-VhciModuleError -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }
    }

    # Fake-backup factory:
    #   $ThrowOnGetJob = orphaned backup (GetJob throws). Otherwise GetJob
    #   returns a healthy fake CBackupJob with a Name and a Get-VBRJob-style
    #   shape (Info.IncludedSize and GetLastBackup) so it survives the main
    #   loop body in Get-VhcJob.
    function script:New-FakeStandaloneBackup {
        param(
            [string]$Name = 'StandaloneBackup',
            [guid]$Id = [guid]::NewGuid(),
            [switch]$ThrowOnGetJob,
            [string]$JobName = 'StandaloneAgentJob'
        )
        $backup = [PSCustomObject]@{
            Id                    = $Id
            Name                  = $Name
            IsAgentStandaloneJob  = $true
            ThrowOnGetJob         = [bool]$ThrowOnGetJob
            JobName               = $JobName
        }
        $backup | Add-Member -MemberType ScriptMethod -Name GetJob -Value {
            if ($this.ThrowOnGetJob) {
                throw "Object reference not set to an instance of an object."
            }
            # Return a minimal job that survives the main loop projection.
            # The loop accesses $Job.Name, $Job.GetLastBackup(), $Job.Info.*,
            # $Job.Options.*, $Job.BackupStorageOptions.*, $Job.VssOptions.*,
            # $Job.IsScheduleEnabled, $Job.TypeToString. We stub the minimum.
            $job = [PSCustomObject]@{
                Id                  = [guid]::NewGuid()
                Name                = $this.JobName
                JobType             = 'EpAgentBackup'
                SheduleEnabledTime  = $null
                ScheduleOptions     = $null
                IsScheduleEnabled   = $true
                TypeToString        = 'Windows Agent Backup'
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
            $job | Add-Member -MemberType ScriptMethod -Name GetLastBackup -Value { return $null }
            return $job
        }
        return $backup
    }

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
            [switch]$ThrowOnGetLastBackup,
            [double]$IncludedSize = 0
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
                IncludedSize       = $IncludedSize
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

    # Dot-source Write-LogFile then the function under test.
    $moduleRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $moduleRoot 'Public/Write-LogFile.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
}

# ---------------------------------------------------------------------------
# ISC-1: Get-VhcJob does not throw when a standalone backup's GetJob() throws
# ---------------------------------------------------------------------------
Describe 'ISC-1: Orphaned standalone backup does not throw' {

    BeforeEach {
        Mock Write-LogFile             -MockWith { }
        Mock Get-VBRJob                -MockWith { @() }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors -MockWith { }
        Mock Export-VhciCsv            -MockWith { }
        Mock Add-VhciModuleError       -MockWith { }
        Mock Get-VBRBackup             -MockWith {
            @( (script:New-FakeStandaloneBackup -Name 'Orphan' -ThrowOnGetJob) )
        }
    }

    It 'does not throw when the only standalone backup is orphaned' {
        { Get-VhcJob } | Should -Not -Throw
    }
}

# ---------------------------------------------------------------------------
# ISC-2: Good siblings survive when one standalone backup is orphaned
# ---------------------------------------------------------------------------
Describe 'ISC-2: Healthy siblings survive an orphan in the same list' {

    BeforeEach {
        $script:capturedSubCollectorJobs = $null
        Mock Write-LogFile             -MockWith { }
        Mock Get-VBRJob                -MockWith { @() }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors -MockWith {
            $script:capturedSubCollectorJobs = $Jobs
        }
        Mock Export-VhciCsv            -MockWith { }
        Mock Add-VhciModuleError       -MockWith { }
        Mock Get-VBRBackup             -MockWith {
            @(
                (script:New-FakeStandaloneBackup -Name 'Good1' -JobName 'GoodAgent1'),
                (script:New-FakeStandaloneBackup -Name 'Orphan' -ThrowOnGetJob),
                (script:New-FakeStandaloneBackup -Name 'Good2' -JobName 'GoodAgent2')
            )
        }
    }

    It 'does not throw' {
        { Get-VhcJob } | Should -Not -Throw
    }

    It 'passes 2 surviving standalone jobs to Invoke-VhciJobSubCollectors' {
        Get-VhcJob
        $surviving = @($script:capturedSubCollectorJobs | Where-Object { $_ })
        $surviving.Count | Should -Be 2
    }

    It 'surviving jobs are the healthy ones (GoodAgent1, GoodAgent2)' {
        Get-VhcJob
        $names = @($script:capturedSubCollectorJobs | Where-Object { $_ } | ForEach-Object { $_.Name })
        $names | Should -Contain 'GoodAgent1'
        $names | Should -Contain 'GoodAgent2'
        $names | Should -Not -Contain 'OrphanedAgent'
    }
}

# ---------------------------------------------------------------------------
# ISC-3: Orphan skip emits WARNING log containing the backup Id
# ---------------------------------------------------------------------------
Describe 'ISC-3: Orphan skip emits WARNING with backup Id' {

    BeforeEach {
        $script:warnings = [System.Collections.Generic.List[string]]::new()
        $script:orphanId = [guid]'186c0756-5c8f-4e65-a855-1e0f91c175f2'
        Mock Write-LogFile -MockWith {
            if ($LogLevel -eq 'WARNING') { $script:warnings.Add($Message) }
        }
        Mock Get-VBRJob                -MockWith { @() }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors -MockWith { }
        Mock Export-VhciCsv            -MockWith { }
        Mock Add-VhciModuleError       -MockWith { }
        Mock Get-VBRBackup             -MockWith {
            @( (script:New-FakeStandaloneBackup -Name 'OrphanBackup' -Id $script:orphanId -ThrowOnGetJob) )
        }
    }

    It 'logs a WARNING that contains the orphaned backup Id' {
        Get-VhcJob
        $matching = @($script:warnings | Where-Object { $_ -match [regex]::Escape($script:orphanId.ToString()) })
        $matching.Count | Should -BeGreaterThan 0
    }

    It 'logs a WARNING that names the orphaned backup' {
        Get-VhcJob
        ($script:warnings | Where-Object { $_ -match 'OrphanBackup' }).Count | Should -BeGreaterThan 0
    }
}

# ---------------------------------------------------------------------------
# ISC-4: Add-VhciModuleError is NOT called for per-item GetJob() failures
#        (only catastrophic Get-VBRBackup failures should escalate)
# ---------------------------------------------------------------------------
Describe 'ISC-4: Per-item GetJob() failure does not register module error' {

    BeforeEach {
        Mock Write-LogFile             -MockWith { }
        Mock Get-VBRJob                -MockWith { @() }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors -MockWith { }
        Mock Export-VhciCsv            -MockWith { }
        Mock Add-VhciModuleError       -MockWith { }
        Mock Get-VBRBackup             -MockWith {
            @( (script:New-FakeStandaloneBackup -Name 'Orphan' -ThrowOnGetJob) )
        }
    }

    It 'does NOT call Add-VhciModuleError when a single GetJob() throws' {
        Get-VhcJob
        Should -Invoke Add-VhciModuleError -Times 0 -Exactly
    }
}

# ---------------------------------------------------------------------------
# ISC-5: All-orphan case proceeds normally with empty standalone job list
# ---------------------------------------------------------------------------
Describe 'ISC-5: All-orphan case proceeds with empty standalone list' {

    BeforeEach {
        $script:capturedSubCollectorJobs = $null
        Mock Write-LogFile             -MockWith { }
        Mock Get-VBRJob                -MockWith { @() }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors -MockWith {
            $script:capturedSubCollectorJobs = $Jobs
        }
        Mock Export-VhciCsv            -MockWith { }
        Mock Add-VhciModuleError       -MockWith { }
        Mock Get-VBRBackup             -MockWith {
            @(
                (script:New-FakeStandaloneBackup -Name 'Orphan1' -ThrowOnGetJob),
                (script:New-FakeStandaloneBackup -Name 'Orphan2' -ThrowOnGetJob),
                (script:New-FakeStandaloneBackup -Name 'Orphan3' -ThrowOnGetJob)
            )
        }
    }

    It 'does not throw when every standalone backup is orphaned' {
        { Get-VhcJob } | Should -Not -Throw
    }

    It 'passes zero surviving standalone jobs to Invoke-VhciJobSubCollectors' {
        Get-VhcJob
        $surviving = @($script:capturedSubCollectorJobs | Where-Object { $_ })
        $surviving.Count | Should -Be 0
    }

    It 'does NOT call Add-VhciModuleError when every standalone is orphaned' {
        Get-VhcJob
        Should -Invoke Add-VhciModuleError -Times 0 -Exactly
    }
}

# ---------------------------------------------------------------------------
# ISC-10: Customer regression scenario - one orphan among siblings
#         (backup GUID 186c0756-5c8f-4e65-a855-1e0f91c175f2)
# ---------------------------------------------------------------------------
Describe 'ISC-10: Customer regression - GUID 186c0756 scenario succeeds' {

    BeforeEach {
        $script:orphanGuid = [guid]'186c0756-5c8f-4e65-a855-1e0f91c175f2'
        $script:capturedSubCollectorJobs = $null
        $script:warnings = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith {
            if ($LogLevel -eq 'WARNING') { $script:warnings.Add($Message) }
        }
        Mock Get-VBRJob                -MockWith { @() }
        Mock Get-VBRConfigurationBackupJob -MockWith { $null }
        Mock Invoke-VhciJobSubCollectors -MockWith {
            $script:capturedSubCollectorJobs = $Jobs
        }
        Mock Export-VhciCsv            -MockWith { }
        Mock Add-VhciModuleError       -MockWith { }
        Mock Get-VBRBackup             -MockWith {
            @(
                (script:New-FakeStandaloneBackup -Name 'CustomerHealthy' -JobName 'HealthyAgent'),
                (script:New-FakeStandaloneBackup -Name 'CustomerOrphan'  -Id $script:orphanGuid -ThrowOnGetJob)
            )
        }
    }

    It 'completes without throwing (Jobs collector reports [OK])' {
        { Get-VhcJob } | Should -Not -Throw
    }

    It 'does NOT call Add-VhciModuleError (no Jobs failure registered)' {
        Get-VhcJob
        Should -Invoke Add-VhciModuleError -Times 0 -Exactly
    }

    It 'logs the exact orphaned backup GUID in a WARNING' {
        Get-VhcJob
        ($script:warnings | Where-Object { $_ -match '186c0756-5c8f-4e65-a855-1e0f91c175f2' }).Count |
            Should -BeGreaterThan 0
    }

    It 'preserves the healthy sibling in the collected job list' {
        Get-VhcJob
        $names = @($script:capturedSubCollectorJobs | Where-Object { $_ } | ForEach-Object { $_.Name })
        $names | Should -Contain 'HealthyAgent'
    }
}

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
}

# ---------------------------------------------------------------------------
# Sweep resilience: a Write-LogFile failure inside the sweep must not
# propagate to the outer catch and disable the sweep for every other job.
# ---------------------------------------------------------------------------
Describe 'Sweep resilience: a logging failure does not disable the sweep' {

    BeforeEach {
        $script:CapturedJobRows = @()
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

    It 'a throw from the sweep-summary log line still applies a real tier-1 match to other jobs' {
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($RealJob) }
        Mock Write-LogFile -MockWith {
            if ($Message -match 'Restore point matching:') { throw 'Simulated logging failure' }
        }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'70707070-7070-7070-7070-707070707070') -ApproxSize 20GB -BackupSize 9GB -SourceJob $RealJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 9
    }

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

    It 'a throw from the sweep-failure ERROR log line does not abort the whole function' {
        # This is the sweep's OUTERMOST catch - unlike the other two logging
        # failures above (which just fall through to here), a throw from
        # THIS Write-LogFile call has nowhere left to go: before the fix, it
        # unwinds Get-VhcJob entirely, so _Jobs.csv is never exported for
        # ANY job, not even via the per-job fallback method.
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })
        Mock Get-VBRJob -MockWith { @($RealJob) }
        Mock Write-LogFile -MockWith {
            if ($Message -match 'Restore point sweep failed') { throw 'Simulated logging failure' }
        }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                throw 'Simulated sweep failure'
            } else {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'73737373-7373-7373-7373-737373737373') -ApproxSize 9GB -BackupSize 6GB) )
            }
        }
        { Get-VhcJob } | Should -Not -Throw
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 6
    }
}

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
        # Distinct object, same Id as $MorpheusJob - proves matching happens
        # by Id, not by object reference.
        $MorpheusJobTwin = script:New-FakeJob -Id $MorpheusJob.Id -Name 'DifferentObjectSameId' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'22222222-2222-2222-2222-222222222222') -ApproxSize 190GB -BackupSize 72.99GB -SourceJob $MorpheusJobTwin) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'MorpheusJob' }
        $Row.OnDiskGB     | Should -Be 72.99
        $Row.OriginalSize | Should -Be 190GB
    }

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

    It 'skips a restore point whose resolved job has no Id, without aborting the sweep' {
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }

        $NoIdJob = [PSCustomObject]@{ Name = 'ObjectWithNoId' }
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($RealJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'NoIdPoint' -ObjectId ([guid]'13131313-1313-1313-1313-131313131313') -ApproxSize 10GB -BackupSize 3GB -SourceJob $NoIdJob),
                    (script:New-FakeRestorePoint -Name 'RealPoint' -ObjectId ([guid]'14141414-1414-1414-1414-141414141414') -ApproxSize 20GB -BackupSize 9GB -SourceJob $RealJob)
                )
            } else { @() }
        }
        Get-VhcJob
        @($script:LogMessages | Where-Object { $_ -match 'matched via tier 1' }).Count | Should -Be 1
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 9
    }

    It 'counts a GetSourceJob() throw in the tier-1 failure counter, not silently' {
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($RealJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'15151515-1515-1515-1515-151515151515') -ApproxSize 5GB -BackupSize 2GB -ThrowOnGetSourceJob) )
            } else { @() }
        }
        Get-VhcJob
        $SweepLine = $script:LogMessages | Where-Object { $_ -match 'matched via tier 1' } | Select-Object -Last 1
        $SweepLine | Should -Match '\b1 tier-1 lookup failures\b'
    }

    It 'a $Jobs element with no Id and no Info reaching the main consumption loop does not throw a null-reference through the per-job catch' {
        # Distinct from the $JobIdByName/$KnownJobIds null-Id guards above -
        # those only protect the SWEEP's own pre-processing. This fixture
        # also flows into the main per-job consumption loop's
        # `if ($NeedsSweep) { $JobIdKey = $Job.Id.ToString() }` at the point
        # the loop looks up this job's own bucket - unlike every other .Id
        # dereference in this file, that one had no null guard. Before the
        # fix, $Job.Id.ToString() throws "cannot call a method on a
        # null-valued expression", caught by the per-job try/catch, which
        # logs a misleading "Could not calculate restore point sizes" WARNING
        # for a job that isn't a calculation failure at all - it simply has
        # no Id. After the fix, the lookup is skipped gracefully and no such
        # WARNING is logged for this job.
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }

        $NoIdJob = [PSCustomObject]@{ Name = 'NoIdMainLoopJob'; TypeToString = 'HPE Morpheus VME Backup' }
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($NoIdJob, $RealJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'16161616-1616-1616-1616-161616161616') -ApproxSize 20GB -BackupSize 9GB -SourceJob $RealJob) )
            } else { @() }
        }
        { Get-VhcJob } | Should -Not -Throw
        @($script:LogMessages | Where-Object { $_ -match 'Could not calculate restore point sizes' }).Count | Should -Be 0
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 9
    }
}

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

    It 'does not abort the sweep when $Jobs contains a null placeholder ahead of a real non-allowlisted job' {
        # Reproduces: Get-VBRJob throws, so the outer catch leaves $Jobs =
        # $null; $Jobs = @($Jobs) + $standaloneJobs then prepends a $null
        # placeholder ahead of the surviving standalone job. That null alone
        # wouldn't flip $NeedsSweep (Select-Object -First 1 silently drops a
        # lone leading $null) - it's the real non-allowlisted standalone job
        # behind it that does. The $KnownJobIds build must skip the null
        # without throwing, or the sweep's outer catch aborts the whole
        # sweep and every job in the run reports OnDiskGB = 0.
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }

        Mock Get-VBRJob -MockWith { throw 'Get-VBRJob failed' }

        $MacAgentJob = script:New-FakeJob -Name 'MacAgentJob' -TypeToString 'Mac Agent Backup'
        $StandaloneBackup = [PSCustomObject]@{ IsAgentStandaloneJob = $true }
        $StandaloneBackup | Add-Member -MemberType ScriptMethod -Name GetJob -Value { $MacAgentJob }.GetNewClosure()
        Mock Get-VBRBackup -MockWith { @($StandaloneBackup) }

        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'77777777-7777-7777-7777-777777777777') -ApproxSize 12GB -BackupSize 6GB -SourceJob $MacAgentJob) )
            } else { @() }
        }

        Get-VhcJob
        @($script:LogMessages | Where-Object { $_ -match 'matched via tier 1' }).Count | Should -Be 1
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'MacAgentJob' }
        $Row.OnDiskGB | Should -Be 6
    }

    It 'a literal $null element in $Jobs never reaches Export-VhciCsv as a row' {
        # Deliberately does NOT use the `-and $InputObject` capture pattern
        # the other tests in this file use for Export-VhciCsv - that filter
        # silently drops a null $InputObject, which would hide exactly the
        # behavior this test exists to pin: without the
        # `if ($null -eq $Job) { continue }` guard, the null placeholder
        # element still reaches `$Job | Select-Object -Property ...`
        # (which emits nothing for a null pipeline input) and the
        # unchanged/null $JobDetails from that iteration still gets
        # $AllJobs.Add()'ed, so `$AllJobs | Export-VhciCsv -FileName
        # '_Jobs.csv'` invokes the real cmdlet once per element - including
        # once with $InputObject = $null for the placeholder.
        $script:JobsCsvCallCount = 0
        $script:NullRowsSeen     = 0
        Mock Write-LogFile -MockWith { }
        Mock Export-VhciCsv -MockWith {
            if ($FileName -eq '_Jobs.csv') {
                $script:JobsCsvCallCount++
                if ($null -eq $InputObject) { $script:NullRowsSeen++ }
            }
        }
        Mock Get-VBRJob -MockWith { throw 'Get-VBRJob failed' }

        $MacAgentJob = script:New-FakeJob -Name 'MacAgentJob' -TypeToString 'Mac Agent Backup'
        $StandaloneBackup = [PSCustomObject]@{ IsAgentStandaloneJob = $true }
        $StandaloneBackup | Add-Member -MemberType ScriptMethod -Name GetJob -Value { $MacAgentJob }.GetNewClosure()
        Mock Get-VBRBackup -MockWith { @($StandaloneBackup) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'78787878-7878-7878-7878-787878787878') -ApproxSize 12GB -BackupSize 6GB -SourceJob $MacAgentJob) )
            } else { @() }
        }

        Get-VhcJob
        $script:JobsCsvCallCount | Should -Be 1
        $script:NullRowsSeen     | Should -Be 0
    }
}

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
                @( (script:New-FakeRestorePoint -Type 'Snapshot' -ObjectId ([guid]'99999999-9999-9999-9999-999999999999') -ApproxSize 100GB -BackupSize 50GB -SourceJob $VMwareJob -BackupParentOrThisName 'VMware - Domain Controller') )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMware - Domain Controller' }
        $Row.OnDiskGB | Should -Be 0
    }

    It 'does not abort the sweep when $Jobs contains a job with no Id (the $JobIdByName build)' {
        # Regression: $JobIdByName's build loop originally guarded only
        # ($null -ne $j), not ($null -ne $j.Id) - unlike $KnownJobIds right
        # below it. $j.Id.ToString() on a null Id throws, and the outer
        # try/catch aborts the ENTIRE sweep (not just this one lookup), so
        # every job - including a perfectly healthy, resolvable one - would
        # report OnDiskGB = 0.
        $NoIdJob = [PSCustomObject]@{ Name = 'NoIdJob'; TypeToString = 'HPE Morpheus VME Backup' }
        $RealJob = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($NoIdJob, $RealJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') -ApproxSize 20GB -BackupSize 9GB -SourceJob $RealJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 9
    }

    It 'sums two Tier-2-only restore points that both name-resolve to the same job' {
        # Regression guard for a naive "$Tier1MatchedJobIds is redundant vs.
        # $RestorePointsByJob" simplification: $RestorePointsByJob is
        # mutated WHILE Tier 2 iterates (a job's first Tier-2 match is added
        # to it mid-loop). Checking live hashtable keys instead of a
        # snapshot taken once after Tier 1 completes would make the second
        # of two Tier-2-only points on the same job look "already matched"
        # by the first, and get silently dropped instead of summed.
        $CloudJob = script:New-FakeJob -Name 'Linux-01' -TypeToString 'Azure IaaS Backup'
        Mock Get-VBRJob -MockWith { @($CloudJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @(
                    (script:New-FakeRestorePoint -Name 'chain1' -ObjectId ([guid]'80808080-8080-8080-8080-808080808081') -ApproxSize 10GB -BackupSize 4GB -ThrowOnGetSourceJob -BackupParentOrThisName 'Linux-01'),
                    (script:New-FakeRestorePoint -Name 'chain2' -ObjectId ([guid]'80808080-8080-8080-8080-808080808082') -ApproxSize 10GB -BackupSize 6GB -ThrowOnGetSourceJob -BackupParentOrThisName 'Linux-01')
                )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Linux-01' }
        $Row.OnDiskGB | Should -Be 10   # 4 + 6, both tier-2 matches summed, neither dropped
    }

    It 'does not abort the sweep when $Jobs contains a job with a populated Id but no Name (the $JobIdByName build)' {
        # Regression: $JobIdByName's build loop guards ($null -ne $j) and
        # ($null -ne $j.Id), but not ($j.Name) - Hashtable.ContainsKey($null)
        # throws ArgumentNullException, and the outer try/catch aborts the
        # ENTIRE sweep (not just this one lookup), so every job - including a
        # perfectly healthy, resolvable one - would report OnDiskGB = 0.
        $NoNameJob = [PSCustomObject]@{ Id = [guid]::NewGuid() }
        $RealJob   = script:New-FakeJob -Name 'RealJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($NoNameJob, $RealJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb') -ApproxSize 20GB -BackupSize 9GB -SourceJob $RealJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'RealJob' }
        $Row.OnDiskGB | Should -Be 9
    }
}

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

# ---------------------------------------------------------------------------
# Replica handling (ADR 0021): Replica jobs are sized via their own
# GetLastBackup() lookup by default, not tier 1/2. If that lookup fails
# outright and a tier-1/2 match already exists for the same Id, that match is
# preserved instead of being replaced with a zeroed-out result.
# ---------------------------------------------------------------------------
Describe 'Replica jobs are sized via their own lookup, tier 1/2 only as a fallback on failure' {

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
        $ReplicaJob  = script:New-FakeJob -Name 'VMware - Replicas' -TypeToString 'VMware Replication' -ThrowOnGetLastBackup -IncludedSize 12.5
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith { @() }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'VMware - Replicas' }
        $Row.OnDiskGB     | Should -Be 0
        $Row.OriginalSize | Should -Be 12.5
    }

    It 'does not abort the sweep when a Replica job has no Id' {
        # Same bug class as $KnownJobIds/$JobIdByName's null-Id guards
        # (f21f03b, d5aea23) - a non-null $Job with no populated Id would
        # otherwise throw on $Job.Id.ToString(). The Replica loop runs AFTER
        # tier 1/2 matching, so the crash doesn't erase MorpheusJob's tier-1
        # match directly - it aborts the sweep via the outer catch instead,
        # which (per this task's $NeedsSweep reset) routes MorpheusJob to the
        # old per-job method instead of its already-correct tier-1 bucket,
        # losing the match anyway.
        $NoIdReplica = [PSCustomObject]@{ Name = 'NoIdReplica'; TypeToString = 'VMware Replication' }
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($NoIdReplica, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'40404040-4040-4040-4040-404040404040') -ApproxSize 20GB -BackupSize 11GB -SourceJob $MorpheusJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'MorpheusJob' }
        $Row.OnDiskGB | Should -Be 11
    }

    It 'a Replica job with a tier-1 match keeps it when GetLastBackup() throws' {
        # Distinct from 'does not abort the sweep when a Replica job has no
        # Id' above and from the WARNING test below - this covers the case
        # where the Replica loop's OWN lookup fails outright (GetLastBackup()
        # throws) AFTER a genuine tier-1 match was already recorded for this
        # same job Id. Before the fix, the loop unconditionally overwrote
        # $RestorePointsByJob[$JobIdKey] with a fresh empty ArrayList
        # regardless of whether the replica-specific lookup produced
        # anything, silently discarding the real tier-1 data. After the fix,
        # a failed lookup preserves the prior match instead of zeroing it.
        $ReplicaJob  = script:New-FakeJob -Name 'Hyper-V - Replicas' -TypeToString 'Hyper-V Replication' -ThrowOnGetLastBackup
        # A second, non-Replica, non-allowlisted job forces $NeedsSweep to
        # $true - a solo Replica job is excluded from $NonReplicaJobs
        # entirely, so the sweep (and thus the Replica loop) would never run.
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                # Non-Snapshot point that tier 1 resolves straight back to
                # $ReplicaJob itself via GetSourceJob(), giving it a genuine
                # tier-1 match before the Replica loop's own (failing) lookup
                # runs.
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'60606060-6060-6060-6060-606060606060') -ApproxSize 8GB -BackupSize 5GB -SourceJob $ReplicaJob) )
            } else { @() }
        }
        Get-VhcJob
        $Row = $script:CapturedJobRows | Where-Object { $_.Name -eq 'Hyper-V - Replicas' }
        $Row.OnDiskGB     | Should -Be 5
        $Row.OriginalSize | Should -Be 8GB
    }

    It 'logs a WARNING when a Replica job already carries a tier-1/2-matched restore point before the overwrite' {
        # $NonReplicaJobs only gates whether the sweep runs at all - it does
        # NOT exclude Replica jobs from tier 1/2 matching (only the
        # restore point's own Type -eq 'Snapshot' check does that). So a
        # non-Snapshot restore point whose GetSourceJob() resolves back to
        # a Replication-type job can land in $RestorePointsByJob before this
        # loop unconditionally overwrites it - previously silent, now logged.
        $script:LogMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:LogMessages.Add($Message) }

        $ReplicaJob  = script:New-FakeJob -Name 'Hyper-V - Replicas' -TypeToString 'Hyper-V Replication' -LastBackup ([PSCustomObject]@{ Id = [guid]::NewGuid() })
        $MorpheusJob = script:New-FakeJob -Name 'MorpheusJob' -TypeToString 'HPE Morpheus VME Backup'
        Mock Get-VBRJob -MockWith { @($ReplicaJob, $MorpheusJob) }
        Mock Get-VBRRestorePoint -MockWith {
            if ($null -eq $Backup) {
                # Non-Snapshot restore point that tier 1 resolves straight
                # back to the Replica job itself via GetSourceJob().
                @( (script:New-FakeRestorePoint -ObjectId ([guid]'50505050-5050-5050-5050-505050505050') -ApproxSize 5GB -BackupSize 3GB -SourceJob $ReplicaJob) )
            } else { @() }
        }
        Get-VhcJob
        @($script:LogMessages | Where-Object { $_ -match "Replica job 'Hyper-V - Replicas' had 1 tier-1/2-matched restore point" }).Count | Should -Be 1
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
