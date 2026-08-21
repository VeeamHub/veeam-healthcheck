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
    if (-not (Get-Command Get-VBRJob -ErrorAction SilentlyContinue)) {
        function global:Get-VBRJob { param([string]$WarningAction) }
    }
    if (-not (Get-Command Get-VBRBackup -ErrorAction SilentlyContinue)) {
        function global:Get-VBRBackup { param([string]$WarningAction) }
    }
    if (-not (Get-Command Get-VBRConfigurationBackupJob -ErrorAction SilentlyContinue)) {
        function global:Get-VBRConfigurationBackupJob { }
    }
    if (-not (Get-Command Get-VBRRestorePoint -ErrorAction SilentlyContinue)) {
        function global:Get-VBRRestorePoint {
            [CmdletBinding()]
            param($Backup)
        }
    }
    if (-not (Get-Command Invoke-VhciJobSubCollectors -ErrorAction SilentlyContinue)) {
        function global:Invoke-VhciJobSubCollectors { param($Jobs) }
    }
    if (-not (Get-Command Export-VhciCsv -ErrorAction SilentlyContinue)) {
        function global:Export-VhciCsv { param([Parameter(ValueFromPipeline=$true)]$InputObject, [string]$FileName) process {} }
    }
    if (-not (Get-Command Add-VhciModuleError -ErrorAction SilentlyContinue)) {
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
                @( (script:New-FakeRestorePoint -Type 'Snapshot' -ObjectId ([guid]'99999999-9999-9999-9999-999999999999') -ApproxSize 100GB -BackupSize 50GB -BackupParentOrThisName 'VMware - Domain Controller') )
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
}
