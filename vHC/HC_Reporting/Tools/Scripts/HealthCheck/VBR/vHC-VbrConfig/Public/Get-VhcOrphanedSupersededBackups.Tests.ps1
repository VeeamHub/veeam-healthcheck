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
            [switch]$IsTapeBackup,
            $Id = $null
        )
        $b = [PSCustomObject]@{
            Id            = $Id
            Name          = $Name
            TypeToString  = $TypeToString
            RepositoryId  = $RepositoryId
            IsTapeBackup  = [bool]$IsTapeBackup
        }
        $b | Add-Member -MemberType ScriptMethod -Name GetParentOrThis -Value { return $this }
        return $b
    }

    # BackupSizeValue/GetStorage mirror Get-VhcJob.Tests.ps1's own
    # New-FakeRestorePoint fixture, modeling the real VBR SDK shape
    # ($RestorePoint.GetStorage().Stats.BackupSize is the actual on-disk
    # size the script reports). Deliberately kept independent from
    # ApproxSize (the source VM's original size) so a test asserting on
    # one can't accidentally pass because the fixture defaulted both to
    # the same value. -ThrowOnGetStorage mirrors that same fixture's
    # -ThrowOnGetSourceJob/-ThrowOnGetBackup switches, to prove a single
    # point's unresolvable storage metadata doesn't take down its sibling
    # ObjectId rows in the same candidate group.
    function script:New-FakeCandidateRestorePoint {
        param(
            [string]$Name = 'FakeObject',
            [string]$Type = 'Increment',
            [guid]$ObjectId = [guid]::NewGuid(),
            [guid]$BackupId = [guid]::NewGuid(),
            [datetime]$CreationTimeUtc = (Get-Date),
            [double]$ApproxSize = 1GB,
            [double]$BackupSize = 1GB,
            $BackupObject,
            [switch]$ThrowOnGetStorage
        )
        $ThrowOnGetStorageCapture = [bool]$ThrowOnGetStorage
        $rp = [PSCustomObject]@{
            Name            = $Name
            Type            = $Type
            ObjectId        = $ObjectId
            BackupId        = $BackupId
            CreationTimeUtc = $CreationTimeUtc
            ApproxSize      = $ApproxSize
            BackupSizeValue = $BackupSize
        }
        $rp | Add-Member -MemberType ScriptMethod -Name GetBackup -Value { return $BackupObject }.GetNewClosure()
        $rp | Add-Member -MemberType ScriptMethod -Name GetStorage -Value {
            if ($ThrowOnGetStorageCapture) { throw 'GetStorage failed' }
            [PSCustomObject]@{ Stats = [PSCustomObject]@{ BackupSize = $this.BackupSizeValue } }
        }.GetNewClosure()
        return $rp
    }

    # Runs the function over ONE candidate group and returns the exported
    # rows. -AgesInDays gives each restore point's age relative to now
    # (one point per entry; entries on distinct ObjectIds unless
    # -SameObject); $null means "no usable timestamp" ([datetime]::MinValue).
    function script:Invoke-CategoryProbe {
        param(
            [string]$Reason = 'Unresolved',
            [object[]]$AgesInDays,
            [hashtable]$ExtraParams = @{}
        )
        $Backup = script:New-FakeBackupObject -Name 'ProbeJob'
        $BackupId = [guid]::NewGuid()
        $Points = foreach ($Age in $AgesInDays) {
            $When = if ($null -eq $Age) { [datetime]::MinValue } else { [datetime]::UtcNow.AddDays(-[double]$Age) }
            script:New-FakeCandidateRestorePoint -Name 'probe-vm' -Type 'Full' -BackupId $BackupId -BackupObject $Backup -CreationTimeUtc $When
        }
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = $Reason
            CurrentJobId  = $null
            RestorePoints = @($Points)
        })
        Get-VhcOrphanedSupersededBackups @ExtraParams
    }

    # Runs one Unresolved candidate whose restore points carry exactly
    # the supplied CreationTimeUtc values, untouched - including $null
    # and strings, which the [datetime]-typed fixture parameter cannot
    # express. One point per entry, each on its own ObjectId.
    function script:Invoke-RawTimeProbe {
        param([object[]]$Times, [hashtable]$ExtraParams = @{})
        $Backup   = script:New-FakeBackupObject -Name 'RawProbe'
        $BackupId = [guid]::NewGuid()
        $Points = foreach ($Time in $Times) {
            $Point = script:New-FakeCandidateRestorePoint -Name 'raw-vm' -Type 'Full' -BackupId $BackupId -BackupObject $Backup
            $Point.CreationTimeUtc = $Time
            $Point
        }
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason = 'Unresolved'; CurrentJobId = $null; RestorePoints = @($Points)
        })
        Get-VhcOrphanedSupersededBackups @ExtraParams
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
        # $Input (the automatic pipeline-enumerator variable) does not
        # reliably surface piped objects inside a Pester -MockWith
        # scriptblock - empirically confirmed empty here even though the
        # production code (Get-VhcOrphanedSupersededBackups) demonstrably
        # builds the expected row count before piping to Export-VhciCsv.
        # Binding an explicit ValueFromPipeline parameter with a process
        # block - the same shape Export-VhciCsv itself uses - captures
        # each piped object correctly instead.
        # Lists, not $null + @(): `@($null) + $x` produces a 2-element
        # array (a leading $null, then $x) rather than a 1-element array,
        # so accumulating onto a $null-seeded variable would silently
        # inflate every captured count by one spurious $null entry.
        $script:CapturedRows = [System.Collections.Generic.List[object]]::new()
        $script:CapturedMeta = [System.Collections.Generic.List[object]]::new()

        Mock Export-VhciCsv -MockWith {
            param(
                [Parameter(ValueFromPipeline = $true)]
                $InputObject,
                [string]$FileName
            )
            process {
                if ($FileName -eq '_orphanedSupersededBackups.csv') {
                    $script:CapturedRows.Add($InputObject)
                } elseif ($FileName -eq '_orphanedSupersededBackupsMeta.csv') {
                    $script:CapturedMeta.Add($InputObject)
                }
            }
        }
    }

    It 'exports an Orphaned row for an Unresolved group, using the backup''s own retained name/type/repo' {
        $Backup = script:New-FakeBackupObject -Name 'Proxmox - Malware Lab' -TypeToString 'Proxmox Backup' -RepositoryId ([guid]'11111111-1111-1111-1111-111111111111')
        $Point  = script:New-FakeCandidateRestorePoint -Name 'pve-vm-201' -Type 'Full' -ApproxSize 10GB -BackupObject $Backup -CreationTimeUtc ([datetime]::UtcNow.AddDays(-60))
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

    Context 'recency guard (#221): an Unresolved chain still being written is not Orphaned' {

        It 'labels an Unresolved candidate Unresolved when its newest restore point is 1 day old' {
            script:Invoke-CategoryProbe -AgesInDays @(1)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'labels every row Unresolved when only one object of a multi-object candidate is recent' {
            # Distinct ObjectIds: two stale objects and one written yesterday.
            $Backup = script:New-FakeBackupObject -Name 'MultiObj'
            $BkId   = [guid]::NewGuid()
            $Points = @(
                (script:New-FakeCandidateRestorePoint -Name 'old-a'  -Type 'Full' -BackupId $BkId -BackupObject $Backup -CreationTimeUtc ([datetime]::UtcNow.AddDays(-90))),
                (script:New-FakeCandidateRestorePoint -Name 'old-b'  -Type 'Full' -BackupId $BkId -BackupObject $Backup -CreationTimeUtc ([datetime]::UtcNow.AddDays(-60))),
                (script:New-FakeCandidateRestorePoint -Name 'fresh'  -Type 'Full' -BackupId $BkId -BackupObject $Backup -CreationTimeUtc ([datetime]::UtcNow.AddDays(-1)))
            )
            $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
                SweepRan        = $true
                CandidateGroups = [System.Collections.Generic.List[object]]::new()
            }
            $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
                Reason = 'Unresolved'; CurrentJobId = $null; RestorePoints = $Points
            })

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows | Should -HaveCount 3
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Unresolved' }) | Should -HaveCount 0
        }

        It 'labels an Unresolved candidate Unresolved when no restore point has a usable timestamp' {
            script:Invoke-CategoryProbe -AgesInDays @($null)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'treats a future-dated restore point (clock skew) as recent' {
            script:Invoke-CategoryProbe -AgesInDays @(-3)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'still labels an Unresolved candidate Orphaned when its newest restore point is 60 days old' {
            script:Invoke-CategoryProbe -AgesInDays @(60, 70)

            $script:CapturedRows | Should -HaveCount 2
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Orphaned' }) | Should -HaveCount 0
        }

        It 'is Unresolved just inside the window and Orphaned just outside it' {
            script:Invoke-CategoryProbe -AgesInDays @((23 / 24)) -ExtraParams @{ ActiveChainWindowDays = 1 }
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'

            $script:CapturedRows.Clear()
            script:Invoke-CategoryProbe -AgesInDays @((25 / 24)) -ExtraParams @{ ActiveChainWindowDays = 1 }
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }

        It 'defaults the window to 35 days' {
            script:Invoke-CategoryProbe -AgesInDays @(34)
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'

            $script:CapturedRows.Clear()
            script:Invoke-CategoryProbe -AgesInDays @(36)
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }

        It 'ships the same 35-day window in VbrConfig.json Thresholds that Get-VBRConfig.ps1 passes in' {
            $Config = Get-Content -Raw (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'VbrConfig.json') | ConvertFrom-Json
            $Config.Thresholds.OrphanedActiveChainWindowDays | Should -Be 35
        }

        It 'keeps Superseded for <Reason> once its newest point is older than the window (R1 pins the in-window case)' -ForEach @(
            @{ Reason = 'StaleObject' }
            @{ Reason = 'Tier2Suppressed' }
        ) {
            script:Invoke-CategoryProbe -Reason $Reason -AgesInDays @(40)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Superseded'
        }
    }

    Context 'recency guard (#221): input domain of CreationTimeUtc' {

        It 'judges a recent restore point Unresolved for DateTimeKind <Kind>' -ForEach @(
            @{ Kind = 'Utc' }
            @{ Kind = 'Unspecified' }
        ) {
            $Recent = [datetime]::SpecifyKind([datetime]::UtcNow.AddDays(-1), [System.DateTimeKind]$Kind)
            script:Invoke-RawTimeProbe -Times @($Recent)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'judges an old restore point Orphaned for DateTimeKind <Kind>' -ForEach @(
            @{ Kind = 'Utc' }
            @{ Kind = 'Unspecified' }
        ) {
            $Old = [datetime]::SpecifyKind([datetime]::UtcNow.AddDays(-60), [System.DateTimeKind]$Kind)
            script:Invoke-RawTimeProbe -Times @($Old)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }

        It 'treats [datetime]::MaxValue as future-dated, hence active' {
            script:Invoke-RawTimeProbe -Times @([datetime]::MaxValue)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'does not let a [datetime]::MinValue placeholder next to a recent point mask the recent point' {
            script:Invoke-RawTimeProbe -Times @([datetime]::MinValue, [datetime]::UtcNow.AddDays(-1))

            $script:CapturedRows | Should -HaveCount 2
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Unresolved' }) | Should -HaveCount 0
        }

        It 'treats the candidate as Unresolved when one point has a [datetime]::MinValue placeholder next to a 30-day-old point' {
            script:Invoke-RawTimeProbe -Times @([datetime]::MinValue, [datetime]::UtcNow.AddDays(-60))

            $script:CapturedRows | Should -HaveCount 2
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Unresolved' }) | Should -HaveCount 0
        }

        It 'converts a DateTimeKind.Local value to UTC before comparing it to the window' {
            # Offset-dependent by construction: the raw Local value differs from
            # the real UTC instant by the machine offset, so the instant is placed
            # +/- half that offset either side of the window edge, on the side
            # where comparing the raw value would give the wrong answer.
            $Offset = [TimeZoneInfo]::Local.GetUtcOffset([datetime]::Now)
            if ($Offset -eq [TimeSpan]::Zero) {
                Set-ItResult -Skipped -Because 'machine UTC offset is zero, so Local and Utc values are indistinguishable'
                return
            }
            $Half = [TimeSpan]::FromTicks([math]::Abs($Offset.Ticks) / 2)
            if ($Offset -lt [TimeSpan]::Zero) {
                $Age = [TimeSpan]::FromDays(35) - $Half
                $Expected = 'Unresolved'
            } else {
                $Age = [TimeSpan]::FromDays(35) + $Half
                $Expected = 'Orphaned'
            }
            $Local = [datetime]::UtcNow.Subtract($Age).ToLocalTime()
            $Local.Kind | Should -Be ([System.DateTimeKind]::Local)

            script:Invoke-RawTimeProbe -Times @($Local)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be $Expected
        }

        It 'keeps every row Unresolved when a very old point precedes a recent one' {
            script:Invoke-RawTimeProbe -Times @([datetime]::UtcNow.AddDays(-400), [datetime]::UtcNow.AddDays(-2))

            $script:CapturedRows | Should -HaveCount 2
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Unresolved' }) | Should -HaveCount 0
        }

        It 'with -ActiveChainWindowDays 0 treats a point from an hour ago as Orphaned' {
            script:Invoke-RawTimeProbe -Times @([datetime]::UtcNow.AddHours(-1)) -ExtraParams @{ ActiveChainWindowDays = 0 }

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }

        It 'with -ActiveChainWindowDays 0 still treats a future-dated point as Unresolved' {
            script:Invoke-RawTimeProbe -Times @([datetime]::UtcNow.AddHours(1)) -ExtraParams @{ ActiveChainWindowDays = 0 }

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'with -ActiveChainWindowDays 0 and no usable timestamp stays Unresolved' {
            script:Invoke-RawTimeProbe -Times @([datetime]::MinValue) -ExtraParams @{ ActiveChainWindowDays = 0 }

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'rejects a negative -ActiveChainWindowDays' {
            { script:Invoke-RawTimeProbe -Times @([datetime]::UtcNow) -ExtraParams @{ ActiveChainWindowDays = -1 } } | Should -Throw
        }
    }

    Context 'recency guard (#221): undated points are exported, not dropped' {

        It 'exports an Unresolved row with blank Oldest/Newest when the only point has a <Label> timestamp' -ForEach @(
            @{ Label = 'null'; Value = $null }
            @{ Label = 'string'; Value = 'not a date' }
        ) {
            script:Invoke-RawTimeProbe -Times @($Value)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
            $script:CapturedRows[0].OldestRestorePoint | Should -BeNullOrEmpty
            $script:CapturedRows[0].NewestRestorePoint | Should -BeNullOrEmpty
        }

        It 'derives Oldest/Newest from the usable points only when a <Label> timestamp sits next to dated ones' -ForEach @(
            @{ Label = 'null'; Value = $null }
            @{ Label = 'string'; Value = 'not a date' }
        ) {
            $Old    = [datetime]::UtcNow.AddDays(-60)
            $Recent = [datetime]::UtcNow.AddDays(-1)
            $Backup = script:New-FakeBackupObject -Name 'MixedTimes'
            $Id     = [guid]::NewGuid()
            $ObjectId = [guid]::NewGuid()
            $Points = @($Old, $Value, $Recent) | ForEach-Object {
                $p = script:New-FakeCandidateRestorePoint -Name 'vm' -Type 'Full' -ObjectId $ObjectId -BackupId $Id -BackupObject $Backup
                $p.CreationTimeUtc = $_
                $p
            }
            $script:VhcOrphanedSupersededCache = [PSCustomObject]@{ SweepRan = $true; CandidateGroups = [System.Collections.Generic.List[object]]::new() }
            $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{ Reason = 'Unresolved'; CurrentJobId = $null; RestorePoints = @($Points) })

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
            $script:CapturedRows[0].OldestRestorePoint | Should -Be $Old.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
            $script:CapturedRows[0].NewestRestorePoint | Should -Be $Recent.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
        }

        It 'exports the same UTC instant it classified for a DateTimeKind.Local point' {
            $Local = [datetime]::UtcNow.AddDays(-60).ToLocalTime()
            $Local.Kind | Should -Be ([System.DateTimeKind]::Local)

            script:Invoke-RawTimeProbe -Times @($Local)

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].NewestRestorePoint | Should -Be $Local.ToUniversalTime().ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
            $script:CapturedRows[0].NewestRestorePoint | Should -Match 'Z$'
        }
    }

    Context 'recency guard (#221): recency is judged per parent backup' {

        BeforeAll {
            # One candidate per VM (per-VM chains), each with its own BackupId,
            # all hanging off -Parent. Ages are days before collection.
            function script:Invoke-ParentProbe {
                param([object[]]$Candidates)
                $script:VhcOrphanedSupersededCache = [PSCustomObject]@{ SweepRan = $true; CandidateGroups = [System.Collections.Generic.List[object]]::new() }
                foreach ($c in $Candidates) {
                    $point = script:New-FakeCandidateRestorePoint -Name $c.Name -Type 'Full' -BackupId ([guid]::NewGuid()) -BackupObject $c.Parent `
                        -CreationTimeUtc ([datetime]::UtcNow.AddDays(-[double]$c.Age))
                    $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{ Reason = 'Unresolved'; CurrentJobId = $null; RestorePoints = @($point) })
                }
                Get-VhcOrphanedSupersededBackups
            }
        }

        It 'keeps every sibling Unresolved when only some VMs of the same parent backup are recent' {
            $Parent = script:New-FakeBackupObject -Name 'LiveJob' -Id ([guid]::NewGuid())
            script:Invoke-ParentProbe -Candidates @(
                @{ Name = 'vm-a'; Parent = $Parent; Age = 1 }
                @{ Name = 'vm-b'; Parent = $Parent; Age = 2 }
                @{ Name = 'vm-c'; Parent = $Parent; Age = 60 }
            )

            $script:CapturedRows | Should -HaveCount 3
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Unresolved' }) | Should -HaveCount 0
        }

        It 'labels a parent backup Orphaned when all of its candidates are old' {
            $Parent = script:New-FakeBackupObject -Name 'DeadJob' -Id ([guid]::NewGuid())
            script:Invoke-ParentProbe -Candidates @(
                @{ Name = 'vm-a'; Parent = $Parent; Age = 60 }
                @{ Name = 'vm-b'; Parent = $Parent; Age = 70 }
            )

            $script:CapturedRows | Should -HaveCount 2
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Orphaned' }) | Should -HaveCount 0
        }

        It 'judges different parent backups independently' {
            $Live = script:New-FakeBackupObject -Name 'LiveJob' -Id ([guid]::NewGuid())
            $Dead = script:New-FakeBackupObject -Name 'DeadJob' -Id ([guid]::NewGuid())
            script:Invoke-ParentProbe -Candidates @(
                @{ Name = 'live-vm'; Parent = $Live; Age = 1 }
                @{ Name = 'dead-vm'; Parent = $Dead; Age = 60 }
            )

            ($script:CapturedRows | Where-Object { $_.ObjectName -eq 'live-vm' }).Category | Should -Be 'Unresolved'
            ($script:CapturedRows | Where-Object { $_.ObjectName -eq 'dead-vm' }).Category | Should -Be 'Orphaned'
        }

        It 'keeps a parent backup Unresolved when an undated point sits in a sibling candidate' {
            $Parent = script:New-FakeBackupObject -Name 'MixedJob' -Id ([guid]::NewGuid())
            $script:VhcOrphanedSupersededCache = [PSCustomObject]@{ SweepRan = $true; CandidateGroups = [System.Collections.Generic.List[object]]::new() }
            foreach ($When in @([datetime]::UtcNow.AddDays(-60), $null)) {
                $p = script:New-FakeCandidateRestorePoint -Name 'vm' -Type 'Full' -BackupId ([guid]::NewGuid()) -BackupObject $Parent
                $p.CreationTimeUtc = $When
                $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{ Reason = 'Unresolved'; CurrentJobId = $null; RestorePoints = @($p) })
            }

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows | Should -HaveCount 2
            @($script:CapturedRows | Where-Object { $_.Category -ne 'Unresolved' }) | Should -HaveCount 0
        }
    }

    Context 'R3 (#221 follow-up): parent-level liveness includes sibling chains that resolved to a live job' {
        # The #278 PR text promises "nothing under its PARENT BACKUP was
        # written within the window". Get-VhcJob hands the classifier every
        # group a tier placed on a job (Cache.ResolvedGroups) so a sibling
        # chain that resolved to a live job and was written today (the #268
        # shape: VM chain unresolved, agent/worker chain resolved) keeps an
        # old Unresolved sibling out of Orphaned. The fixtures share one
        # parent through GetParentOrThis(); that real VBR returns the same
        # parent Id for such siblings is plan scenario S4.

        BeforeEach {
            $script:R3Parent = script:New-FakeBackupObject -Name 'BackupCopy - Prod' -Id ([guid]::NewGuid())
            $Parent = $script:R3Parent
            $script:R3ChildVm = script:New-FakeBackupObject -Name 'BackupCopy - Prod' -Id ([guid]::NewGuid())
            $script:R3ChildVm | Add-Member -MemberType ScriptMethod -Name GetParentOrThis -Value { return $Parent }.GetNewClosure() -Force
            $script:R3ChildAgent = script:New-FakeBackupObject -Name 'BackupCopy - Prod' -Id ([guid]::NewGuid())
            $script:R3ChildAgent | Add-Member -MemberType ScriptMethod -Name GetParentOrThis -Value { return $Parent }.GetNewClosure() -Force
            $script:OldVm = script:New-FakeCandidateRestorePoint -Name 'old-vm' -Type 'Full' -BackupObject $script:R3ChildVm -CreationTimeUtc ([datetime]::UtcNow.AddDays(-90))
            $script:VhcOrphanedSupersededCache = [PSCustomObject]@{ SweepRan = $true; CandidateGroups = [System.Collections.Generic.List[object]]::new(); ResolvedGroups = [System.Collections.Generic.List[object]]::new() }
            $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{ Reason = 'Unresolved'; CurrentJobId = $null; RestorePoints = @($script:OldVm) })
        }

        It 'known bug: R3 - keeps an old Unresolved chain Unresolved when a resolved sibling chain under the same parent was written today' {
            $LiveAgent = script:New-FakeCandidateRestorePoint -Name 'live-agent' -Type 'Increment' -BackupObject $script:R3ChildAgent -CreationTimeUtc ([datetime]::UtcNow.AddHours(-2))
            $script:VhcOrphanedSupersededCache.ResolvedGroups.Add(@($LiveAgent))

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'R3 control: a resolved sibling chain older than the window does not keep the Unresolved chain out of Orphaned' {
            $OldAgent = script:New-FakeCandidateRestorePoint -Name 'old-agent' -Type 'Increment' -BackupObject $script:R3ChildAgent -CreationTimeUtc ([datetime]::UtcNow.AddDays(-60))
            $script:VhcOrphanedSupersededCache.ResolvedGroups.Add(@($OldAgent))

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }

        It 'R3 control: a recent resolved chain under a DIFFERENT parent does not affect the Unresolved chain' {
            $OtherParent = script:New-FakeBackupObject -Name 'Other' -Id ([guid]::NewGuid())
            $Elsewhere = script:New-FakeCandidateRestorePoint -Name 'elsewhere' -Type 'Increment' -BackupObject $OtherParent -CreationTimeUtc ([datetime]::UtcNow.AddHours(-2))
            $script:VhcOrphanedSupersededCache.ResolvedGroups.Add(@($Elsewhere))

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }

        It 'R3: a resolved sibling with an unusable timestamp keeps the Unresolved chain Unresolved (fail safe)' {
            $Undated = script:New-FakeCandidateRestorePoint -Name 'undated' -Type 'Increment' -BackupObject $script:R3ChildAgent -CreationTimeUtc ([datetime]::MinValue)
            $script:VhcOrphanedSupersededCache.ResolvedGroups.Add(@($Undated))

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'R3: a recent Tier2Suppressed candidate under the same parent also keeps the Unresolved chain Unresolved' {
            $Suppressed = script:New-FakeCandidateRestorePoint -Name 'suppressed' -Type 'Increment' -BackupObject $script:R3ChildAgent -CreationTimeUtc ([datetime]::UtcNow.AddHours(-2))
            $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{ Reason = 'Tier2Suppressed'; CurrentJobId = [guid]::NewGuid().ToString(); RestorePoints = @($Suppressed) })

            Get-VhcOrphanedSupersededBackups

            @($script:CapturedRows | Where-Object { $_.ObjectName -eq 'old-vm' }).Category | Should -Be 'Unresolved'
        }

        It 'R3: tolerates a cache object that has no ResolvedGroups property' {
            $script:VhcOrphanedSupersededCache.PSObject.Properties.Remove('ResolvedGroups')

            { Get-VhcOrphanedSupersededBackups } | Should -Not -Throw
            $script:CapturedRows[0].Category | Should -Be 'Orphaned'
        }
    }

    Context 'R1: Superseded has no liveness guard but is counted as potentially reclaimable' {
        # Hypothesis R1: only Unresolved goes through the #278 recency window
        # (Get-VhcOrphanedSupersededBackups.ps1 ~253-261); every other Reason
        # becomes Superseded unconditionally, and the C# table sums Superseded
        # into "potentially reclaimable" (COrphanedSupersededBackupsTable.cs).
        # A StaleObject group can be minutes old (VM removed right after its
        # last run) and a Tier2Suppressed chain can be one the job still writes.
        #
        # DECISION NAMED (product call for Ben / comnam90): these tests encode
        # the SAFEST reading, the one #278 applied to Unresolved. A Superseded
        # group whose newest point is inside the active-chain window is NOT
        # presented as reclaimable; the only existing non-reclaimable category
        # is 'Unresolved' ("still being written, not counted"). Other readings
        # (a new category, or leaving StaleObject as Superseded because VBR
        # purges removed-VM data itself, plan S5b) are Ben's to choose, and
        # these tests would then be rewritten, not just un-skipped.

        It 'known bug: R1 - does not call a <Reason> group Superseded when its newest point is inside the window' -ForEach @(
            @{ Reason = 'StaleObject' }
            @{ Reason = 'Tier2Suppressed' }
        ) {
            # known bug: R1 - Superseded ignored recency (fixed: StaleObject/Tier2Suppressed use the same window as Unresolved)
            $rows = script:Invoke-CategoryProbe -Reason $Reason -AgesInDays @(0.04)
            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'R1 control: a <Reason> group whose newest point is older than the window is still Superseded' -ForEach @(
            @{ Reason = 'StaleObject' }
            @{ Reason = 'Tier2Suppressed' }
        ) {
            $rows = script:Invoke-CategoryProbe -Reason $Reason -AgesInDays @(60)
            $script:CapturedRows | Should -HaveCount 1
            $script:CapturedRows[0].Category | Should -Be 'Superseded'
        }
    }

    Context 'R1: window edge and unusable timestamps for Superseded' {
        It 'R1: a <Reason> group with a future-dated newest point is Unresolved (unusable/future counts as active)' -ForEach @(
            @{ Reason = 'StaleObject' }
            @{ Reason = 'Tier2Suppressed' }
        ) {
            script:Invoke-CategoryProbe -Reason $Reason -AgesInDays @(-3)
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
        }

        It 'R1: a <Reason> group with an unusable timestamp is Unresolved' -ForEach @(
            @{ Reason = 'StaleObject' }
            @{ Reason = 'Tier2Suppressed' }
        ) {
            script:Invoke-CategoryProbe -Reason $Reason -AgesInDays @(60, $null)
            $script:CapturedRows | ForEach-Object { $_.Category | Should -Be 'Unresolved' }
        }

        It 'R1: respects -ActiveChainWindowDays for Superseded (3-day-old group: Unresolved at 35, Superseded at 1)' {
            script:Invoke-CategoryProbe -Reason 'StaleObject' -AgesInDays @(3)
            $script:CapturedRows[0].Category | Should -Be 'Unresolved'
            $script:CapturedRows.Clear()
            script:Invoke-CategoryProbe -Reason 'StaleObject' -AgesInDays @(3) -ExtraParams @{ ActiveChainWindowDays = 1 }
            $script:CapturedRows[0].Category | Should -Be 'Superseded'
        }

        It 'R1: an old StaleObject chain stays Superseded even when a live sibling shares its parent (judged on its own points)' {
            $Parent = script:New-FakeBackupObject -Name 'PerVmJob' -Id ([guid]::NewGuid())
            $Old  = script:New-FakeCandidateRestorePoint -Name 'removed-vm' -Type 'Full' -BackupObject $Parent -CreationTimeUtc ([datetime]::UtcNow.AddDays(-60))
            $Live = script:New-FakeCandidateRestorePoint -Name 'live-vm' -Type 'Full' -BackupObject $Parent -CreationTimeUtc ([datetime]::UtcNow.AddHours(-1))
            $script:VhcOrphanedSupersededCache = [PSCustomObject]@{ SweepRan = $true; CandidateGroups = [System.Collections.Generic.List[object]]::new() }
            $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{ Reason = 'StaleObject'; CurrentJobId = [guid]::NewGuid().ToString(); RestorePoints = @($Old) })

            Get-VhcOrphanedSupersededBackups

            $script:CapturedRows[0].Category | Should -Be 'Superseded'
        }
    }

    It 'uses the explicit -OrphanedSupersededCache parameter over $script:VhcOrphanedSupersededCache when both are present' {
        # Get-VBRConfig.ps1 now passes Get-VhcJob's return value here
        # explicitly, rather than relying on this function reaching for
        # $script:VhcOrphanedSupersededCache as an implicit side effect of
        # Get-VhcJob having already run first in the same collection pass.
        # The explicit parameter must win when both are present, and this
        # function must work correctly using ONLY the parameter - proving
        # the hidden collector-ordering dependency is gone.
        $Backup = script:New-FakeBackupObject -Name 'Explicit Param Job' -TypeToString 'VMware Backup'
        $Point  = script:New-FakeCandidateRestorePoint -Name 'vm-explicit' -Type 'Full' -ApproxSize 5GB -BackupObject $Backup
        $ExplicitCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $ExplicitCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($Point)
        })
        # Deliberately different/stale script-scoped cache, to prove the
        # explicit parameter - not this - is what gets read.
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $false
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }

        Get-VhcOrphanedSupersededBackups -OrphanedSupersededCache $ExplicitCache

        $script:CapturedRows | Should -HaveCount 1
        $script:CapturedRows[0].JobName | Should -Be 'Explicit Param Job'
        $script:CapturedMeta[0].SweepRan | Should -BeTrue
    }

    It 'exports a Superseded row for a Tier2Suppressed/StaleObject group, using the given CurrentJobId' {
        $Backup = script:New-FakeBackupObject -Name 'VBR Managed Agents - Windows' -TypeToString 'Windows Agent Policy'
        $Point  = script:New-FakeCandidateRestorePoint -Name 'WindowsAgent08' -Type 'Increment' -ApproxSize 8GB -BackupObject $Backup -CreationTimeUtc ([datetime]::UtcNow.AddDays(-60))
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

    It 'exports a Superseded row for a StaleObject group even when CurrentJobId is null' {
        # Regression test: a StaleObject candidate (#197) always belongs to
        # a live, currently-configured job by construction - Get-VhcJob.ps1
        # only emits Reason='StaleObject' after confirming the job matched
        # at least one of its own current restore points - but CurrentJobId
        # can still be $null in the edge case where $Job.Id itself returned
        # null. Category must come from Reason, not from CurrentJobId's
        # truthiness, or this candidate gets misclassified as 'Orphaned'
        # ("no live job") when the opposite is true.
        $Backup = script:New-FakeBackupObject -Name 'VBR Managed Agents - Windows' -TypeToString 'Windows Agent Policy'
        $Point  = script:New-FakeCandidateRestorePoint -Name 'WindowsAgent08' -Type 'Increment' -ApproxSize 8GB -BackupObject $Backup -CreationTimeUtc ([datetime]::UtcNow.AddDays(-60))
        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'StaleObject'
            CurrentJobId  = $null
            RestorePoints = @($Point)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -HaveCount 1
        $script:CapturedRows[0].Category | Should -Be 'Superseded'
        $script:CapturedRows[0].CurrentJobId | Should -Be ([guid]::Empty).ToString()
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
        # ApproxSize (100GB/120GB/10GB) is deliberately set to different
        # values than BackupSize (40GB/50GB/5GB) here: this is the source
        # VM's original/uncompressed size (VBR console's "Original Size"),
        # not the actual on-disk backup footprint this report exists to
        # estimate (VBR console's "Backup Size"). If the collector ever
        # regresses to reading ApproxSize again, these assertions fail
        # instead of silently passing because the fixture happened to
        # default both to the same value.
        $Backup = script:New-FakeBackupObject -Name 'VMware - Malware' -TypeToString 'VMware Backup'
        $ObjId = [guid]::NewGuid()
        $BkId  = [guid]::NewGuid()
        $Points = @(
            (script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Full' -ObjectId $ObjId -BackupId $BkId -ApproxSize 100GB -BackupSize 40GB -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-01-01')),
            (script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Full' -ObjectId $ObjId -BackupId $BkId -ApproxSize 120GB -BackupSize 50GB -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-03-01')),
            (script:New-FakeCandidateRestorePoint -Name 'MALWARE' -Type 'Increment' -ObjectId $ObjId -BackupId $BkId -ApproxSize 10GB -BackupSize 5GB -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-02-01'))
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
        [double]$row.AvgFullSizeBytes | Should -Be 48318382080    # (40GB+50GB)/2 = 96636764160/2
        [double]$row.AvgIncrementalSizeBytes | Should -Be 5368709120   # 5GB
        [double]$row.TotalSizeBytes | Should -Be 102005473280    # 40GB+50GB+5GB = 95GB
        [datetime]$row.OldestRestorePoint | Should -Be (Get-Date '2026-01-01')
        [datetime]$row.NewestRestorePoint | Should -Be (Get-Date '2026-03-01')
    }

    It 'writes AvgFullSizeBytes/AvgIncrementalSizeBytes/TotalSizeBytes/OldestRestorePoint/NewestRestorePoint in an invariant, host-culture-independent wire format' {
        # Regression test: Export-Csv (via Export-VhciCsv) stringifies non-string
        # properties using the collecting host's CURRENT culture, not invariant.
        # The C# consumer (OrphanedSupersededBackupAggregator.MapRow) parses
        # these columns with CultureInfo.InvariantCulture, so on a comma-decimal
        # / dd.MM.yyyy host culture (most of continental Europe, UK, AU/NZ,
        # India, LatAm), un-pinned raw [double]/[DateTime] values would be
        # exported comma-decimal / day-first and either silently misparsed
        # (e.g. "8500000000,5" read as 85000000005 - ~10x inflated) or throw
        # and drop the whole row. Proves the fix by actually switching the
        # thread's current culture before exporting, then asserting the
        # exported strings are period-decimal / ISO 8601 regardless.
        $OriginalCulture = [System.Threading.Thread]::CurrentThread.CurrentCulture
        try {
            [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('de-DE')

            $Backup = script:New-FakeBackupObject -Name 'VMware - Culture Check' -TypeToString 'VMware Backup'
            $Point  = script:New-FakeCandidateRestorePoint -Name 'CultureCheckVM' -Type 'Full' -BackupSize 8500000000.5 -BackupObject $Backup -CreationTimeUtc (Get-Date '2026-03-07T10:30:00')
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
        } finally {
            [System.Threading.Thread]::CurrentThread.CurrentCulture = $OriginalCulture
        }

        $row = $script:CapturedRows[0]
        # GetType() checks first and separately from content checks: the
        # mocked Export-VhciCsv captures the PSCustomObject BEFORE any real
        # Export-Csv serialization, so a loose "-Be '8500000000.5'" content
        # comparison against an un-pinned raw [double]/[DateTime] value can
        # still pass via PowerShell's own type coercion, hiding the exact
        # regression this test exists to catch. Asserting the captured
        # value's runtime type is [string] is what actually proves the
        # producer now pins the wire format itself, rather than leaving it
        # to Export-Csv's implicit (culture-dependent) ToString().
        $row.AvgFullSizeBytes.GetType().FullName        | Should -Be 'System.String'
        $row.TotalSizeBytes.GetType().FullName          | Should -Be 'System.String'
        $row.AvgIncrementalSizeBytes.GetType().FullName | Should -Be 'System.String'
        $row.OldestRestorePoint.GetType().FullName      | Should -Be 'System.String'
        $row.NewestRestorePoint.GetType().FullName      | Should -Be 'System.String'

        # Period decimal, no de-DE comma and no thousands grouping.
        $row.AvgFullSizeBytes         | Should -Be '8500000000.5'
        $row.TotalSizeBytes           | Should -Be '8500000000.5'
        $row.AvgIncrementalSizeBytes  | Should -Be '0'
        # ISO 8601 round-trip, year-month-day order - unambiguous regardless
        # of a dd/MM host culture.
        $row.OldestRestorePoint       | Should -Match '^2026-03-07T'
        $row.NewestRestorePoint       | Should -Match '^2026-03-07T'
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

    It 'treats a Guid.Empty RepositoryId as blank/unknown, not a literal all-zero-Guid value' {
        # Regression test: [guid]::Empty is truthy in PowerShell ($g -eq
        # [guid]::Empty doesn't make `if ($g)` false), so a "no repository"
        # sentinel from the VBR SDK would otherwise pass the
        # "$RepositoryId present" check, skip the blank-out, and get
        # exported as the literal all-zero Guid string instead of blank -
        # landing in a meaningless distinct group downstream instead of the
        # C# table's "unknown" bucket (which only recognizes a true $null).
        $Backup = script:New-FakeBackupObject -Name 'VMware - Malware' -TypeToString 'VMware Backup' -RepositoryId ([guid]::Empty)
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
        $RepositoryDetails = @([PSCustomObject]@{ Id = [guid]::Empty; Name = 'Should not resolve' })

        Get-VhcOrphanedSupersededBackups -RepositoryDetails $RepositoryDetails

        $script:CapturedRows[0].RepositoryId   | Should -BeNullOrEmpty
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

    It 'keeps sibling ObjectIds when one restore point in the group has a null ObjectId' {
        $Backup = script:New-FakeBackupObject -Name 'Mixed - Null ObjectId' -TypeToString 'VMware Backup'
        $SharedBackupId = [guid]::NewGuid()
        $GoodObjectId = [guid]::NewGuid()
        $GoodPoint = script:New-FakeCandidateRestorePoint -Name 'goodvm' -Type 'Full' -ObjectId $GoodObjectId -BackupId $SharedBackupId -BackupObject $Backup
        # New-FakeCandidateRestorePoint's -ObjectId param is typed [guid],
        # which can't bind $null - set it directly on the constructed
        # object instead, to fake a restore point VBR returned with no
        # ObjectId at all.
        $NullObjectPoint = script:New-FakeCandidateRestorePoint -Name 'nullobjectvm' -Type 'Full' -BackupId $SharedBackupId -BackupObject $Backup
        $NullObjectPoint.ObjectId = $null

        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($GoodPoint, $NullObjectPoint)
        })

        Get-VhcOrphanedSupersededBackups

        # Before the fix, $RestorePoint.ObjectId.ToString() on the null
        # -ObjectId point threw inside the group's own try/catch, which
        # discarded BOTH rows (including the good sibling ObjectId) rather
        # than just the one point that couldn't be classified.
        $script:CapturedRows | Should -HaveCount 2
        ($script:CapturedRows | Where-Object { $_.ObjectName -eq 'goodvm' }) | Should -HaveCount 1
        ($script:CapturedRows | Where-Object { $_.ObjectName -eq 'nullobjectvm' }) | Should -HaveCount 1
    }

    It 'keeps sibling ObjectIds when one restore point in the group fails GetStorage()' {
        # Regression test: ApproxSize (what the size fields used to read) is
        # a plain property that can't throw. GetStorage().Stats.BackupSize
        # (the actual on-disk size, now used instead) can - and this whole
        # foreach ($ObjKey ...) loop runs inside the candidate's own
        # try/catch, so an unguarded throw here would discard every ObjectId
        # row in the group, not just the one point with unresolvable storage
        # metadata. Same failure shape - and same fix shape - as the null-
        # ObjectId sibling test above.
        $Backup = script:New-FakeBackupObject -Name 'Mixed - Storage Failure' -TypeToString 'VMware Backup'
        $SharedBackupId = [guid]::NewGuid()
        $GoodPoint = script:New-FakeCandidateRestorePoint -Name 'goodvm' -Type 'Full' -BackupSize 10GB -BackupId $SharedBackupId -BackupObject $Backup
        $ThrowingPoint = script:New-FakeCandidateRestorePoint -Name 'brokenstoragevm' -Type 'Full' -BackupId $SharedBackupId -BackupObject $Backup -ThrowOnGetStorage

        $script:VhcOrphanedSupersededCache = [PSCustomObject]@{
            SweepRan        = $true
            CandidateGroups = [System.Collections.Generic.List[object]]::new()
        }
        $script:VhcOrphanedSupersededCache.CandidateGroups.Add([PSCustomObject]@{
            Reason        = 'Unresolved'
            CurrentJobId  = $null
            RestorePoints = @($GoodPoint, $ThrowingPoint)
        })

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -HaveCount 2
        $GoodRow = $script:CapturedRows | Where-Object { $_.ObjectName -eq 'goodvm' }
        $BrokenRow = $script:CapturedRows | Where-Object { $_.ObjectName -eq 'brokenstoragevm' }
        $GoodRow | Should -HaveCount 1
        $BrokenRow | Should -HaveCount 1
        # The good sibling's own size must reflect only its own point (not
        # zeroed out, not contaminated by the throwing point).
        [double]$GoodRow.TotalSizeBytes | Should -Be 10GB
        # The point whose storage couldn't be read contributes no size data
        # (excluded, not counted as 0) but still gets a row - "unresolvable
        # size" is not the same as "no size", and Get-VhciBackupSizeOrNull's
        # design keeps them apart.
        [double]$BrokenRow.TotalSizeBytes | Should -Be 0
        [int]$BrokenRow.FullCount | Should -Be 1
    }

    It 'keeps two null-ObjectId restore points in the same BackupId group as separate, uncontaminated rows' {
        $Backup = script:New-FakeBackupObject -Name 'Two Null ObjectIds' -TypeToString 'VMware Backup'
        $SharedBackupId = [guid]::NewGuid()
        # Both points share one BackupId and both have a null ObjectId -
        # the exact shape a buggy grouping key (e.g. a bare postfix
        # increment inside a string subexpression, which silently
        # evaluates to an empty string every time in PowerShell) would
        # collapse onto one shared key, re-merging their counts/sizes.
        $Point1 = script:New-FakeCandidateRestorePoint -Name 'nullobj-a' -Type 'Full' -BackupSize 10GB -BackupId $SharedBackupId -BackupObject $Backup
        $Point1.ObjectId = $null
        $Point2 = script:New-FakeCandidateRestorePoint -Name 'nullobj-b' -Type 'Full' -BackupSize 999GB -BackupId $SharedBackupId -BackupObject $Backup
        $Point2.ObjectId = $null

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

        # HaveCount 2 alone isn't sufficient - some buggy grouping shapes
        # can still produce a nonzero-but-wrong row count. The real proof
        # each row is uncontaminated is that its own FullCount/
        # TotalSizeBytes reflects only its own point, not both summed
        # together (e.g. FullCount=2 / TotalSizeBytes=1083405500416, the
        # blended shape a shared key produces).
        $script:CapturedRows | Should -HaveCount 2

        $RowA = $script:CapturedRows | Where-Object { $_.ObjectName -eq 'nullobj-a' }
        $RowB = $script:CapturedRows | Where-Object { $_.ObjectName -eq 'nullobj-b' }
        $RowA | Should -HaveCount 1
        $RowB | Should -HaveCount 1
        [int]$RowA.FullCount | Should -Be 1
        [int]$RowB.FullCount | Should -Be 1
        [double]$RowA.TotalSizeBytes | Should -Be 10GB
        [double]$RowB.TotalSizeBytes | Should -Be 999GB
    }

    It 'still exports a one-row meta CSV with SweepRan=false when the cache itself is $null' {
        # A real, reachable path: Get-VhcJob's sub-collectors (via
        # Invoke-VhciJobSubCollectors) can throw before ever assigning
        # $script:VhcOrphanedSupersededCache, leaving it $null rather than
        # the SweepRan=$false placeholder object Get-VhcJob normally seeds
        # up front. Before the fix, this case returned before exporting
        # either CSV - including the meta file whose entire purpose is
        # covering exactly this ambiguity.
        $script:VhcOrphanedSupersededCache = $null

        Get-VhcOrphanedSupersededBackups

        $script:CapturedRows | Should -BeNullOrEmpty
        $script:CapturedMeta | Should -HaveCount 1
        $script:CapturedMeta[0].SweepRan | Should -Be $false
    }
}
