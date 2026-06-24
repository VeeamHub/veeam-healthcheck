#Requires -Version 7.0
# Pester v5 tests for Get-VhcSessionReport - new JobId/PolicyName/PolicyTag columns.

BeforeAll {
    $publicDir  = Split-Path -Parent $PSCommandPath
    $moduleRoot = Split-Path -Parent $publicDir
    $privateDir = Join-Path $moduleRoot 'Private'

    # Stub VBR cmdlets so Mock can attach. None exist outside the VBR PS module.
    if (-not (Get-Command Get-VBRJob               -ErrorAction SilentlyContinue)) { function global:Get-VBRJob               { param([string]$ErrorAction) } }
    if (-not (Get-Command Get-VBRComputerBackupJob -ErrorAction SilentlyContinue)) { function global:Get-VBRComputerBackupJob { param([string]$ErrorAction) } }
    if (-not (Get-Command Get-VBREPJob             -ErrorAction SilentlyContinue)) { function global:Get-VBREPJob             { param([string]$ErrorAction) } }
    if (-not (Get-Command Get-VBRTaskSession       -ErrorAction SilentlyContinue)) { function global:Get-VBRTaskSession       { param($Session) } }

    # Dot-source the helper that the SUT calls plus the SUT itself.
    . (Join-Path $privateDir 'Get-VhciSessionLogWithTimeout.ps1')
    # SUT now pipes its CSV through Protect-VhciCsvInjection (formula-injection
    # neutralizer); dot-source it too or the export call throws CommandNotFound.
    . (Join-Path $privateDir 'Protect-VhciCsvInjection.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
    . (Join-Path $publicDir 'Write-LogFile.ps1')

    # Set script-scoped ReportPath so Export-Csv has somewhere to land.
    # The SUT reads $script:ReportPath inside Get-VhcSessionReport - because it
    # was dot-sourced into the Pester container's script scope, the same scope
    # used here, direct assignment is the simplest reliable way to set it.
    $script:tempDir = Join-Path ([IO.Path]::GetTempPath()) "vhc-session-test-$([guid]::NewGuid())"
    New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    $script:ReportPath = $script:tempDir
}

AfterAll {
    if (Test-Path $script:tempDir) { Remove-Item -Recurse -Force $script:tempDir }
}

Describe 'GVSR-1: Session CSV includes JobId/PolicyName/PolicyTag columns' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @() }
        Mock Get-VBRComputerBackupJob -MockWith { @() }
        Mock Get-VBREPJob             -MockWith { @() }
        Mock Get-VhciSessionLogWithTimeout -MockWith { @() }

        # Fake task that Get-VBRTaskSession will return for our fake session
        $parentGuid = [guid]'02fe84bc-7394-42b5-bdb2-81a56190d8c5'
        $childGuid  = [guid]'592c44dc-861c-48fc-b70e-e9916c790222'
        $script:childSession  = [pscustomobject]@{
            Name = 'Physical - Linux Servers - lab-m01-lnx01 (Incremental)'
            JobId = $childGuid
            Info = [pscustomobject]@{ PolicyName = 'Physical - Linux Servers'; PolicyTag = $parentGuid }
        }

        $script:fakeTask = [pscustomobject]@{
            Name = 'lab-m01-lnx01'
            Status = 'Success'
            JobName = 'Physical - Linux Servers - lab-m01-lnx01'
            ObjectPlatform = [pscustomobject]@{ IsEpAgentPlatform = $true; Platform = 'EpAgent' }
            JobSess = [pscustomobject]@{
                IsRetryMode = $false
                CreationTime = (Get-Date).AddDays(-1)
                BackupStats = [pscustomobject]@{ BackupSize = 100; DataSize = 200; DedupRatio = 1; CompressRatio = 1 }
                Progress = [pscustomobject]@{
                    TransferedSize = 0; ReadSize = 0;
                    Duration = [timespan]::FromMinutes(5);
                    BottleneckInfo = [pscustomobject]@{ Source=0; Proxy=0; Network=0; Target=0; Bottleneck=0 }
                }
                Info = [pscustomobject]@{ SessionAlgorithm = 1 }
            }
            WorkDetails = [pscustomobject]@{ TaskAlgorithm = 'Increment'; WorkDuration = [timespan]::FromMinutes(5) }
        }
        # Get-VBRTaskSession's -Session parameter carries
        # VBRSessionTransformationAttribute when the real Veeam PS module is
        # loaded (e.g. on a VBR host). The attribute fires before the mock body
        # runs and refuses our PSCustomObject. -RemoveParameterType "Session"
        # tells Pester to drop the typed attribute so the mock accepts any input.
        Mock Get-VBRTaskSession -RemoveParameterType 'Session' -MockWith { @($script:fakeTask) }
    }

    It 'writes PolicyName and PolicyTag equal to the parent for a child session' {
        Get-VhcSessionReport -BackupSessions @($script:childSession)

        $csvPath = Join-Path $script:tempDir 'VeeamSessionReport.csv'
        Test-Path $csvPath | Should -BeTrue
        $rows = Import-Csv -Path $csvPath
        $rows.Count | Should -BeGreaterThan 0
        $rows[0].PolicyName | Should -Be 'Physical - Linux Servers'
        $rows[0].PolicyTag  | Should -Be '02fe84bc-7394-42b5-bdb2-81a56190d8c5'
        $rows[0].JobId      | Should -Be '592c44dc-861c-48fc-b70e-e9916c790222'
    }

    It 'leaves PolicyName/PolicyTag empty when Info has no PolicyName' {
        $bareSession = [pscustomobject]@{
            Name = 'Hyper-V - Engineers CHC 01'
            JobId = [guid]'68621a52-2a9c-4fc5-a3f4-acc1c2caa44e'
            Info = [pscustomobject]@{ PolicyName = ''; PolicyTag = [guid]::Empty }
        }
        Get-VhcSessionReport -BackupSessions @($bareSession)

        $rows = Import-Csv -Path (Join-Path $script:tempDir 'VeeamSessionReport.csv')
        $rows[0].PolicyName | Should -Be ''
        $rows[0].PolicyTag  | Should -Be ''
        $rows[0].JobId      | Should -Be '68621a52-2a9c-4fc5-a3f4-acc1c2caa44e'
    }
}
