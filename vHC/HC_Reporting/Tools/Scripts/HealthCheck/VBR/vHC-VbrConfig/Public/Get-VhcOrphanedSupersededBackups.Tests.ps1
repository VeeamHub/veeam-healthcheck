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
