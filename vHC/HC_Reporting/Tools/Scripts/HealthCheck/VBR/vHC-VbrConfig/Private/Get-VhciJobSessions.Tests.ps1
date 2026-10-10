#Requires -Version 7.0
# Pester v5 tests for Get-VhciJobSessions (GJS-1 through GJS-7).
# PS 7 is required because the project's test convention runs Pester v5 under
# pwsh; PS 5.1 ships Pester v3 which lacks the Should -BeOfType syntax used
# below. See docs/plans/2026-05-27-vbr-session-fast-path.md Task Conventions.
#
# Strategy: Test-VhciCBackupSessionFastPath and Invoke-VhciCBackupSessionFetch
# are pure PS functions so we Mock them directly. No Add-Type, no live VBR.

BeforeAll {
    # Dot-source the function under test PLUS its private deps so the names
    # exist for Pester's Mock to attach to.
    $privateDir = Split-Path -Parent $PSCommandPath
    . (Join-Path $privateDir 'Test-VhciCBackupSessionFastPath.ps1')
    . (Join-Path $privateDir 'Invoke-VhciCBackupSessionFetch.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')

    # Write-LogFile lives in Public/; dot-source it so Pester can mock it.
    $publicDir = Join-Path (Split-Path -Parent $privateDir) 'Public'
    . (Join-Path $publicDir 'Write-LogFile.ps1')

    # Helpers for fake jobs
    function script:New-FakeJob {
        param([string]$Name = 'FakeJob', [guid]$Id = [guid]::NewGuid())
        [PSCustomObject]@{ Name = $Name; Id = $Id }
    }
    function script:New-FakeSession {
        param([datetime]$CreationTime, [string]$JobName = 'FakeJob')
        [PSCustomObject]@{ CreationTime = $CreationTime; JobName = $JobName }
    }
}

# ---------------------------------------------------------------------------
# GJS-1  Probe $true -> fast branch; Invoke-VhciCBackupSessionFetch invoked,
#                       slow-path scriptblock never invoked
# ---------------------------------------------------------------------------
Describe 'GJS-1: Probe $true selects the fast path' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $true }
        Mock Invoke-VhciCBackupSessionFetch -MockWith {
            return @(script:New-FakeSession -CreationTime (Get-Date).AddHours(-1))
        }
        $script:slowCalled = $false
        $script:slowSb     = { $script:slowCalled = $true; @() }
    }

    It 'calls Invoke-VhciCBackupSessionFetch and not the slow-path scriptblock' {
        $job = script:New-FakeJob 'J1'
        @(Get-VhciJobSessions -Jobs @($job) -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 1 -Exactly
        $script:slowCalled | Should -BeFalse
    }

    It 'logs an INFO line containing "fast path"' {
        $script:infoMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith {
            if (-not $LogLevel -or $LogLevel -eq 'INFO') { $script:infoMessages.Add($Message) }
        }
        $job = script:New-FakeJob 'J1'
        @(Get-VhciJobSessions -Jobs @($job) -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        ($script:infoMessages | Where-Object { $_ -match 'fast path' }).Count | Should -BeGreaterThan 0
    }
}

# ---------------------------------------------------------------------------
# GJS-3  Fast path, 3 jobs -> Invoke-VhciCBackupSessionFetch invoked 3x with
#                              each job's Id and the supplied $Since
# ---------------------------------------------------------------------------
Describe 'GJS-3: Fast path iterates jobs with correct JobId and Since' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $true }
        Mock Invoke-VhciCBackupSessionFetch -MockWith {
            @(script:New-FakeSession -CreationTime (Get-Date).AddHours(-1))
        }
        $script:job1 = script:New-FakeJob 'J1'
        $script:job2 = script:New-FakeJob 'J2'
        $script:job3 = script:New-FakeJob 'J3'
        $script:cutoff = (Get-Date).AddDays(-7)
    }

    It 'invokes the fetch exactly 3 times' {
        @(Get-VhciJobSessions -Jobs @($script:job1, $script:job2, $script:job3) `
            -Since $script:cutoff -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 3 -Exactly
    }

    It 'passes each job''s Id to the fetch' {
        @(Get-VhciJobSessions -Jobs @($script:job1, $script:job2, $script:job3) `
            -Since $script:cutoff -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 1 -Exactly -ParameterFilter { $JobId -eq $script:job1.Id }
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 1 -Exactly -ParameterFilter { $JobId -eq $script:job2.Id }
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 1 -Exactly -ParameterFilter { $JobId -eq $script:job3.Id }
    }

    It 'passes the supplied $Since to the fetch' {
        @(Get-VhciJobSessions -Jobs @($script:job1) -Since $script:cutoff `
            -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 1 -Exactly -ParameterFilter { $Since -eq $script:cutoff }
    }

    It 'passes a $Until that is between $Since and (Get-Date).AddSeconds(1)' {
        # $Until is computed inside the helper as (Get-Date) at the start of the
        # fast-path branch. We can't pin the exact value but we can verify it
        # lives within a sensible window: after $Since and not in the future.
        $before = Get-Date
        @(Get-VhciJobSessions -Jobs @($script:job1) -Since $script:cutoff `
            -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        $after = (Get-Date).AddSeconds(1)  # tolerance for clock drift
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 1 -Exactly -ParameterFilter {
            $Until -gt $script:cutoff -and $Until -ge $before -and $Until -le $after
        }
    }
}

# ---------------------------------------------------------------------------
# GJS-2  Probe $false -> slow branch; scriptblock invoked once,
#                        Invoke-VhciCBackupSessionFetch never invoked
# ---------------------------------------------------------------------------
Describe 'GJS-2: Probe $false selects the slow path' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $false }
        Mock Invoke-VhciCBackupSessionFetch -MockWith { throw 'Should not be called' }
        $script:slowCallCount = 0
        $script:slowSb = {
            $script:slowCallCount++
            @(script:New-FakeSession -CreationTime (Get-Date).AddHours(-1))
        }
    }

    It 'invokes the slow-path scriptblock exactly once' {
        @(Get-VhciJobSessions -Jobs @((script:New-FakeJob 'J1')) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        $script:slowCallCount | Should -Be 1
    }

    It 'never invokes Invoke-VhciCBackupSessionFetch' {
        @(Get-VhciJobSessions -Jobs @((script:New-FakeJob 'J1')) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 0 -Exactly
    }

    It 'logs an INFO line containing "slow path"' {
        $script:infoMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith {
            if (-not $LogLevel -or $LogLevel -eq 'INFO') { $script:infoMessages.Add($Message) }
        }
        @(Get-VhciJobSessions -Jobs @((script:New-FakeJob 'J1')) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        ($script:infoMessages | Where-Object { $_ -match 'slow path' }).Count | Should -BeGreaterThan 0
    }
}

# ---------------------------------------------------------------------------
# GJS-5  Slow path applies the cutoff filter client-side
# ---------------------------------------------------------------------------
Describe 'GJS-5: Slow path filters by CreationTime > $Since' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $false }
        $script:slowSb = {
            @(
                (script:New-FakeSession -CreationTime (Get-Date).AddDays(-10)),  # outside window
                (script:New-FakeSession -CreationTime (Get-Date).AddHours(-1))   # inside window
            )
        }
    }

    It 'excludes sessions older than $Since' {
        # Slow path ignores $Jobs (correct per design); empty job list is fine here.
        $result = @(Get-VhciJobSessions -Jobs @() `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy')
        # Two sessions are fed in (one 10 days old, one 1 hour old). Only the
        # 1-hour-old one is within the 7-day window.
        $result.Count | Should -Be 1
    }

    It 'includes sessions newer than $Since' {
        $result = @(Get-VhciJobSessions -Jobs @() `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy')
        $result[0].CreationTime | Should -BeGreaterThan (Get-Date).AddDays(-7)
    }
}

# ---------------------------------------------------------------------------
# GJS-4  Fast path, mid-iteration throw -> WARNING logged with job name and
#                                          message; other jobs still processed
# ---------------------------------------------------------------------------
Describe 'GJS-4: Fast path mid-iteration throw is contained' {

    BeforeEach {
        $script:warnMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith {
            if ($LogLevel -eq 'WARNING') { $script:warnMessages.Add($Message) }
        }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $true }

        $script:goodJob  = script:New-FakeJob 'GoodJob'
        $script:badJob   = script:New-FakeJob 'BadJob'
        $script:goodJob2 = script:New-FakeJob 'GoodJob2'

        Mock Invoke-VhciCBackupSessionFetch -MockWith {
            if ($JobId -eq $script:badJob.Id) { throw 'Simulated fetch failure' }
            @(script:New-FakeSession -CreationTime (Get-Date).AddHours(-1) -JobName 'X')
        }
    }

    It 'does not throw when one job''s fetch fails' {
        { @(Get-VhciJobSessions -Jobs @($script:goodJob, $script:badJob, $script:goodJob2) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand { } -PathLabel 'VM/BackupCopy') } |
            Should -Not -Throw
    }

    It 'logs a WARNING containing the failing job name' {
        @(Get-VhciJobSessions -Jobs @($script:goodJob, $script:badJob, $script:goodJob2) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        ($script:warnMessages | Where-Object { $_ -match 'BadJob' }).Count | Should -BeGreaterThan 0
    }

    It 'logs a WARNING containing the exception message' {
        @(Get-VhciJobSessions -Jobs @($script:goodJob, $script:badJob, $script:goodJob2) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        ($script:warnMessages | Where-Object { $_ -match 'Simulated fetch failure' }).Count | Should -BeGreaterThan 0
    }

    It 'still returns sessions from the other two jobs' {
        $result = @(Get-VhciJobSessions -Jobs @($script:goodJob, $script:badJob, $script:goodJob2) `
            -Since (Get-Date).AddDays(-7) -SlowPathCommand { } -PathLabel 'VM/BackupCopy')
        $result.Count | Should -Be 2
    }
}

# ---------------------------------------------------------------------------
# GJS-6  Slow-path scriptblock throws -> WARNING logged; returns @()
# ---------------------------------------------------------------------------
Describe 'GJS-6: Slow-path scriptblock failure logs WARNING and returns empty' {

    BeforeEach {
        $script:warnMessages = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith {
            if ($LogLevel -eq 'WARNING') { $script:warnMessages.Add($Message) }
        }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $false }
        $script:slowSb = { throw 'Simulated cmdlet failure' }
    }

    It 'does not throw' {
        { @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') } |
            Should -Not -Throw
    }

    It 'logs a WARNING with the exception message' {
        @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        ($script:warnMessages | Where-Object { $_ -match 'Simulated cmdlet failure' }).Count | Should -BeGreaterThan 0
    }

    It 'returns an empty array' {
        $result = @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy')
        $result.Count | Should -Be 0
    }
}

# ---------------------------------------------------------------------------
# GJS-7  Empty $Jobs + fast path -> zero fetch calls, returns empty
# ---------------------------------------------------------------------------
Describe 'GJS-7: Empty $Jobs + fast path returns empty without calling fetch' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $true }
        Mock Invoke-VhciCBackupSessionFetch -MockWith { throw 'Should not be called' }
    }

    It 'never invokes Invoke-VhciCBackupSessionFetch' {
        @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand { } -PathLabel 'VM/BackupCopy') | Out-Null
        Should -Invoke Invoke-VhciCBackupSessionFetch -Times 0 -Exactly
    }

    It 'returns an empty array' {
        $result = @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand { } -PathLabel 'VM/BackupCopy')
        $result.Count | Should -Be 0
    }
}

# ---------------------------------------------------------------------------
# GJS-8  Empty $Jobs + slow path -> scriptblock still invoked once
#        (slow path ignores $Jobs by design per spec error-handling table)
# ---------------------------------------------------------------------------
Describe 'GJS-8: Empty $Jobs + slow path still invokes the scriptblock' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $false }
        $script:slowCallCount = 0
        $script:slowSb = {
            $script:slowCallCount++
            @(script:New-FakeSession -CreationTime (Get-Date).AddHours(-1))
        }
    }

    It 'invokes the slow-path scriptblock exactly once even with empty $Jobs' {
        @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy') | Out-Null
        $script:slowCallCount | Should -Be 1
    }

    It 'returns the (filtered) scriptblock output' {
        $result = @(Get-VhciJobSessions -Jobs @() -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand $script:slowSb -PathLabel 'VM/BackupCopy')
        $result.Count | Should -Be 1
    }
}

# ---------------------------------------------------------------------------
# GJS-9  Fast path returns each session once even when two queried jobs return
#        it (issue #224: a Backup Copy worker found by tier B/C is queried
#        alongside its parent, and the parent's query already returns the
#        per-machine child sessions - ADR 0018)
# ---------------------------------------------------------------------------
Describe 'GJS-9: A session returned for more than one job is collected once' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Test-VhciCBackupSessionFastPath -MockWith { $true }
        $script:SharedId = [guid]::NewGuid()
        $script:Shared   = [PSCustomObject]@{ Id = $script:SharedId; CreationTime = (Get-Date).AddHours(-1); JobName = 'Parent\Child' }
        $script:Own      = [PSCustomObject]@{ Id = [guid]::NewGuid(); CreationTime = (Get-Date).AddHours(-2); JobName = 'Worker' }
        $script:ParentJob = script:New-FakeJob 'Parent'
        $script:WorkerJob = script:New-FakeJob 'Parent\Child'
        Mock Invoke-VhciCBackupSessionFetch -MockWith {
            if ($JobId -eq $script:ParentJob.Id) { return @($script:Shared) }
            return @($script:Shared, $script:Own)
        }
    }

    It 'returns the shared session once and keeps the session only one job returned' {
        $r = @(Get-VhciJobSessions -Jobs @($script:ParentJob, $script:WorkerJob) -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand { @() } -PathLabel 'VM/BackupCopy')
        $r.Count | Should -Be 2
        @($r | Where-Object { $_.Id -eq $script:SharedId }).Count | Should -Be 1
        @($r | Where-Object { $_.Id -eq $script:Own.Id }).Count | Should -Be 1
    }

    It 'logs how many duplicate sessions it dropped, with the path label' {
        $script:logged = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:logged.Add($Message) }
        $null = @(Get-VhciJobSessions -Jobs @($script:ParentJob, $script:WorkerJob) -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand { @() } -PathLabel 'VM/BackupCopy')
        @($script:logged | Where-Object { $_ -match '^\[VM/BackupCopy\] Dropped 1 duplicate session' }).Count | Should -Be 1
    }

    It 'does not log a drop line when there were no duplicates' {
        $script:logged = [System.Collections.Generic.List[string]]::new()
        Mock Write-LogFile -MockWith { $script:logged.Add($Message) }
        Mock Invoke-VhciCBackupSessionFetch -MockWith { @($script:Own) }
        $null = @(Get-VhciJobSessions -Jobs @($script:ParentJob) -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand { @() } -PathLabel 'VM/BackupCopy')
        @($script:logged | Where-Object { $_ -match 'duplicate session' }).Count | Should -Be 0
    }

    It 'keeps sessions that have no Id (cannot be compared, so never dropped)' {
        $a = [PSCustomObject]@{ CreationTime = (Get-Date).AddHours(-1); JobName = 'A' }
        $b = [PSCustomObject]@{ CreationTime = (Get-Date).AddHours(-1); JobName = 'B' }
        Mock Invoke-VhciCBackupSessionFetch -MockWith { @($a, $b) }
        $r = @(Get-VhciJobSessions -Jobs @($script:ParentJob, $script:WorkerJob) -Since (Get-Date).AddDays(-7) `
            -SlowPathCommand { @() } -PathLabel 'VM/BackupCopy')
        $r.Count | Should -Be 4
    }
}
