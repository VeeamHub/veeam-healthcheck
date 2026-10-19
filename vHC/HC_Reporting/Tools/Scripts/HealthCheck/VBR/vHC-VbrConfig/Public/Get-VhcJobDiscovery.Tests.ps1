#Requires -Version 7.0
# Pester v5 tests for Get-VhcJobDiscovery (issue #224).
#
# The tier B / tier C discovery logic itself is exercised end to end by
# Get-VhcJob.Tests.ps1 (the logic moved here unchanged from Get-VhcJob). These
# tests cover what is new: the result object splits the job set into the parts
# Get-VhcJob and Get-VhcBackupSessions each need.

BeforeAll {
    if (-not (Get-Command Get-VBRJob -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRJob { param([string]$WarningAction) }
    }
    if (-not (Get-Command Get-VBRBackup -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRBackup { param([string]$WarningAction) }
    }
    if (-not (Get-Command Add-VhciModuleError -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }
    }
    if (-not (Get-Command Invoke-VhciCBackupJobFetch -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Invoke-VhciCBackupJobFetch { }
    }

    function script:New-FakeJob {
        param([string]$Name = 'FakeJob', [guid]$Id = [guid]::NewGuid())
        [PSCustomObject]@{ Id = $Id; Name = $Name }
    }

    # A backup whose GetJob() resolves to $ResolvedJob (tier B) or, for a
    # standalone agent backup, to $ResolvedJob with IsAgentStandaloneJob set.
    function script:New-FakeBackup {
        param($ResolvedJob, [bool]$Standalone = $false, [guid]$JobId = [guid]::Empty)
        $capture = $ResolvedJob
        $backup = [PSCustomObject]@{
            Id                   = [guid]::NewGuid()
            Name                 = 'Backup'
            JobId                = $JobId
            IsAgentStandaloneJob = $Standalone
        }
        $backup | Add-Member -MemberType ScriptMethod -Name GetJob -Value { return $capture }.GetNewClosure()
        return $backup
    }

    $moduleRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $moduleRoot 'Public/Write-LogFile.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
}

Describe 'Get-VhcJobDiscovery: result shape' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Add-VhciModuleError -MockWith { }
        Mock Invoke-VhciCBackupJobFetch -MockWith { @() }
        $script:Visible    = script:New-FakeJob 'Visible'
        $script:Hidden     = script:New-FakeJob 'HiddenFromGetVBRJob'
        $script:Standalone = script:New-FakeJob 'StandaloneAgent'
        Mock Get-VBRJob { @($script:Visible) }
        Mock Get-VBRBackup {
            @(
                (script:New-FakeBackup -ResolvedJob $script:Standalone -Standalone $true),
                (script:New-FakeBackup -ResolvedJob $script:Hidden)
            )
        }
    }

    It 'puts only what Get-VBRJob returned in VbrJobs' {
        $r = Get-VhcJobDiscovery -VBRVersion 13
        @($r.VbrJobs).Count | Should -Be 1
        $r.VbrJobs[0].Id | Should -Be $script:Visible.Id
    }

    It 'puts a job Get-VBRJob does not return in DiscoveredJobs, not in VbrJobs' {
        $r = Get-VhcJobDiscovery -VBRVersion 13
        @($r.DiscoveredJobs).Count | Should -Be 1
        $r.DiscoveredJobs[0].Id | Should -Be $script:Hidden.Id
        @($r.VbrJobs | Where-Object { $_.Id -eq $script:Hidden.Id }).Count | Should -Be 0
    }

    It 'keeps standalone agent jobs out of DiscoveredJobs' {
        $r = Get-VhcJobDiscovery -VBRVersion 13
        @($r.StandaloneAgentJobs).Count | Should -Be 1
        @($r.DiscoveredJobs | Where-Object { $_.Id -eq $script:Standalone.Id }).Count | Should -Be 0
    }

    It 'returns every job in Jobs, in the order Get-VhcJob has always used' {
        $r = Get-VhcJobDiscovery -VBRVersion 13
        @($r.Jobs).Count | Should -Be 3
        @($r.Jobs | ForEach-Object { $_.Id }) | Should -Be @($script:Visible.Id, $script:Standalone.Id, $script:Hidden.Id)
    }

    It 'returns an empty DiscoveredJobs when Get-VBRJob already sees everything' {
        Mock Get-VBRBackup { @() }
        $r = Get-VhcJobDiscovery -VBRVersion 13
        @($r.DiscoveredJobs).Count | Should -Be 0
        @($r.Jobs).Count | Should -Be 1
    }

    It 'does not throw and returns empty lists when Get-VBRJob fails' {
        Mock Get-VBRJob { throw 'service down' }
        Mock Get-VBRBackup { @() }
        $r = Get-VhcJobDiscovery -VBRVersion 13
        @($r.VbrJobs).Count | Should -Be 0
        @($r.DiscoveredJobs).Count | Should -Be 0
        Should -Invoke Add-VhciModuleError -Times 1 -Exactly -ParameterFilter { $CollectorName -eq 'Jobs' }
    }
}

# ---------------------------------------------------------------------------
# Regression guard: where Get-VBRJob already returns every job, tiers B and C
# run (VBR 12) over backups and core job records that all belong to those
# visible jobs, and must add nothing. Get-VhcBackupSessions queries
# DiscoveredJobs, so anything added here changes the session job set.
# ---------------------------------------------------------------------------
Describe 'Get-VhcJobDiscovery: nothing hidden means nothing discovered (VBR 12, tiers B and C run)' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Add-VhciModuleError -MockWith { }
        $script:Visible = script:New-FakeJob 'Visible'
        Mock Get-VBRJob { @($script:Visible) }
        Mock Invoke-VhciCBackupJobFetch -MockWith { @() }
    }

    It 'tier B adds nothing for a backup whose JobId is a visible job' {
        $visibleId = $script:Visible.Id
        Mock Get-VBRBackup { @( (script:New-FakeBackup -ResolvedJob (script:New-FakeJob 'ShouldNotBeResolved') -JobId $visibleId) ) }
        $r = Get-VhcJobDiscovery -VBRVersion 12
        @($r.DiscoveredJobs).Count | Should -Be 0
        @($r.Jobs).Count | Should -Be 1
    }

    It 'tier B adds nothing when GetJob() resolves to a child whose parent is a visible job' {
        $parent = $script:Visible
        $child = script:New-FakeJob 'ChildOfVisible'
        $child | Add-Member -MemberType ScriptMethod -Name GetParentJob -Value { return $parent }.GetNewClosure()
        Mock Get-VBRBackup { @( (script:New-FakeBackup -ResolvedJob $child) ) }
        $r = Get-VhcJobDiscovery -VBRVersion 12
        @($r.DiscoveredJobs).Count | Should -Be 0
    }

    It 'tier C adds nothing when a core job record resolves to a visible job' {
        Mock Get-VBRBackup { @() }
        $parent = $script:Visible
        $record = [PSCustomObject]@{ Id = [guid]::NewGuid(); HasNoJobRecord = $false }
        $record | Add-Member -MemberType ScriptMethod -Name GetParent -Value { return $parent }.GetNewClosure()
        Mock Invoke-VhciCBackupJobFetch -MockWith { @($record) }
        $r = Get-VhcJobDiscovery -VBRVersion 12
        @($r.DiscoveredJobs).Count | Should -Be 0
        Should -Invoke Invoke-VhciCBackupJobFetch -Times 1 -Exactly
    }
}
