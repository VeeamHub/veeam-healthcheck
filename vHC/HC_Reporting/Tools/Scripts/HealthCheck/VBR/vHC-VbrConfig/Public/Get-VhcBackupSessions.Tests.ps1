#Requires -Version 7.0
# Pester v5 tests for Get-VhcBackupSessions (ISC-1 through ISC-7).
#
# Rewritten 2026-05-27 (ADR 0018, issue #147). The previous ISC-2 asserted
# Get-VBRBackupSession -Job binding; that parameter does not exist in any
# released VBR version. The function now delegates path selection to the
# private Get-VhciJobSessions helper, which is tested separately.
#
# PS 7 is required because the project's test convention runs Pester v5 under
# pwsh; PS 5.1 ships Pester v3 which lacks the Should -BeOfType syntax used
# below. See docs/plans/2026-05-27-vbr-session-fast-path.md Task Conventions.

BeforeAll {
    if (-not (Get-Command Get-VBRJob -ErrorAction SilentlyContinue)) {
        function global:Get-VBRJob { param([string]$ErrorAction) }
    }
    if (-not (Get-Command Get-VBRComputerBackupJob -ErrorAction SilentlyContinue)) {
        function global:Get-VBRComputerBackupJob { param([string]$ErrorAction) }
    }
    if (-not (Get-Command Get-VBREPJob -ErrorAction SilentlyContinue)) {
        function global:Get-VBREPJob { param([string]$ErrorAction) }
    }
    if (-not (Get-Command Get-VBRBackupSession -ErrorAction SilentlyContinue)) {
        function global:Get-VBRBackupSession { }
    }
    if (-not (Get-Command Get-VBRComputerBackupJobSession -ErrorAction SilentlyContinue)) {
        function global:Get-VBRComputerBackupJobSession { }
    }

    function script:New-FakeJob {
        param([string]$Name = 'FakeJob', [guid]$Id = [guid]::NewGuid())
        [PSCustomObject]@{ Name = $Name; Id = $Id }
    }

    # Dot-source Write-LogFile (production logger), Get-VhciJobSessions and its
    # deps (so Mock can attach to them), then the function under test.
    $moduleRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $moduleRoot 'Public/Write-LogFile.ps1')
    . (Join-Path $moduleRoot 'Private/Test-VhciCBackupSessionFastPath.ps1')
    . (Join-Path $moduleRoot 'Private/Invoke-VhciCBackupSessionFetch.ps1')
    . (Join-Path $moduleRoot 'Private/Get-VhciJobSessions.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
}

# ---------------------------------------------------------------------------
# ISC-1  Smoke: function exists, empty job lists -> no throw, returns array
# ---------------------------------------------------------------------------
Describe 'ISC-1: Empty job lists do not throw' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @() }
        Mock Get-VBRComputerBackupJob -MockWith { @() }
        Mock Get-VhciJobSessions      -MockWith { @() }
    }

    It 'does not throw' {
        { @(Get-VhcBackupSessions -ReportInterval 7) } | Should -Not -Throw
    }

    It 'returns an empty [object[]] when both job sources are empty' {
        $result = @(Get-VhcBackupSessions -ReportInterval 7)
        $result.GetType().Name | Should -Be 'Object[]'
        $result.Count | Should -Be 0
    }
}

# ---------------------------------------------------------------------------
# ISC-2  Get-VhciJobSessions invoked twice with the correct -PathLabel values
# ---------------------------------------------------------------------------
Describe 'ISC-2: Helper invoked once per session family with correct PathLabel' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @((script:New-FakeJob 'V1')) }
        Mock Get-VBRComputerBackupJob -MockWith { @((script:New-FakeJob 'A1')) }
        Mock Get-VhciJobSessions      -MockWith { @() }
    }

    It 'invokes Get-VhciJobSessions exactly twice' {
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        Should -Invoke Get-VhciJobSessions -Times 2 -Exactly
    }

    It 'invokes once with PathLabel ''VM/BackupCopy''' {
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter { $PathLabel -eq 'VM/BackupCopy' }
    }

    It 'invokes once with PathLabel ''Agent''' {
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter { $PathLabel -eq 'Agent' }
    }
}

# ---------------------------------------------------------------------------
# ISC-3  Slow-path scriptblocks routed to the correct cmdlets
# ---------------------------------------------------------------------------
Describe 'ISC-3: SlowPathCommand routes to the expected cmdlet per family' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @((script:New-FakeJob 'V1')) }
        Mock Get-VBRComputerBackupJob -MockWith { @((script:New-FakeJob 'A1')) }
        Mock Get-VhciJobSessions      -MockWith { @() }
    }

    It 'VM/BackupCopy scriptblock text contains Get-VBRBackupSession' {
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter {
            $PathLabel -eq 'VM/BackupCopy' -and $SlowPathCommand.ToString() -match '\bGet-VBRBackupSession\b'
        }
    }

    It 'Agent scriptblock text contains Get-VBRComputerBackupJobSession' {
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter {
            $PathLabel -eq 'Agent' -and $SlowPathCommand.ToString() -match '\bGet-VBRComputerBackupJobSession\b'
        }
    }
}

# ---------------------------------------------------------------------------
# ISC-4  $Since == (Get-Date).AddDays(-ReportInterval) within ~1s tolerance
# ---------------------------------------------------------------------------
Describe 'ISC-4: $Since propagation matches ReportInterval' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @() }
        Mock Get-VBRComputerBackupJob -MockWith { @() }
        Mock Get-VhciJobSessions      -MockWith { @() }
    }

    It 'passes a Since within 5 seconds of (Get-Date).AddDays(-7)' {
        # 5s tolerance accommodates slow CI runners; the function call between
        # captures of $expected and $Since happens inside Pester's harness.
        $expected = (Get-Date).AddDays(-7)
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        Should -Invoke Get-VhciJobSessions -Times 2 -Exactly -ParameterFilter {
            [Math]::Abs(($Since - $expected).TotalSeconds) -lt 5
        }
    }
}

# ---------------------------------------------------------------------------
# ISC-5  Return concatenates both helper calls
# ---------------------------------------------------------------------------
Describe 'ISC-5: Return is the concatenation of both helper calls' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @((script:New-FakeJob 'V1')) }
        Mock Get-VBRComputerBackupJob -MockWith { @((script:New-FakeJob 'A1')) }
        # Return distinct sentinel arrays per family so we can verify concat.
        Mock Get-VhciJobSessions -MockWith {
            if ($PathLabel -eq 'VM/BackupCopy') {
                return @([PSCustomObject]@{ Tag = 'vm1' }, [PSCustomObject]@{ Tag = 'vm2' })
            }
            return @([PSCustomObject]@{ Tag = 'agent1' })
        }
    }

    It 'returns 3 sessions (2 vm + 1 agent)' {
        $result = @(Get-VhcBackupSessions -ReportInterval 7)
        $result.Count | Should -Be 3
    }

    It 'contains both vm and agent sentinels' {
        $result = @(Get-VhcBackupSessions -ReportInterval 7)
        ($result | Where-Object { $_.Tag -like 'vm*' }).Count    | Should -Be 2
        ($result | Where-Object { $_.Tag -like 'agent*' }).Count | Should -Be 1
    }
}

# ---------------------------------------------------------------------------
# ISC-6  One helper call throws -> other still runs, partial result returned
# ---------------------------------------------------------------------------
Describe 'ISC-6: One helper throw does not terminate the other' {

    BeforeEach {
        $script:warnMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith {
            if ($LogLevel -eq 'WARNING') { $script:warnMessages.Add($Message) }
        }
        Mock Get-VBRJob               -MockWith { @((script:New-FakeJob 'V1')) }
        Mock Get-VBRComputerBackupJob -MockWith { @((script:New-FakeJob 'A1')) }
        Mock Get-VhciJobSessions -MockWith {
            if ($PathLabel -eq 'VM/BackupCopy') { throw 'Simulated VM helper failure' }
            return @([PSCustomObject]@{ Tag = 'agent1' })
        }
    }

    It 'does not throw' {
        { @(Get-VhcBackupSessions -ReportInterval 7) } | Should -Not -Throw
    }

    It 'returns the surviving agent session' {
        $result = @(Get-VhcBackupSessions -ReportInterval 7)
        $result.Count | Should -Be 1
        ($result | Where-Object { $_.Tag -eq 'agent1' }).Count | Should -Be 1
    }

    It 'logs a WARNING when the failing helper throws' {
        @(Get-VhcBackupSessions -ReportInterval 7) | Out-Null
        ($script:warnMessages | Where-Object { $_ -match 'Simulated VM helper failure' }).Count | Should -BeGreaterThan 0
    }
}

# ---------------------------------------------------------------------------
# ISC-7  Return shape: flat [object[]], not nested ArrayList
# ---------------------------------------------------------------------------
Describe 'ISC-7: Return is a flat [object[]]' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Get-VBRJob               -MockWith { @((script:New-FakeJob 'V1')) }
        Mock Get-VBRComputerBackupJob -MockWith { @((script:New-FakeJob 'A1')) }
        Mock Get-VhciJobSessions -MockWith {
            @([PSCustomObject]@{ Tag = 'x' }, [PSCustomObject]@{ Tag = 'y' })
        }
    }

    It 'returns [object[]]' {
        $result = @(Get-VhcBackupSessions -ReportInterval 7)
        $result.GetType().Name | Should -Be 'Object[]'
    }

    It 'is flat (no element is an ArrayList or array)' {
        $result = @(Get-VhcBackupSessions -ReportInterval 7)
        foreach ($item in $result) {
            $item | Should -Not -BeOfType [System.Collections.ArrayList]
            $item.GetType().IsArray | Should -BeFalse
        }
    }
}

# ---------------------------------------------------------------------------
# ISC-8  Issue #224: jobs Get-VBRJob does not return (found by the shared
#        discovery step) must reach the per-job session fetch
# ---------------------------------------------------------------------------
Describe 'ISC-8: Discovered jobs are queried for sessions (#224)' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        $script:VisibleJob    = script:New-FakeJob 'Visible'
        $script:HiddenJob     = script:New-FakeJob 'HiddenFromGetVBRJob'
        $script:AgentJob      = script:New-FakeJob 'Agent1'
        Mock Get-VBRJob               -MockWith { @($script:VisibleJob) }
        Mock Get-VBRComputerBackupJob -MockWith { @($script:AgentJob) }
        Mock Get-VBREPJob             -MockWith { @() }
        Mock Get-VhciJobSessions      -MockWith { @() }
        $script:Discovery = [PSCustomObject]@{
            VbrJobs        = @($script:VisibleJob)
            DiscoveredJobs = @($script:HiddenJob)
        }
    }

    It 'passes the discovered job to the VM/BackupCopy session fetch' {
        $null = @(Get-VhcBackupSessions -ReportInterval 7 -JobDiscovery $script:Discovery)
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter {
            $PathLabel -eq 'VM/BackupCopy' -and
            @($Jobs | Where-Object { $_.Id -eq $script:HiddenJob.Id }).Count -eq 1
        }
    }

    It 'still passes the Get-VBRJob-visible job exactly once' {
        $null = @(Get-VhcBackupSessions -ReportInterval 7 -JobDiscovery $script:Discovery)
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter {
            $PathLabel -eq 'VM/BackupCopy' -and
            @($Jobs | Where-Object { $_.Id -eq $script:VisibleJob.Id }).Count -eq 1
        }
    }

    It 'does not call Get-VBRJob again when discovery is supplied' {
        $null = @(Get-VhcBackupSessions -ReportInterval 7 -JobDiscovery $script:Discovery)
        Should -Invoke Get-VBRJob -Times 0 -Exactly
    }

    It 'subtracts an agent job that discovery also returned (no duplicate fetch)' {
        $script:Discovery.DiscoveredJobs = @($script:HiddenJob, $script:AgentJob)
        $null = @(Get-VhcBackupSessions -ReportInterval 7 -JobDiscovery $script:Discovery)
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter {
            $PathLabel -eq 'VM/BackupCopy' -and
            @($Jobs | Where-Object { $_.Id -eq $script:AgentJob.Id }).Count -eq 0
        }
    }

    It 'falls back to its own Get-VBRJob when no discovery is supplied (unchanged behaviour)' {
        $null = @(Get-VhcBackupSessions -ReportInterval 7)
        Should -Invoke Get-VBRJob -Times 1 -Exactly
        Should -Invoke Get-VhciJobSessions -Times 1 -Exactly -ParameterFilter {
            $PathLabel -eq 'VM/BackupCopy' -and @($Jobs).Count -eq 1
        }
    }
}
