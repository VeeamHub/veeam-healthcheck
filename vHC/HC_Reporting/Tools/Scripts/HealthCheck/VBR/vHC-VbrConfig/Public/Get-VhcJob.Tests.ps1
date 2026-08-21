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
                Name                = $this.JobName
                JobType             = 'EpAgentBackup'
                SheduleEnabledTime  = $null
                ScheduleOptions     = $null
                IsScheduleEnabled   = $true
                TypeToString        = 'Agent'
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
