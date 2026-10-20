#Requires -Version 7.0
# Pester v5 tests for Get-VhciNasJob: one NAS job that cannot be wrapped (for
# example its repository was deleted) must not blank _nasBackup.csv / _nasBCJ.csv.
# Real failure seen on a lab VBR 13 server: "Failed to wrap nas backup job [<id>]:
# Backup repository '<id>' was not found" thrown by the whole-list enumeration.

BeforeAll {
    # Stubs so Mock has something to attach to when the Veeam module is absent.
    function global:Get-VBRUnstructuredBackupJob { param([string[]]$Name) }
    function global:Get-VBRUnstructuredBackup { param() }
    function global:Get-VBRUnstructuredBackupRestorePoint { param($Backup) }
    function global:Get-VBRNASBackupCopyJob { param() }

    $privateDir = Split-Path -Parent $PSCommandPath
    $publicDir  = Join-Path (Split-Path -Parent $privateDir) 'Public'
    . (Join-Path $publicDir 'Write-LogFile.ps1')
    . (Join-Path $privateDir 'Export-VhciCsv.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')

    function script:New-FakeNasJob {
        param([string]$Name)
        [PSCustomObject]@{ Name = $Name; Id = [guid]::NewGuid() }
    }
    $script:BrokenMsg = "Failed to wrap nas backup job [5cc08fe5-09b8-4b7d-a52c-c37605d224d6]: Backup repository '280cd331-b099-4617-b59b-7488ad8d6046' was not found"
}

Describe 'Get-VhciNasJob' {

    BeforeEach {
        $script:Exports = @{}
        $script:Logs    = [System.Collections.Generic.List[object]]::new()
        Mock Write-LogFile { param($Message, $LogLevel) $script:Logs.Add([pscustomobject]@{ Msg = $Message; Level = $LogLevel }) }
        Mock Export-VhciCsv { param($FileName, [Parameter(ValueFromPipeline)]$InputObject) if (-not $script:Exports.ContainsKey($FileName)) { $script:Exports[$FileName] = @() }; if ($null -ne $InputObject) { $script:Exports[$FileName] += $InputObject } }
        Mock Get-VBRUnstructuredBackupRestorePoint { @() }
        Mock Get-VBRNASBackupCopyJob { @() }

        $script:Good1 = script:New-FakeNasJob 'NAS-Good-1'
        $script:Good2 = script:New-FakeNasJob 'NAS-Good-2'
        $script:Bad   = script:New-FakeNasJob 'NAS-Bad'
        Mock Get-VBRUnstructuredBackup {
            @([pscustomobject]@{ Name = 'NAS-Good-1' }, [pscustomobject]@{ Name = 'NAS-Bad' }, [pscustomobject]@{ Name = 'NAS-Good-2' })
        }
    }

    It 'all jobs healthy: exports every job, no warnings' {
        Mock Get-VBRUnstructuredBackupJob { @($script:Good1, $script:Good2) }
        Get-VhciNasJob
        @($script:Exports['_nasBackup.csv']).Count | Should -Be 2
        @($script:Logs | Where-Object Level -eq 'WARNING').Count | Should -Be 0
        @($script:Logs | Where-Object Level -eq 'ERROR').Count | Should -Be 0
    }

    It 'one broken job: still exports the healthy jobs and warns naming the broken one' {
        Mock Get-VBRUnstructuredBackupJob {
            if ($null -ne $Name) {
                switch ($Name) {
                    'NAS-Good-1' { $script:Good1 }
                    'NAS-Good-2' { $script:Good2 }
                    default      { throw $script:BrokenMsg }
                }
            } else { throw $script:BrokenMsg }
        }
        { Get-VhciNasJob } | Should -Not -Throw
        $rows = @($script:Exports['_nasBackup.csv'])
        $rows.Count | Should -Be 2
        @($rows.Name | Sort-Object) | Should -Be @('NAS-Good-1', 'NAS-Good-2')
        $warn = @($script:Logs | Where-Object { $_.Level -eq 'WARNING' -and $_.Msg -match 'NAS-Bad' -and $_.Msg -match '280cd331' })
        $warn.Count | Should -BeGreaterThan 0
    }

    It 'broken job: nasBCJ.csv is still written' {
        Mock Get-VBRNASBackupCopyJob { @([pscustomobject]@{ Name = 'BCJ-1' }) }
        Mock Get-VBRUnstructuredBackupJob { throw $script:BrokenMsg }
        Get-VhciNasJob
        $script:Exports.ContainsKey('_nasBCJ.csv') | Should -BeTrue
        @($script:Exports['_nasBCJ.csv']).Count | Should -Be 1
    }

    It 'enumeration and every per-job fallback fail: does not throw and logs the gap' {
        Mock Get-VBRUnstructuredBackupJob { throw $script:BrokenMsg }
        { Get-VhciNasJob } | Should -Not -Throw
        @($script:Logs | Where-Object { $_.Level -eq 'WARNING' }).Count | Should -BeGreaterThan 0
    }

    It 'a failing Get-VBRNASBackupCopyJob does not stop nasBackup.csv' {
        Mock Get-VBRUnstructuredBackupJob { @($script:Good1) }
        Mock Get-VBRNASBackupCopyJob { throw $script:BrokenMsg }
        Get-VhciNasJob
        @($script:Exports['_nasBackup.csv']).Count | Should -Be 1
        @($script:Logs | Where-Object { $_.Level -eq 'WARNING' -and $_.Msg -match 'copy' }).Count | Should -BeGreaterThan 0
    }
}
